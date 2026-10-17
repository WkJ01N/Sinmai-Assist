using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MAI2.Util;
using Manager;

namespace SinmaiAssist.Cheat;

// Only the game's Music player is routed to our pool. Never attach DSP to the
// shared native pools, which also serve judgements, voices and other mods.
internal static class PracticeAudio
{
    private const uint PoolId=0x574B0101;
    private static readonly System.Type PlayerType=AccessTools.TypeByName("CriAtomExPlayer");
    private static readonly System.Type PoolType=AccessTools.TypeByName("CriAtomExStandardVoicePool");
    private static readonly FieldInfo Players=AccessTools.Field(typeof(SoundCtrl),"_players");
    private static readonly FieldInfo PlayerField=Players?.FieldType.IsGenericType==true ?
        AccessTools.Field(Players.FieldType.GetGenericArguments()[1],"Player") : null;
    private static readonly MethodInfo Route=PlayerType==null?null:AccessTools.Method(PlayerType,"SetVoicePoolIdentifier",new[]{typeof(uint)});
    private static readonly MethodInfo Pitch=PlayerType==null?null:AccessTools.Method(PlayerType,"SetPitch",new[]{typeof(float)});
    private static readonly MethodInfo Stretch=PlayerType==null?null:AccessTools.Method(PlayerType,"SetDspTimeStretchRatio",new[]{typeof(float)});
    private static readonly MethodInfo UpdateAll=PlayerType==null?null:AccessTools.Method(PlayerType,"UpdateAll",System.Type.EmptyTypes);
    private static readonly MethodInfo StartMethod=PlayerType==null?null:AccessTools.Method(PlayerType,"Start",System.Type.EmptyTypes);
    private static readonly MethodInfo Attach=PoolType==null?null:AccessTools.Method(PoolType,"AttachDspTimeStretch",System.Type.EmptyTypes);
    private static readonly ConstructorInfo Create=PoolType?.GetConstructor(new[]{typeof(int),typeof(int),typeof(int),typeof(bool),typeof(uint)});
    private static object _pool;
    public static bool Installed;
    public static bool Compatible => Players!=null && PlayerField!=null && Route!=null && Pitch!=null && UpdateAll!=null && StartMethod!=null;
    public static bool CanPreservePitch => Compatible && Stretch!=null && Attach!=null && Create!=null && typeof(IDisposable).IsAssignableFrom(PoolType);
    internal static object MusicPlayer
    {
        get
        {
            var players=Players?.GetValue(Singleton<SoundCtrl>.Instance) as IDictionary;
            var item=players?[(int)SoundManager.PlayerID.Music];
            return item==null?null:PlayerField.GetValue(item);
        }
    }
    public static void PreparePracticeMusic(bool preservePitch)
    {
        if(!Installed || !Compatible || MusicPlayer==null) throw new InvalidOperationException("Practice music speed interfaces unavailable.");
        if(!preservePitch) return;
        if(!CanPreservePitch) throw new InvalidOperationException("Time stretch unavailable. Disable Preserve pitch and start practice again.");
        if(_pool!=null)return;
        object pool=null;
        try
        {
            // Four streaming stereo voices cover overlap with a retiring voice.
            // 192 kHz covers 48 kHz audio, 2x pitch and the DSP's extra headroom.
            pool=Create.Invoke(new object[]{4,2,192000,true,PoolId});
            Attach.Invoke(pool,null);
            _pool=pool;
        }
        catch(Exception ex)
        {
            (pool as IDisposable)?.Dispose();
            throw new InvalidOperationException("Time stretch preparation failed. Disable Preserve pitch and start practice again.",ex);
        }
    }
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()=>StartMethod;
    [HarmonyPrefix]
    private static void BeforeStart(object __instance)
    {
        if(!PracticeLoop.Session || !PlaySession.Playing || !ReferenceEquals(__instance,MusicPlayer))return;
        // SoundCtrl.Play has already ResetParameters and set the cue/start time.
        bool preserve=PracticeLoop.ActivePreservePitch;
        if(preserve && _pool==null)throw new InvalidOperationException("Practice stretch pool is not ready.");
        Route.Invoke(__instance,new object[]{preserve?PoolId:0u});
        Pitch.Invoke(__instance,new object[]{PlaybackRate.MusicPitch});
        if(preserve)Stretch.Invoke(__instance,new object[]{1f/PracticeLoop.ActiveSpeed});
    }
    public static void Release()
    {
        if(_pool==null)return;
        // Call only after the native music voice has stopped (release/retry/quit).
        Singleton<SoundCtrl>.Instance.Stop((int)SoundManager.PlayerID.Music,true);
        var player=MusicPlayer;
        if(player!=null)Route.Invoke(player,new object[]{0u});
        (_pool as IDisposable)?.Dispose();_pool=null;
    }
}
