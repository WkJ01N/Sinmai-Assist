using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Process;
using SinmaiAssist.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SinmaiAssist.Cheat;

internal static class BatchController
{
    public static BatchSettings Settings=new BatchSettings();
    internal static readonly BatchNative Native=new BatchNative();
    internal static readonly BatchRun Run=new BatchRun(Native);
    public static bool Installed;
    public static bool Active=>Run.Active;
    public static string JournalError {get;private set;}
    private static string _directory;
    private static readonly ISerializer Yaml=new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
    public static void Initialize(string directory){_directory=Path.GetFullPath(directory);}
    public static void Start()
    {
        CheckStart();
        Settings.Validate();
        if(File.Exists(Path.Combine(_directory,"BatchProgress.yml")))
        {
            var old=ReadJob();
            if(!old.Finished||BatchRecovery.NeedsReview(old))throw new InvalidOperationException("Previous batch unfinished. Resume/review that job before starting another.");
        }
        var job=new BatchJob{Users=Native.Users,Settings=Settings.Copy(),Songs=BatchNative.BuildQueue(Settings)};
        if(job.Songs.Count==0)throw new InvalidOperationException("No eligible songs in the selected range.");
        Run.Start(job);
    }
    public static void ResumeSaved()
    {
        CheckStart();var job=ReadJob();
        if(job.Finished&&job.Songs.All(s=>s.Done))throw new InvalidOperationException("This job is already finished.");
        if(BatchRecovery.NeedsReview(job))throw new InvalidOperationException("Prior scores or uploads need review. Check server history, then use Reviewed history: resume. Old payloads will not be resent.");
        Run.Start(job);
    }
    public static void ReviewSaved()
    {
        CheckStart();var job=ReadJob();
        BatchRecovery.Review(job,Native.Users,Native.PendingLogs);Run.Start(job);
    }
    private static void CheckStart()
    {
        if(Active)throw new InvalidOperationException("Batch is already active.");
        if(!Installed||!SinmaiAssist.MainConfig.Cheat.FastSkip||SinmaiAssist.MainConfig.ModSetting.SafeMode)
            throw new InvalidOperationException("Enable FastSkip and restart outside safe mode before using Batch.");
        if(PracticeLoop.Session||RankAutoPlay.IsBusy||FastSkip.IsBusy)throw new InvalidOperationException("Finish practice or pending score operations first.");
        if(Native.Scene!=BatchScene.Selection)throw new InvalidOperationException("Open song selection first.");
    }
    private static BatchJob ReadJob()
    {
        var job=new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<BatchJob>(File.ReadAllText(Path.Combine(_directory,"BatchProgress.yml")));
        if(job==null||!Guid.TryParseExact(job.Id,"N",out _)||job.Version!=1||job.Settings==null||job.Songs==null||job.Uploads==null||job.Events==null||
            job.Users?.Length!=2||job.Songs.Any(s=>s==null||s.Charts==null||s.Charts.Any(c=>c==null||c.Difficulty?.Length!=2||c.Goals?.Length!=2||
                c.Difficulty.Where((d,p)=>d>=0&&c.Goals[p]==null).Any())))throw new InvalidOperationException("Unsupported batch progress file.");
        return job;
    }
    public static void Save(BatchJob job)
    {
        if(_directory==null)return;
        try
        {
            AtomicConfigFile.Write(Path.Combine(_directory,"BatchProgress.yml"),Yaml.Serialize(job));
            if(job.Finished)
            {
                var rows=job.Songs.SelectMany(s=>new[]{s.MusicId+" "+s.Name+" ("+s.ScoreType+") "+s.Outcome+(s.Charts.Count==0?" / no selectable difficulty":"")}
                    .Concat(s.Charts.Select(c=>"  ["+string.Join("/",c.Difficulty)+"] "+c.Outcome+" attempts="+c.Attempts+" saved="+c.Saved+" targets="+
                    string.Join(" | ",c.Goals.Select(g=>g?.ToString()??"not entered"))+" "+c.Error)));
                string report=string.Join(Environment.NewLine,rows.Concat(new[]{"","Upload receipts:"}).Concat(job.Uploads.Select(u=>u.Id+" "+u.State+" attempts="+u.TotalAttempts+" "+u.Error))
                    .Concat(new[]{"","Events:"}).Concat(job.Events));
                AtomicConfigFile.Write(Path.Combine(_directory,"BatchReports",job.Id+".txt"),report);
            }
            JournalError=null;
        }
        catch(Exception ex){if(JournalError==null)MelonLogger.Error(ex);JournalError=ex.Message;}
    }
    public static void Tick()
    {
        if(!Active)return;
        if(JournalError!=null){Run.Pause("Batch journal write failed. Fix the folder and retry Save progress: "+JournalError);return;}
        try{Run.Tick();}catch(Exception ex){MelonLogger.Error(ex);Native.Freeze(true);Runtime.Commands.Status="Batch failed: "+ex.Message;}
    }
    public static void RetryJournal(){if(Run.Job!=null)Save(Run.Job);}
    public static void RequireIdle(){if(Active)throw new InvalidOperationException("This operation is unavailable while Batch owns the session.");}

