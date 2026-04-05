using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;

namespace AmongUsMod;

/// <summary>
/// Always Impostor — forces the local player to be Impostor every game.
/// Hooks RoleManager.SelectRoles (runs on host only) and swaps roles
/// so the local player always ends up as Impostor.
/// </summary>
[HarmonyPatch]
public static class AlwaysImpostor
{
    /// <summary>
    /// After roles are assigned, check if local player is Impostor.
    /// If not, swap with an existing Impostor.
    /// </summary>
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    [HarmonyPostfix]
    public static void SelectRoles_Postfix(RoleManager __instance)
    {
        // Only the host assigns roles
        if (!AmongUsClient.Instance.AmHost) return;

        var localPlayer = PlayerControl.LocalPlayer;
        if (localPlayer == null) return;

        var localData = localPlayer.Data;
        if (localData == null) return;

        // Check if local player already has an impostor role
        if (IsImpostorRole(localData.RoleType))
        {
            AmongUsModPlugin.Log.LogInfo("[AlwaysImpostor] Already Impostor!");
            return;
        }

        // Find an existing impostor to swap with
        var allPlayers = GameData.Instance.AllPlayers;
        NetworkedPlayerInfo impostorToSwap = null;

        for (int i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            if (player == null || player.PlayerId == localData.PlayerId) continue;
            if (player.Disconnected) continue;

            if (IsImpostorRole(player.RoleType))
            {
                impostorToSwap = player;
                break;
            }
        }

        if (impostorToSwap != null)
        {
            // Swap roles: give local player the impostor's role, give impostor crewmate
            var impostorRole = impostorToSwap.RoleType;
            var localRole = localData.RoleType;

            var impostorPC = impostorToSwap.Object;
            if (impostorPC != null)
            {
                AmongUsModPlugin.Log.LogInfo(
                    $"[AlwaysImpostor] Swapping: local({localData.PlayerId}) gets {impostorRole}, " +
                    $"player({impostorToSwap.PlayerId}) gets {localRole}");

                localPlayer.RpcSetRole(impostorRole, true);
                impostorPC.RpcSetRole(localRole, true);
            }
        }
        else
        {
            // No impostor found (shouldn't happen), force impostor role
            AmongUsModPlugin.Log.LogInfo("[AlwaysImpostor] No impostor to swap, forcing role");
            localPlayer.RpcSetRole(RoleTypes.Impostor, true);
        }
    }

    /// <summary>
    /// Ensure at least 1 impostor is set in game options.
    /// </summary>
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
    [HarmonyPrefix]
    public static void BeginGame_Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var opts = GameOptionsManager.Instance?.CurrentGameOptions;
        if (opts != null && opts.NumImpostors < 1)
        {
            opts.NumImpostors = 1;
            AmongUsModPlugin.Log.LogInfo("[AlwaysImpostor] Set NumImpostors to 1");
        }
    }

    private static bool IsImpostorRole(RoleTypes role)
    {
        return role == RoleTypes.Impostor
            || role == RoleTypes.Shapeshifter
            || role == RoleTypes.Phantom;
    }
}
