using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AmongUsMod;

/// <summary>
/// 3D Crewmates — replaces the 2D sprite rendering with primitive-based 3D models.
///
/// v1: builds a recognizable 3D crewmate from Unity primitives (capsule body +
/// rounded visor + backpack) so we don't need external asset bundles yet.
/// v2 (planned): load FBX models from an AssetBundle.
///
/// How it works:
///   1. On PlayerControl.Start, build a 3D model GameObject and parent it to the player.
///   2. Hide the CosmeticsLayer sprite renderers (keep them for tasks/UI hit tests).
///   3. On FixedUpdate, sync model rotation to movement direction and bob it while walking.
///   4. Sync the body material color to the player's color palette.
/// </summary>
[HarmonyPatch]
public static class ThreeDCrewmates
{
    // Track 3D rig per player so we can update and clean up
    private static readonly Dictionary<int, CrewmateRig> Rigs = new();

    /// <summary>Container for a player's 3D model + refs needed for updates.</summary>
    private class CrewmateRig
    {
        public GameObject Root;
        public Renderer BodyRenderer;
        public Material BodyMaterial;
        public Transform Body;
        public Transform LegL;
        public Transform LegR;
        public float WalkPhase;
        public int LastColorId = -1;
    }

    // =========================================================================
    //  ATTACH — Build the 3D rig when a player spawns
    // =========================================================================

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    [HarmonyPostfix]
    public static void Start_Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        var key = __instance.GetInstanceID();
        if (Rigs.ContainsKey(key)) return;

