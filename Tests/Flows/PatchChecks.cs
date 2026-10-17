#if REAL_HARMONY
using System.Reflection;
using HarmonyLib;
using SinmaiAssist.Cheat;
using SinmaiAssist.Runtime;

internal static class PatchChecks
{
    public static void ReadOnlyAudioProbe()
    {
        int count=0;
        foreach(var session in SinmaiAssist.Utils.GameAudioSessions.Open())
        {try {var muted=session.Muted;var process=session.ProcessId;count++;}finally {session.Dispose();}}
        Console.WriteLine("PASS: Windows Core Audio session enumeration and state reads ("+count+" sessions); no audio state was changed.");
    }
    public static void Run()
    {
        var harmony=new Harmony("SinmaiAssist.Offline.PatchChecks");
        try
        {
            PracticeAudio.Installed=false;
            harmony.PatchAll(typeof(PracticeAudio));
            if(Harmony.GetPatchInfo(typeof(CriAtomExPlayer).GetMethod("Start"))?.Prefixes.Any(p=>p.owner==harmony.Id)!=true)
                throw new Exception("PracticeAudio must register before its Installed flag is set.");
            foreach(var type in new[]{typeof(MuteVolumePatch),typeof(MuteResetPatch),typeof(MuteStartPatch),typeof(MuteDisposePatch)})
                harmony.PatchAll(type);
            MuteHookChecks.Run();
            harmony.PatchAll(typeof(PlaySession));
            harmony.PatchAll(typeof(RankAutoPlay));
            RankAutoPlay.Installed=PlaySession.Installed=true;
            AutoPlaySelection.Select("RandomFullComboPlus");AutoPlaySelection.Select("Rank");
            var process=new Process.GameProcess();process.OnStart();
            AutoPlaySelection.Synchronize(0);
            if(!RankAutoPlay.Requested||AutoPlay.autoPlayMode!=AutoPlay.AutoPlayMode.Rank)
                throw new Exception("Actual Harmony OnStart patch must retain rank selection.");
            process.OnRelease();AutoPlaySelection.Select("None");
            Console.WriteLine("PASS: actual Harmony registration and invocation for practice audio, CRI mute, game startup/release and rank scoring.");
        }
        finally {harmony.UnpatchSelf();}
    }
}
#endif
