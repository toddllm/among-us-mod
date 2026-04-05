using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using UnityEngine;
using AmongUs.GameOptions;

namespace AmongUsMod;

/// <summary>
/// AI NPC Bot System — fills empty player slots with bots that:
/// - Move around the map randomly
/// - Complete tasks (crewmate bots)
/// - Vote during meetings (random vote or skip)
/// - Provide enough players for a proper game
/// </summary>
[HarmonyPatch]
public static class AINpcBots
{
    // Bot configuration
    private const int TARGET_PLAYER_COUNT = 10;
    private const float BOT_MOVE_INTERVAL = 2.5f;
    private const float BOT_TASK_INTERVAL = 15f;
    private const float BOT_SPEED = 1.0f;

    private static readonly Dictionary<byte, BotState> BotStates = new();
    private static float _lastBotSpawnCheck;

    private class BotState
    {
        public Vector2 MoveTarget;
        public float MoveTimer;
        public float TaskTimer;
    }

    // =========================================================================
    //  BOT SPAWNING — Add bots in the lobby when not enough players
    // =========================================================================

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
    [HarmonyPostfix]
    public static void LobbyUpdate_Postfix(GameStartManager __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (Time.time - _lastBotSpawnCheck < 2f) return;
        _lastBotSpawnCheck = Time.time;

        var currentCount = GameData.Instance?.AllPlayers?.Count ?? 0;
        if (currentCount >= TARGET_PLAYER_COUNT) return;

        var botsNeeded = TARGET_PLAYER_COUNT - currentCount;
        if (botsNeeded <= 0) return;

        AmongUsModPlugin.Log.LogInfo($"[AINpc] {currentCount} players, need {botsNeeded} bots");

        for (int i = 0; i < botsNeeded; i++)
        {
            SpawnBot(i);
        }
    }

    private static void SpawnBot(int index)
    {
        try
        {
            var playerPrefab = AmongUsClient.Instance.PlayerPrefab;
            if (playerPrefab == null) return;

            var bot = UnityEngine.Object.Instantiate(playerPrefab);
            if (bot == null) return;

            bot.isDummy = true;
            bot.notRealPlayer = true;
            bot.PlayerId = (byte)(200 + index);

            BotStates[bot.PlayerId] = new BotState
            {
                MoveTarget = Vector2.zero,
                MoveTimer = 0f,
                TaskTimer = 0f,
            };

            AmongUsModPlugin.Log.LogInfo($"[AINpc] Spawned bot {bot.PlayerId}");
        }
        catch (Exception e)
        {
            AmongUsModPlugin.Log.LogWarning($"[AINpc] Bot spawn failed: {e.Message}");
        }
    }

    // =========================================================================
    //  BOT MOVEMENT — Random wandering during gameplay
    // =========================================================================

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdate_Postfix(PlayerControl __instance)
    {
        if (!__instance.isDummy && !__instance.notRealPlayer) return;
        if (!AmongUsClient.Instance.AmHost) return;

        var pid = __instance.PlayerId;
        if (!BotStates.TryGetValue(pid, out var state))
        {
            state = new BotState();
            BotStates[pid] = state;
        }

        var data = __instance.Data;
        if (data == null || data.IsDead) return;

        // Movement AI
        state.MoveTimer += Time.fixedDeltaTime;
        if (state.MoveTimer >= BOT_MOVE_INTERVAL)
        {
            state.MoveTimer = 0f;
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = UnityEngine.Random.Range(1f, 4f);
            state.MoveTarget = (Vector2)__instance.transform.position +
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
        }

        if (__instance.moveable && __instance.MyPhysics != null)
        {
            var currentPos = (Vector2)__instance.transform.position;
            var delta = state.MoveTarget - currentPos;
            if (delta.magnitude > 0.1f)
            {
                var direction = delta.normalized;
                __instance.MyPhysics.body?.SetVelocity(direction * BOT_SPEED);
            }
            else
            {
                __instance.MyPhysics.body?.SetVelocity(Vector2.zero);
            }
        }

        // Task completion AI (crewmate bots)
        if (!RoleManager.IsImpostorRole(data.RoleType))
        {
            state.TaskTimer += Time.fixedDeltaTime;
            if (state.TaskTimer >= BOT_TASK_INTERVAL)
            {
                state.TaskTimer = 0f;
                TryCompleteTask(__instance);
            }
        }
    }

    private static void TryCompleteTask(PlayerControl bot)
    {
        if (bot.myTasks == null || bot.myTasks.Count == 0) return;

        for (int i = 0; i < bot.myTasks.Count; i++)
        {
            var task = bot.myTasks[i];
            if (task != null && !task.IsComplete)
            {
                bot.RpcCompleteTask(task.Id);
                AmongUsModPlugin.Log.LogInfo(
                    $"[AINpc] Bot {bot.PlayerId} completed task {task.Id}");
                break;
            }
        }
    }

    // =========================================================================
    //  BOT VOTING — Bots vote during meetings
    // =========================================================================

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    public static void MeetingStart_Postfix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        AmongUsModPlugin.Log.LogInfo("[AINpc] Meeting started, scheduling bot votes");

        // Use BepInEx IL2CPP coroutine utility
        __instance.StartCoroutine(BotVoteCoroutine(__instance));
    }

    private static IEnumerator BotVoteCoroutine(MeetingHud meeting)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 15f));

        if (meeting == null) yield break;

        var allPlayers = PlayerControl.AllPlayerControls;
        if (allPlayers == null) yield break;

        var alivePlayerIds = new List<byte>();
        for (int i = 0; i < allPlayers.Count; i++)
        {
            var p = allPlayers[i];
            if (p?.Data != null && !p.Data.IsDead && !p.Data.Disconnected)
            {
                alivePlayerIds.Add(p.PlayerId);
            }
        }

        for (int i = 0; i < allPlayers.Count; i++)
        {
            var bot = allPlayers[i];
            if (bot == null || (!bot.isDummy && !bot.notRealPlayer)) continue;
            if (bot.Data == null || bot.Data.IsDead) continue;

            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 2f));

            byte voteTarget;
            if (UnityEngine.Random.value < 0.4f || alivePlayerIds.Count == 0)
            {
                voteTarget = 253; // skip
            }
            else
            {
                var candidates = alivePlayerIds.Where(id => id != bot.PlayerId).ToList();
                voteTarget = candidates.Count > 0
                    ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
                    : (byte)253;
            }

            try
            {
                meeting.CastVote(bot.PlayerId, voteTarget);
                AmongUsModPlugin.Log.LogInfo(
                    $"[AINpc] Bot {bot.PlayerId} voted for " +
                    (voteTarget == 253 ? "SKIP" : $"player {voteTarget}"));
            }
            catch (Exception e)
            {
                AmongUsModPlugin.Log.LogWarning($"[AINpc] Vote failed: {e.Message}");
            }
        }
    }

    // =========================================================================
    //  CLEANUP
    // =========================================================================

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void OnGameEnd_Postfix()
    {
        BotStates.Clear();
        AmongUsModPlugin.Log.LogInfo("[AINpc] Game ended, bot states cleared");
    }
}
