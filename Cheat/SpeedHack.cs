using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using SinmaiAssist.Utils;
using UnityEngine;

namespace SinmaiAssist.Cheat;

public static class SpeedHack
{
    public const float Step = 0.1f;
    public const float MinSpeed = 0.1f;
    public const float DefaultSpeed = 1.0f;
    private static bool _ownsTimeScale;
    private static float _previousTimeScale;
    private static float _appliedTimeScale;

    public static void OnUpdate()
    {
        // Kept as a compatibility hook; Runtime.Commands owns keyboard dispatch.
    }

    public static void IncreaseSpeed()
    {
        ApplySpeed(SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier + Step, true);
    }

    public static void DecreaseSpeed()
    {
        ApplySpeed(SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier - Step, true);
    }

    public static void ResetSpeed()
    {
        ApplySpeed(DefaultSpeed, true);
    }

    public static void ApplyCurrentSpeed(bool notify)
    {
        ApplySpeed(SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier, notify);
    }

    public static void ApplySpeed(float speed, bool notify)
    {
        float clampedSpeed = float.IsNaN(speed) || float.IsInfinity(speed) ? DefaultSpeed : Mathf.Max(MinSpeed, speed);
        clampedSpeed = Mathf.Round(clampedSpeed * 10f) / 10f;
        SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier = clampedSpeed;

        if (PlaySession.Owner != null) NotesManager.UpdateTimer();
        SpeedHackAudioClockPatch.Flush();
        ApplyGlobalSpeedState();

        try
        {
            float currentPitchCents = PlaybackRate.MusicPitch;
            SpeedHackBgmPitchPatch.UpdateActiveBgmPitch(currentPitchCents);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"SpeedHack update active BGM pitch failed: {e.Message}");
        }

        if (notify)
        {
            NotifyCurrentSpeed();
        }
    }

    public static bool IsInChartPlay()
    {
        return NotesManager.Instance().IsPlaying();
    }

    public static bool ShouldScaleGameClock()
    {
        var config = SinmaiAssist.MainConfig.Cheat.SpeedHack;
        return !SinmaiAssist.MainConfig.ModSetting.SafeMode && (PracticeLoop.Session || config.Enable && (IsInChartPlay() || config.EnableGlobalSpeed));
    }

    public static bool ShouldApplyTimeScale()
    {
        var config = SinmaiAssist.MainConfig.Cheat.SpeedHack;
        return !SinmaiAssist.MainConfig.ModSetting.SafeMode && config.Enable && config.EnableGlobalSpeed && !IsInChartPlay();
    }

    public static void ApplyGlobalSpeedState()
    {
        if (!ShouldApplyTimeScale()) { RestoreGlobalSpeed(); return; }
        if (!_ownsTimeScale) { _previousTimeScale = Time.timeScale; _ownsTimeScale = true; }
        _appliedTimeScale = GetCurrentSpeed();
        Time.timeScale = _appliedTimeScale;
    }

    public static void RestoreGlobalSpeed()
    {
        if (!_ownsTimeScale) return;
        if (Mathf.Approximately(Time.timeScale, _appliedTimeScale)) Time.timeScale = _previousTimeScale;
        _ownsTimeScale = false;
    }

    public static float GetCurrentSpeed()
    {
        return Mathf.Max(MinSpeed, SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier);
    }

    public static float GetPitchCents()
    {
        return (float)(1200.0 * Math.Log(GetCurrentSpeed(), 2.0));
    }

    public static void NotifyCurrentSpeed()
    {
        string message = $"SpeedHack: x{SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier:0.0}";
        GameMessageManager.SendMessage(0, message);
        GameMessageManager.SendMessage(1, message);
    }
}

