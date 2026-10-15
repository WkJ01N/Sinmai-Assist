using System;
using System.Collections.Generic;
using HarmonyLib;
using MAI2.Util;
using Manager;

namespace SinmaiAssist.Cheat;

internal static class SpeedHackAudioClockPatch
{
    private static readonly Dictionary<int,RateClock> Clocks=new Dictionary<int,RateClock>();
    internal static double MusicPosition;
    public static void Reset(int playerId,double position=0)
    {
        var clock=new RateClock();
        clock.Reset(0,position,PlaybackRate.ForPlayer(playerId));Clocks[playerId]=clock;
        if(playerId==(int)SoundManager.PlayerID.Music) MusicPosition=position;
    }
    public static void Clear() { Clocks.Clear();MusicPosition=0; }
    public static void Flush()
    {
        foreach(var id in new List<int>(Clocks.Keys)) Singleton<SoundCtrl>.Instance.GetPlayTiming(id);
    }
    [HarmonyPostfix, HarmonyPatch(typeof(SoundCtrl),"GetPlayTiming")]
    private static void GetPlayTimingPostfix(ref long __result,int playerID)
    {
        if(__result<0 || !SpeedHackBgmPitchPatch.IsBgmPlayer(playerID)) return;
        if(!Clocks.TryGetValue(playerID,out var clock))
        { clock=new RateClock();clock.Reset(0,0,PlaybackRate.ForPlayer(playerID));Clocks[playerID]=clock; }
        double position=clock.Sample(__result,PlaybackRate.ForPlayer(playerID));
        if(playerID==(int)SoundManager.PlayerID.Music)MusicPosition=position;
        __result=(long)Math.Round(position,MidpointRounding.AwayFromZero);
    }
}
