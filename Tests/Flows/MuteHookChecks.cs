using MAI2.Util;
using Manager;
using SinmaiAssist.Cheat;
using SinmaiAssist.Runtime;

internal static class MuteHookChecks
{
    private static void Check(bool success,string message){if(!success)throw new Exception("Native mute hook: "+message);}
    public static void Run()
    {
        MuteState.Apply(false,false,false,false,false,false,false);
        AudioMute.SessionSource=()=>Array.Empty<IGameAudioSession>();
        AudioMute.Installed=true;
        AudioMute.Initialize();
        var music=Singleton<SoundCtrl>.Instance.Music;
        music.SetVolume(.4f);music.RawTime=1234;
        MuteState.Set(SoundGroup.Music,true);
        Check(music.Volume==0&&music.RawTime==1234,"existing music becomes silent without changing audio clock");
        music.SetVolume(.6f);music.UpdateAll();
        Check(music.Volume==0,"native volume writes cannot break category mute");
        MuteState.Set(SoundGroup.Music,false);
        Check(music.Volume==.6f,"unmute restores newest native volume");
        MuteState.Set(SoundGroup.Music,true);music.ResetParameters();music.Start();
        Check(music.Volume==0,"reset and later playback remain muted");
        UnityEngine.AudioListener.volume=.3f;MuteState.Master=true;
        Check(UnityEngine.AudioListener.volume==0,"global mute covers Unity audio without pausing its listener");
        var extra=new CriAtomExPlayer();extra.SetVolume(.7f);extra.Start();
        Check(extra.Volume==0,"global mute covers new managed CRI players outside SoundCtrl");
        MuteState.Master=false;
        Check(UnityEngine.AudioListener.volume==.3f&&extra.Volume==.7f,"global release restores Unity and unmanaged-category CRI volumes");
        Check(music.Volume==0,"master toggle retains category mute");
        MuteState.Set(SoundGroup.Music,false);
        Check(music.Volume==1,"reset's latest default volume restored");
        AudioMute.Shutdown();AudioMute.Installed=false;
        Console.WriteLine("PASS: production CRI mute hooks preserve clocks and latest native volume through mute/reset/start/master transitions.");
    }
}