internal static class SpeedHackGameClockPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetGameMSecAddD")]
    private static void GetGameMSecAddDPostfix(ref double __result)
    {
        if (!SpeedHack.ShouldScaleGameClock())
        {
            return;
        }
        __result *= PlaybackRate.Effective;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetGameMSecAdd")]
    private static void GetGameMSecAddPostfix(ref long __result)
    {
        if (!SpeedHack.ShouldScaleGameClock())
        {
            return;
        }
        __result = (long)(__result * PlaybackRate.Effective);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetGameFrameAdd")]
    private static void GetGameFrameAddPostfix(ref float __result)
    {
        if (!SpeedHack.ShouldScaleGameClock())
        {
            return;
        }
        __result *= PlaybackRate.Effective;
    }
}

internal static class SpeedHackBgmPitchPatch
{
    private static readonly int[] BgmPlayerIds =
    {
        (int)SoundManager.PlayerID.Music,
        (int)SoundManager.PlayerID.Bgm,
        (int)SoundManager.PlayerID.Sound_Sepalate_Right
    };

    private static float NormalPitch => SinmaiAssist.MainConfig.ModSetting.SafeMode || !SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable ? 0 : SpeedHack.GetPitchCents();
    private static SoundCtrl _activeSoundCtrl;
    private static float _lastPitchCents = float.NaN;
    private static bool _loggedPitchApplied = false;
    private static bool _loggedPitchFailed = false;
    private static readonly System.Collections.Generic.Dictionary<System.Type, (MethodInfo setPitchFloat, MethodInfo setPitchInt, MethodInfo updateAll)> MethodCache = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), "Play")]
    private static void PlayPostfix(SoundCtrl __instance, SoundCtrl.PlaySetting setting)
    {
        if (!IsBgmPlayer(setting.PlayerID))
        {
            return;
        }

        _activeSoundCtrl = __instance;
        float pitchCents = setting.PlayerID == (int)SoundManager.PlayerID.Music ? PlaybackRate.MusicPitch : NormalPitch;
        SpeedHackAudioClockPatch.Reset(setting.PlayerID, setting.StartTime);
        ApplyPitch(_activeSoundCtrl, setting.PlayerID, pitchCents, true);
    }

    public static void UpdateActiveBgmPitch(float pitchCents)
    {
        if (_activeSoundCtrl == null)
        {
            try
            {
                _activeSoundCtrl = Singleton<SoundCtrl>.Instance;
            }
            catch
            {
                return;
            }
        }

        bool hasChange = !Mathf.Approximately(_lastPitchCents, pitchCents);

        for (int i = 0; i < BgmPlayerIds.Length; i++)
        {
            ApplyPitch(_activeSoundCtrl, BgmPlayerIds[i], BgmPlayerIds[i] == (int)SoundManager.PlayerID.Music ? pitchCents : NormalPitch, hasChange);
        }

        _lastPitchCents = pitchCents;
    }

    internal static bool IsBgmPlayer(int playerId)
    {
        for (int i = 0; i < BgmPlayerIds.Length; i++)
        {
            if (BgmPlayerIds[i] == playerId)
            {
                return true;
            }
        }
        return false;
    }

    private static void ApplyPitch(SoundCtrl soundCtrl, int playerId, float pitchCents, bool hasChange)
    {
        try
        {
            if (soundCtrl == null) return;

            IDictionary players = Traverse.Create(soundCtrl).Field("_players").GetValue<IDictionary>();
            if (players == null || !players.Contains(playerId)) return;

            object playerObj = players[playerId];
            if (playerObj == null) return;

            object player = Traverse.Create(playerObj).Field("Player").GetValue<object>();
            if (player == null) return;

            System.Type playerType = player.GetType();
            if (!MethodCache.TryGetValue(playerType, out var methods))
            {
                methods = (
                    AccessTools.Method(playerType, "SetPitch", new[] { typeof(float) }),
                    AccessTools.Method(playerType, "SetPitch", new[] { typeof(int) }),
                    AccessTools.Method(playerType, "UpdateAll")
                );
                MethodCache[playerType] = methods;
            }

            bool pitchApplied = false;

            if (methods.setPitchFloat != null)
            {
                methods.setPitchFloat.Invoke(player, new object[] { pitchCents });
                pitchApplied = true;
            }
            else if (methods.setPitchInt != null)
            {
                methods.setPitchInt.Invoke(player, new object[] { Mathf.RoundToInt(pitchCents) });
                pitchApplied = true;
            }

            if (pitchApplied && methods.updateAll != null)
            {
                methods.updateAll.Invoke(player, null);
            }

            if (hasChange && pitchApplied && !_loggedPitchApplied)
            {
                MelonLogger.Msg($"SpeedHack pitch hook applied. player={playerId}, cents={pitchCents:0.##}");
                _loggedPitchApplied = true;
                _loggedPitchFailed = false;
            }
            else if (hasChange && !pitchApplied && !_loggedPitchFailed)
            {
                MelonLogger.Warning("SpeedHack pitch hook not applied: SetPitch method not found.");
                _loggedPitchFailed = true;
                _loggedPitchApplied = false;
            }
        }
        catch (Exception e)
        {
            if (hasChange && !_loggedPitchFailed)
            {
                MelonLogger.Warning($"SpeedHack apply BGM pitch failed (player:{playerId}): {e.Message}");
                _loggedPitchFailed = true;
                _loggedPitchApplied = false;
            }
        }
    }
}
