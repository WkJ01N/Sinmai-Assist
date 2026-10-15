using System;
using System.Reflection;
using HarmonyLib;
using Manager;

namespace SinmaiAssist.Cheat;

internal static class PlaybackRate
{
    public static float Effective => SinmaiAssist.MainConfig.ModSetting.SafeMode ? 1 :
        PracticeLoop.Session ? PracticeLoop.ActiveSpeed :
        SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable ? SpeedHack.GetCurrentSpeed() : 1;
    public static float MusicPitch => PracticeLoop.Session && PracticeLoop.ActivePreservePitch ? 0 :
        (float)(1200 * Math.Log(Effective,2));
    public static float ForPlayer(int id) => id==(int)SoundManager.PlayerID.Music ? Effective :
        SinmaiAssist.MainConfig.ModSetting.SafeMode || !SinmaiAssist.MainConfig.Cheat.SpeedHack.Enable ? 1 : SpeedHack.GetCurrentSpeed();
    public static void Refresh()
    {
        if (PlaySession.Owner != null) NotesManager.UpdateTimer();
        SpeedHackBgmPitchPatch.UpdateActiveBgmPitch(MusicPitch);
    }
}

internal static class SpeedHackNotesClockPatch
{
    private static readonly FieldInfo Current = AccessTools.Field(typeof(NotesManager),"_curMSec");
    private static readonly FieldInfo Gap = AccessTools.Field(typeof(NotesManager),"_msecStartGap");
    internal static readonly RateClock Clock = new RateClock();
    public static bool Compatible => Current?.FieldType==typeof(float) && Gap?.FieldType==typeof(float);

    [HarmonyPostfix, HarmonyPatch(typeof(NotesManager),"StartPlay")]
    private static void StartPlayPostfix(float msecStartGap) => Clock.Reset(msecStartGap,msecStartGap,PlaybackRate.Effective);
    [HarmonyPostfix, HarmonyPatch(typeof(NotesManager),"StopPlay")]
    private static void StopPlayPostfix() => Clock.Clear();
    [HarmonyPostfix, HarmonyPatch(typeof(NotesManager),"clearNotes")]
    private static void ClearNotesPostfix() => Clock.Clear();
    [HarmonyPostfix, HarmonyPatch(typeof(NotesManager),"UpdateTimer")]
    private static void UpdateTimerPostfix()
    {
        if (!Compatible) return;
        double raw=(float)Current.GetValue(null);
        if (!Clock.Initialized) Clock.Reset((float)Gap.GetValue(null),(float)Gap.GetValue(null),PlaybackRate.Effective);
        Current.SetValue(null,(float)Clock.Sample(raw,PlaybackRate.Effective));
    }
}
