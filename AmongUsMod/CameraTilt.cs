using HarmonyLib;
using UnityEngine;

namespace AmongUsMod;

/// <summary>
/// Camera Tilt — gives the game a 3D perspective feel by switching the main camera
/// from orthographic to perspective and applying a pitch rotation so you look down
/// at the 3D crewmate models at an angle.
///
/// The game's FollowerCamera only updates position; we hook its Update postfix
/// and re-apply rotation + perspective settings each frame so nothing overrides us.
/// </summary>
[HarmonyPatch]
public static class CameraTilt
{
    // Tuning — edit to taste
    private const float TILT_DEGREES = 35f;   // how far to pitch the camera down
    private const float CAMERA_HEIGHT = -12f; // Z distance (negative = pull camera back along -Z)
    private const float FIELD_OF_VIEW = 50f;  // perspective FOV when tilt is active
    private const bool USE_PERSPECTIVE = true;

    private static Camera _cachedCamera;

    [HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
    [HarmonyPostfix]
    public static void FollowerCameraUpdate_Postfix(FollowerCamera __instance)
    {
        var cam = GetMainCamera();
        if (cam == null) return;

        // Switch to perspective once (and keep it that way)
        if (USE_PERSPECTIVE && cam.orthographic)
        {
            cam.orthographic = false;
            cam.fieldOfView = FIELD_OF_VIEW;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            AmongUsModPlugin.Log.LogInfo("[CamTilt] Switched main camera to perspective");
        }

        // Apply tilt and pull the camera back so the target is still framed
        var camT = cam.transform;

        // Pitch the camera down toward the play field
        camT.localRotation = Quaternion.Euler(TILT_DEGREES, 0f, 0f);

        // Push the camera back/up along the tilt vector so it still points at the player
        // The FollowerCamera already moved the parent transform to follow the target,
        // so we only adjust local Z (distance from target) and local Y (height offset)
        float rad = TILT_DEGREES * Mathf.Deg2Rad;
        float yOffset = -Mathf.Sin(rad) * CAMERA_HEIGHT;
        float zOffset = -Mathf.Cos(rad) * Mathf.Abs(CAMERA_HEIGHT);
        camT.localPosition = new Vector3(0f, yOffset, zOffset);
    }

    private static Camera GetMainCamera()
    {
        if (_cachedCamera != null) return _cachedCamera;
        _cachedCamera = Camera.main;
        return _cachedCamera;
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    [HarmonyPostfix]
    public static void OnGameEnd_Postfix()
    {
        // Clear the cache so we re-grab Camera.main on the next game
        _cachedCamera = null;
    }
}