        var rig = BuildRig(__instance);
        if (rig != null)
        {
            Rigs[key] = rig;
            HideSprites(__instance);
            AmongUsModPlugin.Log.LogInfo($"[3D] Built rig for player {__instance.PlayerId}");
        }
    }

    /// <summary>Build a simple 3D crewmate from primitives.</summary>
    private static CrewmateRig BuildRig(PlayerControl pc)
    {
        var root = new GameObject("Crewmate3D");
        root.transform.SetParent(pc.transform, worldPositionStays: false);
        root.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        root.transform.localScale = Vector3.one * 0.5f;

        // Body — capsule, slightly squashed to match crewmate proportions
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
        // Disable the primitive's collider (we don't want physics interference)
        var bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Object.Destroy(bodyCol);

        // Visor — squashed sphere on the front of the head
        var visor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visor.name = "Visor";
        visor.transform.SetParent(root.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.9f, 0.35f);
        visor.transform.localScale = new Vector3(0.5f, 0.3f, 0.2f);
        var visorCol = visor.GetComponent<Collider>();
        if (visorCol != null) Object.Destroy(visorCol);
        var visorMat = new Material(Shader.Find("Standard"));
        visorMat.color = new Color(0.5f, 0.85f, 1f, 1f);
        visorMat.SetFloat("_Metallic", 0.7f);
        visorMat.SetFloat("_Glossiness", 0.9f);
        visor.GetComponent<Renderer>().material = visorMat;

        // Backpack — cube on the back
        var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pack.name = "Backpack";
        pack.transform.SetParent(root.transform, false);
        pack.transform.localPosition = new Vector3(0f, 0.5f, -0.4f);
        pack.transform.localScale = new Vector3(0.5f, 0.5f, 0.3f);
        var packCol = pack.GetComponent<Collider>();
        if (packCol != null) Object.Destroy(packCol);

        // Legs — two short capsules
        var legL = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        legL.name = "LegL";
        legL.transform.SetParent(root.transform, false);
        legL.transform.localPosition = new Vector3(-0.2f, -0.1f, 0f);
        legL.transform.localScale = new Vector3(0.2f, 0.25f, 0.2f);
        var legLCol = legL.GetComponent<Collider>();
        if (legLCol != null) Object.Destroy(legLCol);

        var legR = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        legR.name = "LegR";
        legR.transform.SetParent(root.transform, false);
        legR.transform.localPosition = new Vector3(0.2f, -0.1f, 0f);
        legR.transform.localScale = new Vector3(0.2f, 0.25f, 0.2f);
        var legRCol = legR.GetComponent<Collider>();
        if (legRCol != null) Object.Destroy(legRCol);

        // Body material — color set later by SyncColor
        var bodyRenderer = body.GetComponent<Renderer>();
        var bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = Color.red;
        bodyRenderer.material = bodyMat;

        // Apply the same body material to legs and backpack so they match the suit
        legL.GetComponent<Renderer>().material = bodyMat;
        legR.GetComponent<Renderer>().material = bodyMat;
        pack.GetComponent<Renderer>().material = bodyMat;

        return new CrewmateRig
        {
            Root = root,
            BodyRenderer = bodyRenderer,
            BodyMaterial = bodyMat,
            Body = body.transform,
            LegL = legL.transform,
            LegR = legR.transform,
        };
    }

    /// <summary>Hide the 2D sprite cosmetics layer so the 3D model is the only thing you see.</summary>
    private static void HideSprites(PlayerControl pc)
    {
        if (pc.cosmetics == null) return;
        // Disable all SpriteRenderers under the cosmetics layer
        var renderers = pc.cosmetics.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Count; i++)
        {
            renderers[i].enabled = false;
        }
    }

    // =========================================================================
    //  UPDATE — Rotate + bob the 3D model based on movement every frame
    // =========================================================================

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    [HarmonyPostfix]
    public static void FixedUpdate_Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        var key = __instance.GetInstanceID();
        if (!Rigs.TryGetValue(key, out var rig) || rig.Root == null) return;

        // Sync color to player palette (only on change)
        var data = __instance.Data;
        if (data != null && data.DefaultOutfit != null)
        {
            var colorId = data.DefaultOutfit.ColorId;
            if (colorId != rig.LastColorId)
            {
                rig.LastColorId = colorId;
                try
                {
                    var color = Palette.PlayerColors[colorId];
                    rig.BodyMaterial.color = color;
                }
                catch { /* colorId out of range — leave default */ }
            }
        }

        // Rotation from velocity
        Vector2 vel = Vector2.zero;
        if (__instance.MyPhysics != null && __instance.MyPhysics.body != null)
        {
            vel = __instance.MyPhysics.body.velocity;
        }

        if (vel.sqrMagnitude > 0.01f)
        {
            // Face movement direction — yaw around Y axis
            float yaw = Mathf.Atan2(vel.x, vel.y) * Mathf.Rad2Deg;
            var target = Quaternion.Euler(0f, yaw, 0f);
            rig.Root.transform.localRotation = Quaternion.Slerp(
                rig.Root.transform.localRotation, target, 0.2f);

            // Walk animation: bob the whole rig and swing legs
            rig.WalkPhase += Time.fixedDeltaTime * 8f;
            float bob = Mathf.Abs(Mathf.Sin(rig.WalkPhase)) * 0.05f;
            rig.Root.transform.localPosition = new Vector3(0f, 0.25f + bob, 0f);

            if (rig.LegL != null && rig.LegR != null)
            {
                float swing = Mathf.Sin(rig.WalkPhase) * 20f;
                rig.LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
                rig.LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }
        }
        else
        {
            // Idle — ease back to neutral
            rig.Root.transform.localPosition = Vector3.Lerp(
                rig.Root.transform.localPosition,
                new Vector3(0f, 0.25f, 0f), 0.2f);
            if (rig.LegL != null) rig.LegL.localRotation = Quaternion.identity;
            if (rig.LegR != null) rig.LegR.localRotation = Quaternion.identity;
        }

        // Hide rig when the player is dead (game hides the sprite body; match it)
        if (data != null && data.IsDead)
        {
            if (rig.Root.activeSelf) rig.Root.SetActive(false);
        }
        else
        {
            if (!rig.Root.activeSelf) rig.Root.SetActive(true);
        }
    }

    // =========================================================================
    //  CLEANUP — Destroy rigs when players leave or games end
    // =========================================================================

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnDestroy))]
    [HarmonyPrefix]
    public static void OnDestroy_Prefix(PlayerControl __instance)
    {
        if (__instance == null) return;
        var key = __instance.GetInstanceID();
        if (Rigs.TryGetValue(key, out var rig))
        {
            if (rig.Root != null) Object.Destroy(rig.Root);
            Rigs.Remove(key);
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void OnGameEnd_Postfix()
    {
        foreach (var kv in Rigs)
        {
            if (kv.Value?.Root != null) Object.Destroy(kv.Value.Root);
        }
        Rigs.Clear();
        AmongUsModPlugin.Log.LogInfo("[3D] Cleared all rigs");
    }
}