    [HarmonyPostfix,HarmonyPatch(typeof(MusicSelectProcess),"OnStart")]
    private static void SelectStart(MusicSelectProcess __instance){BatchNative.Selection=__instance;}
    [HarmonyPostfix,HarmonyPatch(typeof(MusicSelectProcess),"OnRelease")]
    private static void SelectEnd(MusicSelectProcess __instance){if(BatchNative.Selection==__instance)BatchNative.Selection=null;}
    [HarmonyPrefix,HarmonyPatch(typeof(MusicSelectProcess),"OnUpdate")]
    private static bool SelectUpdate(MusicSelectProcess __instance)
    {
        if(!Active)return true;
        if(BatchMembers.Int(__instance,"_mainSequence")==3)
        {
            ((ProcessDataContainer)BatchMembers.Get(__instance,"container")).processManager.IsTimeCounting(false);
            return false;
        }
        return !Run.Paused;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(MusicSelectProcess),"TimerCountUp")]
    private static bool SelectTimeout()=>!Active;
    [HarmonyPrefix,HarmonyPriority(Priority.First),HarmonyPatch(typeof(GameProcess),"OnUpdate")]
    private static bool GameUpdate()
    {
        if(!Active||!PlaySession.Playing)return true;
        PlaySession.SetPause(true);return false;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(ResultProcess),"OnStart")]
    private static void ResultStart(ResultProcess __instance){BatchNative.Result=__instance;Native.AllowResultLeave=false;}
    [HarmonyPostfix,HarmonyPatch(typeof(ResultProcess),"OnRelease")]
    private static void ResultEnd(ResultProcess __instance){if(BatchNative.Result==__instance)BatchNative.Result=null;}
    [HarmonyPrefix,HarmonyPatch(typeof(ResultProcess),"ToNextCheck")]
    private static bool ResultLeave()=>!Active||Native.AllowResultLeave;
    [HarmonyPrefix,HarmonyPatch(typeof(ResultProcess),"OnUpdate")]
    private static bool ResultUpdate()=>!Active;
    [HarmonyPrefix,HarmonyPatch(typeof(NextTrackProcess),"ProcessingProcess")]
    private static bool NextProcessing()=>!Active||!Run.Paused;
    [HarmonyPrefix,HarmonyPatch(typeof(NextTrackProcess),"OnStart")]
    private static void NextStart()
    {
        if(!Active)return;
        GameManager.NextMapSelect=GameManager.NextCharaSelect=false;
        if(GameManager.IsFreedomMode)GameManager.PauseFreedomModeTimer(true);
    }
    [HarmonyPostfix,HarmonyPatch(typeof(NextTrackProcess),"OnStart")]
    private static void NextReady(NextTrackProcess __instance)
    {
        if(!Active)return;
        try
        {
            if(BatchMembers.Int(__instance,"_mode")!=0)throw new InvalidOperationException("Native flow did not select the next-track path; batch paused.");
            if(!(BatchMembers.Get(__instance,"toProcess") is MusicSelectProcess))
                BatchMembers.Set(__instance,"toProcess",new MusicSelectProcess((ProcessDataContainer)BatchMembers.Get(__instance,"container")));
        }
        catch(Exception ex){Run.Pause(ex.Message);}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(NextTrackProcess),"ProcessingProcess")]
    private static void NextProcessed(NextTrackProcess __instance)
    {
        if(!Active||Run.Paused||Native.ResetDone||!(bool)BatchMembers.Get(__instance,"isAddAnim")||(bool)BatchMembers.Get(__instance,"isAddProcess"))return;
        try
        {
            Native.ResetLogs();
            foreach(var monitor in BatchMembers.Items(BatchMembers.Get(__instance,"_monitors")))BatchMembers.Call(monitor,"SetNextTrack",1u);
            var container=(ProcessDataContainer)BatchMembers.Get(__instance,"container");
            container.processManager.SendMessage(new Message(ProcessType.CommonProcess,20031));
        }
        catch(Exception ex){Run.Pause(ex.Message);}
    }
    [HarmonyPrefix,HarmonyPatch(typeof(RetryWaitProcess),"OnUpdate")]
    private static bool FailedChartReturn(RetryWaitProcess __instance)
    {
        if(!Active||!BatchNative.ReturningWithoutScore)return true;
        if(Run.Paused)return false;
        try
        {
            var game=Singleton<GamePlayManager>.Instance;
            int index=(int)GameManager.MusicTrackNumber-1;
            if(game.GetScoreListCount()!=index+1)throw new InvalidOperationException("Unexpected log layout while abandoning a failed chart.");
            game.RemoveScoreList(index);game.SetQuickRetryFrag(false);
            var container=(ProcessDataContainer)BatchMembers.Get(__instance,"container");
            BatchMembers.Set(__instance,"_state",1);BatchNative.ReturningWithoutScore=false;
            container.processManager.AddProcess(new MusicSelectProcess(container),20);container.processManager.ReleaseProcess(__instance);
        }
        catch(Exception ex){Run.Pause(ex.Message);}
        return false;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(GameManager),"PauseFreedomModeTimer")]
    private static void FreedomPause(ref bool isPause){if(Active&&GameManager.IsFreedomMode)isPause=true;}
}
