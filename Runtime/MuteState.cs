using System;

namespace SinmaiAssist.Runtime;

internal enum SoundGroup { Music,MenuMusic,GameEffects,MenuEffects,Voices,Notifications }

internal static class MuteState
{
    private static bool _master;
    private static readonly bool[] Groups=new bool[6];
    public static event Action Changed;
    public static bool Master { get=>_master; set {if(_master==value)return;_master=value;Changed?.Invoke();} }
    public static bool Get(SoundGroup group)=>Groups[(int)group];
    public static void Set(SoundGroup group,bool value)
    {if(Groups[(int)group]==value)return;Groups[(int)group]=value;Changed?.Invoke();}
    public static bool IsMuted(SoundGroup group)=>Master||Get(group);
    public static void Apply(bool master,params bool[] groups)
    {
        if(groups.Length!=Groups.Length)throw new ArgumentException("Expected six mute categories.");
        bool changed=_master!=master;_master=master;
        for(int i=0;i<Groups.Length;i++){changed|=Groups[i]!=groups[i];Groups[i]=groups[i];}
        if(changed)Changed?.Invoke();
    }
    public static SoundGroup Classify(int player)
    {
        // The right monitor repeats the 7..40 range at an offset of 35.
        if(player>=42&&player<=75)player-=35;
        if(player==2)return SoundGroup.Music;
        if(player==3||player==7)return SoundGroup.MenuMusic;
        if(player>=4&&player<=6||player==8||player==9)return SoundGroup.Voices;
        if(player>=11&&player<=16)return SoundGroup.MenuEffects;
        if(player>=17&&player<=40)return SoundGroup.GameEffects;
        return SoundGroup.Notifications;
    }
}

// Keep the game's desired volume separate from our temporary output override.
internal sealed class MutedVolume
{
    public float Intended { get; private set; }
    public MutedVolume(float volume){Intended=volume;}
    public float Write(float volume,bool muted){Intended=volume;return Output(muted);}
    public float Reset(bool muted)=>Write(1f,muted);
    public float Output(bool muted)=>muted?0f:Intended;
}
