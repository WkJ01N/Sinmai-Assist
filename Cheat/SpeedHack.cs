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

    public static void OnUpdate()
    {
        if (DebugInput.GetKeyDown(SinmaiAssist.KeyBindConfig.SpeedHack.Increase.KeyCode))
        {
            IncreaseSpeed();
        }
        else if (DebugInput.GetKeyDown(SinmaiAssist.KeyBindConfig.SpeedHack.Decrease.KeyCode))
        {
            DecreaseSpeed();
        }
        else if (DebugInput.GetKeyDown(SinmaiAssist.KeyBindConfig.SpeedHack.Reset.KeyCode))
        {
            ResetSpeed();
        }
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
        float clampedSpeed = Mathf.Max(MinSpeed, speed);
        clampedSpeed = Mathf.Round(clampedSpeed * 10f) / 10f;
        SinmaiAssist.MainConfig.Cheat.SpeedHack.SpeedMultiplier = clampedSpeed;

        ApplyGlobalSpeedState();

        try
        {
            float currentPitchCents = GetPitchCents();
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
        return config.Enable && (IsInChartPlay() || config.EnableGlobalSpeed);
    }

    public static bool ShouldApplyTimeScale()
    {
        var config = SinmaiAssist.MainConfig.Cheat.SpeedHack;
        return config.Enable && config.EnableGlobalSpeed && !IsInChartPlay();
    }

    public static void ApplyGlobalSpeedState()
    {
        Time.timeScale = ShouldApplyTimeScale() ? GetCurrentSpeed() : DefaultSpeed;
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
        __result *= SpeedHack.GetCurrentSpeed();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetGameMSecAdd")]
    private static void GetGameMSecAddPostfix(ref long __result)
    {
        if (!SpeedHack.ShouldScaleGameClock())
        {
            return;
        }
        __result = (long)(__result * SpeedHack.GetCurrentSpeed());
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), "GetGameFrameAdd")]
    private static void GetGameFrameAddPostfix(ref float __result)
    {
        if (!SpeedHack.ShouldScaleGameClock())
        {
            return;
        }
        __result *= SpeedHack.GetCurrentSpeed();
    }
}

internal static class SpeedHackNotesClockPatch
{
    private static readonly FieldInfo CurMSecField = AccessTools.Field(typeof(NotesManager), "_curMSec");
    private static readonly FieldInfo MsecStartGapField = AccessTools.Field(typeof(NotesManager), "_msecStartGap");
    private static bool _loggedMissingFields;
    private static bool _loggedFieldsResolved;
    private static float _lastRealMsec = float.NaN;
    private static float _timeOffset;

    private static void ResetAccumulators()
    {
        _lastRealMsec = float.NaN;
        _timeOffset = 0f;
    }

    private static bool TryResolveFields()
    {
        if (CurMSecField != null && MsecStartGapField != null)
        {
            if (!_loggedFieldsResolved)
            {
                _loggedFieldsResolved = true;
                MelonLogger.Msg("SpeedHack: NotesManager clock fields resolved.");
            }
            return true;
        }

        if (!_loggedMissingFields)
        {
            _loggedMissingFields = true;
            MelonLogger.Warning("SpeedHack: NotesManager _curMSec / _msecStartGap null.");
        }
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NotesManager), "StartPlay")]
    private static void StartPlayPostfix()
    {
        ResetAccumulators();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NotesManager), "StopPlay")]
    private static void StopPlayPostfix()
    {
        ResetAccumulators();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NotesManager), "clearNotes")]
    private static void ClearNotesPostfix()
    {
        ResetAccumulators();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NotesManager), "UpdateTimer")]
    private static void UpdateTimerPostfix()
    {
        if (!SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable || !TryResolveFields())
        {
            return;
        }

        try
        {
            float realMsec = (float)CurMSecField.GetValue(null);
            float gap = (float)MsecStartGapField.GetValue(null);
            float speed = SpeedHack.GetCurrentSpeed();

            if (speed == 1.0f)
            {
                ResetAccumulators();
                _lastRealMsec = realMsec;
                return;
            }

            if (float.IsNaN(_lastRealMsec) || realMsec < _lastRealMsec)
            {
                _lastRealMsec = realMsec;
                _timeOffset = 0f;
            }
            else
            {
                float realDelta = realMsec - _lastRealMsec;
                if (realDelta > 0f)
                {
                    _timeOffset += realDelta * (speed - 1f);
                }
                _lastRealMsec = realMsec;
            }

            CurMSecField.SetValue(null, Mathf.Max(gap, realMsec + _timeOffset));
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"SpeedHack apply NotesManager scaled msec failed: {e.Message}");
        }
    }
}

internal static class SpeedHackAudioClockPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundCtrl), "GetPlayTiming")]
    private static void GetPlayTimingPostfix(ref long __result, int playerID)
    {
        if (!SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable) return;

        if (playerID != (int)SoundManager.PlayerID.Music &&
            playerID != (int)SoundManager.PlayerID.Bgm &&
            playerID != (int)SoundManager.PlayerID.Sound_Sepalate_Right)
        {
            return;
        }

        __result = (long)(__result * SpeedHack.GetCurrentSpeed());
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
        float pitchCents = SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable ? SpeedHack.GetPitchCents() : 0f;
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
            ApplyPitch(_activeSoundCtrl, BgmPlayerIds[i], pitchCents, hasChange);
        }

        _lastPitchCents = pitchCents;
    }

    private static bool IsBgmPlayer(int playerId)
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