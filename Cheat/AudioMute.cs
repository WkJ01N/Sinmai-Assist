using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MAI2.Util;
using Manager;
using SinmaiAssist.Runtime;
using Type = System.Type;

namespace SinmaiAssist.Cheat;

internal static class AudioMute
{
    private sealed class Channel
    {
        public SoundGroup Group;
        public bool Known;
        public MutedVolume Volume;
        public bool Muted=>MuteState.Master||Known&&MuteState.Get(Group);
    }
    private static readonly FieldInfo Players=AccessTools.Field(typeof(SoundCtrl),"_players");
    private static readonly FieldInfo Player=Players==null?null:AccessTools.Field(Players.FieldType.GetGenericArguments()[1],"Player");
    private static readonly Dictionary<CriAtomExPlayer,Channel> Channels=new Dictionary<CriAtomExPlayer,Channel>();
    private static readonly AudioSessionMute Sessions=new AudioSessionMute();
    internal static Func<IEnumerable<IGameAudioSession>> SessionSource=Utils.GameAudioSessions.Open;
    private static readonly uint ProcessId=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
    private static bool _internalWrite,_initialized;
    private static float? _listenerVolume;
    private static double _nextScan;
    public static bool Installed;
    public static string Status { get; private set; }="Audio mute is initializing.";
    public static bool Compatible=>Players!=null&&Player!=null&&
        AccessTools.Method(typeof(CriAtomExPlayer),"SetVolume",new[]{typeof(float)})!=null&&
        AccessTools.Method(typeof(CriAtomExPlayer),"GetParameterFloat32",new[]{typeof(CriAtomEx.Parameter)})?.ReturnType==typeof(float)&&
        AccessTools.Method(typeof(CriAtomExPlayer),"ResetParameters",Type.EmptyTypes)!=null&&
        AccessTools.Method(typeof(CriAtomExPlayer),"Start",Type.EmptyTypes)!=null&&
        AccessTools.Method(typeof(CriAtomExPlayer),"Dispose",Type.EmptyTypes)!=null&&
        AccessTools.Property(typeof(CriAtomExPlayer),"isAvailable")?.PropertyType==typeof(bool);
    public static void Initialize()
    {
        if(_initialized)return;_initialized=true;MuteState.Changed+=Refresh;
        Refresh();
    }
    private static void Discover()
    {
        if(!Installed)return;
        var items=Players.GetValue(Singleton<SoundCtrl>.Instance) as IDictionary;
        if(items==null)return;
        foreach(DictionaryEntry pair in items)
        {
            var player=Player.GetValue(pair.Value) as CriAtomExPlayer;
            if(player==null||!player.isAvailable)continue;
            if(Channels.TryGetValue(player,out var existing)){existing.Known=true;existing.Group=MuteState.Classify((int)pair.Key);continue;}
            Channels.Add(player,new Channel{Known=true,Group=MuteState.Classify((int)pair.Key),Volume=new MutedVolume(player.GetParameterFloat32(CriAtomEx.Parameter.Volume))});
        }
    }
    public static void BeforeVolume(CriAtomExPlayer player,ref float volume)
    {
        if(_internalWrite||!Installed)return;
        if(!Channels.TryGetValue(player,out var channel)){Discover();Channels.TryGetValue(player,out channel);}
        if(channel==null&&MuteState.Master){channel=new Channel{Volume=new MutedVolume(volume)};Channels.Add(player,channel);}
        if(channel!=null)volume=channel.Volume.Write(volume,channel.Muted);
    }
    public static void AfterReset(CriAtomExPlayer player)
    {
        if(!Installed)return;
        if(!Channels.TryGetValue(player,out var channel)){Discover();Channels.TryGetValue(player,out channel);}
        if(channel==null&&MuteState.Master){channel=new Channel{Volume=new MutedVolume(1)};Channels.Add(player,channel);}
        if(channel==null)return;
        channel.Volume.Reset(channel.Muted);
        Apply(player,channel,false);
    }
    public static void BeforeStart(CriAtomExPlayer player)
    {
        if(!Installed)return;Discover();
        if(!Channels.TryGetValue(player,out var channel)&&MuteState.Master)
        {channel=new Channel{Volume=new MutedVolume(player.GetParameterFloat32(CriAtomEx.Parameter.Volume))};Channels.Add(player,channel);}
        if(channel!=null)Apply(player,channel,false);
    }
    public static void Forget(CriAtomExPlayer player)=>Channels.Remove(player);
    private static void Apply(CriAtomExPlayer player,Channel channel,bool update)
    {
        if(!player.isAvailable)return;
        _internalWrite=true;
        try {player.SetVolume(channel.Volume.Output(channel.Muted));if(update)player.UpdateAll();}
        finally {_internalWrite=false;}
    }
    public static void Refresh()
    {
        try
        {
            Discover();
            foreach(var pair in Channels)Apply(pair.Key,pair.Value,true);
            if(MuteState.Master)
            {
                float current=UnityEngine.AudioListener.volume;
                if(!_listenerVolume.HasValue||current!=0)_listenerVolume=current;
                UnityEngine.AudioListener.volume=0;
            }
            else RestoreListener();
            ScanSessions();
        }
        catch(Exception ex)
        {
            string error="Audio mute failed: "+ex.Message;
            if(Status!=error)MelonLoader.MelonLogger.Warning(error);Status=error;
        }
    }
    private static void ScanSessions()
    {
        if(MuteState.Master||Sessions.NeedsRestore)
            Sessions.Update(MuteState.Master,ProcessId,SessionSource());
        Status=!Installed?"CRI mute hooks unavailable; category mute is unavailable.":
            Sessions.Error??(MuteState.Master?(Sessions.Matched>0?"Whole game muted.":"CRI muted; waiting for the game's Windows audio session."):"Category mute settings applied.");
    }
    public static void Tick(double now)
    {
        if(now<_nextScan)return;_nextScan=now+1;Refresh();
    }
    private static void RestoreListener()
    {
        if(!_listenerVolume.HasValue)return;
        if(UnityEngine.AudioListener.volume==0)UnityEngine.AudioListener.volume=_listenerVolume.Value;
        _listenerVolume=null;
    }
    public static void Shutdown()
    {
        MuteState.Changed-=Refresh;
        try
        {
            _internalWrite=true;
            foreach(var pair in Channels)if(pair.Key.isAvailable)
                try {pair.Key.SetVolume(pair.Value.Volume.Intended);pair.Key.UpdateAll();}
                catch(Exception ex){MelonLoader.MelonLogger.Warning("CRI volume restoration: "+ex.Message);}
            RestoreListener();
        }
        catch(Exception ex){MelonLoader.MelonLogger.Warning("Audio mute restoration: "+ex.Message);}
        finally
        {
            _internalWrite=false;Channels.Clear();
            try
            {
                if(Sessions.NeedsRestore)Sessions.Update(false,ProcessId,SessionSource());
                if(Sessions.Error!=null)MelonLoader.MelonLogger.Warning(Sessions.Error);
            }
            catch(Exception ex){MelonLoader.MelonLogger.Warning("Windows audio restoration: "+ex.Message);}
            _initialized=false;
        }
    }
}

[HarmonyPatch(typeof(CriAtomExPlayer),"SetVolume")]
internal static class MuteVolumePatch
{
    [HarmonyPrefix] private static void Write(CriAtomExPlayer __instance,ref float volume)=>AudioMute.BeforeVolume(__instance,ref volume);
}
[HarmonyPatch(typeof(CriAtomExPlayer),"ResetParameters")]
internal static class MuteResetPatch
{
    [HarmonyPostfix] private static void Reset(CriAtomExPlayer __instance)=>AudioMute.AfterReset(__instance);
}
[HarmonyPatch(typeof(CriAtomExPlayer),"Start")]
internal static class MuteStartPatch
{
    [HarmonyPrefix] private static void Start(CriAtomExPlayer __instance)=>AudioMute.BeforeStart(__instance);
}
[HarmonyPatch(typeof(CriAtomExPlayer),"Dispose")]
internal static class MuteDisposePatch
{
    [HarmonyPrefix] private static void Release(CriAtomExPlayer __instance)=>AudioMute.Forget(__instance);
}
