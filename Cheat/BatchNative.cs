using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Process;
using SinmaiAssist.Config;

namespace SinmaiAssist.Cheat;

internal static class BatchMembers
{
    private static readonly Dictionary<(System.Type,string),MemberInfo> Members=new Dictionary<(System.Type,string),MemberInfo>();
    private static readonly Dictionary<(System.Type,string,int),MethodInfo> Methods=new Dictionary<(System.Type,string,int),MethodInfo>();
    public static object Get(object obj,string name)
    {
        if(obj==null)throw new InvalidOperationException("Not initialized: "+name);
        var type=obj as System.Type??obj.GetType();var key=(type,name);
        if(!Members.TryGetValue(key,out var member))Members[key]=member=(MemberInfo)AccessTools.Field(type,name)??AccessTools.Property(type,name);
        if(member is FieldInfo f)return f.GetValue(obj is System.Type?null:obj);
        if(member is PropertyInfo p)return p.GetValue(obj is System.Type?null:obj);
        throw new MissingMemberException(type.FullName,name);
    }
    public static void Set(object obj,string name,object value)
    {
        Get(obj,name);var type=obj as System.Type??obj.GetType();var member=Members[(type,name)];
        System.Type target=member is FieldInfo f?f.FieldType:((PropertyInfo)member).PropertyType;
        if(target.IsEnum)value=Enum.ToObject(target,value);
        else if(value!=null&&!target.IsInstanceOfType(value))value=Convert.ChangeType(value,target);
        if(member is FieldInfo field)field.SetValue(obj is System.Type?null:obj,value);
        else ((PropertyInfo)member).SetValue(obj is System.Type?null:obj,value);
    }
    public static MethodInfo Method(System.Type type,string name,int count)
    {
        var key=(type,name,count);
        if(!Methods.TryGetValue(key,out var method))
        {
            for(var current=type;current!=null;current=current.BaseType)
            {
                method=current.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m=>m.Name==name&&m.GetParameters().Length==count);
                if(method!=null)break;
            }
            Methods[key]=method;
        }
        return method??throw new MissingMethodException(type.FullName,name);
    }
    public static object Call(object obj,string name,params object[] args)
    {
        var type=obj as System.Type??obj.GetType();var method=Method(type,name,args.Length);
        var parameters=method.GetParameters();
        for(int i=0;i<args.Length;i++)if(args[i]!=null&&!parameters[i].ParameterType.IsInstanceOfType(args[i]))
            args[i]=parameters[i].ParameterType.IsEnum?Enum.ToObject(parameters[i].ParameterType,args[i]):Convert.ChangeType(args[i],parameters[i].ParameterType);
        try{return method.Invoke(obj is System.Type?null:obj,args);}
        catch(TargetInvocationException ex){throw ex.InnerException??ex;}
    }
    public static int Int(object obj,string name)=>Convert.ToInt32(Get(obj,name));
    public static IEnumerable<object> Items(object collection)=>((IEnumerable)collection).Cast<object>();
    public static void Require(System.Type type,params string[] names)
    {
        foreach(string name in names)
            if(AccessTools.Field(type,name)==null&&AccessTools.Property(type,name)==null)throw new MissingMemberException(type.FullName,name);
    }
}

internal sealed class BatchNative : IBatchRuntime
{
    internal static MusicSelectProcess Selection;
    internal static ResultProcess Result;
    internal static bool ReturningWithoutScore;
    internal bool AllowResultLeave;
    private BatchJob _job;
    private uint _originalLimit;
    private bool _originalMap,_originalCharacter,_originalFreedomPause;
    private bool _resetRequested;
    private int _uploadCount;
    private object[] _uploadIdentity;
    private string _savedMode;
    private ProcessDataContainer _container;
    private readonly HashSet<object> _background=new HashSet<object>();
    private static readonly HashSet<string> BatchProcesses=new HashSet<string>{"Process.MusicSelectProcess","Process.GameProcess","Process.ResultProcess",
        "Process.NextTrackProcess","Process.RetryWaitProcess","Process.UnlockProcess","Process.TrackStartProcess","Process.MovieTrackStart.MovieTrackStartProcess"};
    public bool Frozen;
    public double Now=>UnityEngine.Time.realtimeSinceStartup;
    public BatchScene Scene
    {
        get
        {
            if(BatchController.Active&&_container!=null)
            foreach(var control in BatchMembers.Items(BatchMembers.Get(_container.processManager,"_processList")))
            {
                if(BatchMembers.Int(control,"State")>=2)continue;
                var process=BatchMembers.Get(control,"Process");
                if(!_background.Contains(process)&&!(process is FadeProcess)&&!BatchProcesses.Contains(process.GetType().FullName))return BatchScene.Unknown;
            }
            if(Selection!=null && BatchMembers.Int(Selection,"_mainSequence")==3)return BatchScene.Selection;
            if(Result!=null)return BatchScene.Result;
            if(PlaySession.Owner!=null)return PlaySession.Playing?BatchScene.Playing:BatchScene.Opening;
            return BatchScene.Transition;
        }
    }
    public string[] Users => Enumerable.Range(0,2).Select(p=> {
        var user=Singleton<UserDataManager>.Instance.GetUserData(p);
        return user.IsEntry?((bool)BatchMembers.Call(user,"IsAimeUser")?"":"Inactive:")+user.Detail.UserID:null;
    }).ToArray();
    public int PendingLogs=>Singleton<GamePlayManager>.Instance.GetScoreListCount();
    public int Limit {get;private set;}
    public bool ResetDone=>!_resetRequested;
    public static string Check()
    {
        try
        {
            foreach(var requirement in new[]{(typeof(MusicSelectProcess),"OnGameStart",0),(typeof(ResultProcess),"ToNextCheck",0),
                (typeof(NextTrackProcess),"ProcessingProcess",0),(typeof(GamePlayManager),"RemoveScoreList",1),
                (typeof(GamePlayManager),"GetScoreListCount",0),(typeof(GamePlayManager),"ClaerLog",0),
                (typeof(MusicSelectProcess),"GetCurrentDifficulty",1),(typeof(MusicSelectProcess),"GetUserScore",3),
                (typeof(MusicSelectProcess),"CalcMonitorDifficulty",1),(typeof(MusicSelectProcess),"IsPlayableMusic",1),
                (typeof(MusicSelectProcess),"TimerCountUp",0),(typeof(MusicSelectProcess),"IsTournamentFolder",1),
                (typeof(Monitor.NextTrackMonitor),"SetNextTrack",1),(typeof(Monitor.MusicSelectMonitor),"SetDeployList",1),
                (typeof(Manager.UserData),"IsAimeUser",0),(typeof(UnlockProcess),"OnUpdate",0)})
                BatchMembers.Method(requirement.Item1,requirement.Item2,requirement.Item3);
            BatchMembers.Require(typeof(MusicSelectProcess),"_mainSequence","CombineMusicDataList","CategoryNameList","CurrentCategorySelect","CurrentMusicSelect","ScoreType","DifficultySelectIndex","CurrentDifficulty","MonitorArray","TrialPlayIndex");
            BatchMembers.Require(typeof(NextTrackProcess),"isAddAnim","isAddProcess","_monitors","container","toProcess","_mode");
            BatchMembers.Require(typeof(RetryWaitProcess),"_state","container");
            BatchMembers.Require(typeof(Manager.ProcessManager),"_processList");
            BatchMembers.Require(AccessTools.Inner(typeof(Manager.ProcessManager),"ProcessControle"),"State","Process");
            BatchMembers.Require(typeof(UnlockProcess),"_state");
            BatchMembers.Require(typeof(GameManager),"TempMaxTrackCount","MusicTrackNumber","NextMapSelect","NextCharaSelect","IsFreedomTimerPause");
            if(!ScorePlanAdapter.IsCompatible||!PlaySession.Installed)return "Scoring or timeline module unavailable.";
            BatchPackets.Check();return null;
        }
        catch(Exception ex){return "Batch interfaces unavailable: "+ex.Message;}
    }
    public static void CheckPlayers()
    {
        if(GameManager.IsCourseMode||GameManager.IsKaleidxScopeMode||GameManager.IsTutorial||GameManager.IsEventMode||GameManager.IsNoteCheckMode)
            throw new InvalidOperationException("Batch supports ordinary logged-in play, not special/event modes.");
        if(Manager.Party.Party.Party.Get()?.IsJoinAndActive()==true)throw new InvalidOperationException("Cross-machine party play is unsupported.");
        bool entered=false;
        for(int p=0;p<2;p++)
        {
            var user=Singleton<UserDataManager>.Instance.GetUserData(p);if(!user.IsEntry)continue;entered=true;
            if(!user.IsHuman()||user.IsGuest()||user.IsTrial()||!(bool)BatchMembers.Call(user,"IsAimeUser")||user.Detail.UserID==0)throw new InvalidOperationException("Every entered player must be logged in and able to save scores.");
        }
        if(!entered)throw new InvalidOperationException("No entered player.");
    }
    public static List<BatchSong> BuildQueue(BatchSettings settings)
    {
        if(Selection==null||BatchMembers.Int(Selection,"_mainSequence")!=3)throw new InvalidOperationException("Open the normal song list first.");
        CheckPlayers();
        var categories=new List<List<BatchEntry>>();
        var lists=BatchMembers.Items(BatchMembers.Get(Selection,"CombineMusicDataList")).ToArray();
        var names=BatchMembers.Items(BatchMembers.Get(Selection,"CategoryNameList")).Select(n=>n.ToString()).ToArray();
        int selectedCategory=BatchMembers.Int(Selection,"CurrentCategorySelect");
        if(!AllowedCategory(names[selectedCategory]))throw new InvalidOperationException("Batch requires an ordinary song category; ghost, challenge, task, connection and special folders are unsupported.");
        int scoreType=BatchMembers.Int(Selection,"ScoreType");
        int[] difficulty=Enumerable.Range(0,2).Select(p=>(int)BatchMembers.Call(Selection,"GetCurrentDifficulty",p)).ToArray();
        for(int c=0;c<lists.Length;c++)
        {
            var entries=new List<BatchEntry>();categories.Add(entries);
            if(!AllowedCategory(names[c]))continue;
            foreach(var combined in BatchMembers.Items(lists[c]))
            {
                var entry=new BatchEntry{Category=names[c]};entries.Add(entry);
                var data=((IList)BatchMembers.Get(combined,"musicSelectData"))[scoreType];
                if(data==null)continue;
                var music=BatchMembers.Get(data,"MusicData");int id=(int)BatchMembers.Call(music,"GetID");
                entry.MusicId=id;entry.ScoreType=scoreType;entry.Name=BatchMembers.Get(BatchMembers.Get(music,"name"),"str").ToString();
                if(id<=3||id>=100000)continue;
                var available=(bool[])BatchMembers.Call(Selection,"IsPlayableMusic",id);
                var notes=((IList)BatchMembers.Get(data,"ScoreData"));
                int entryDifficulty=BatchMembers.Int(data,"Difficulty");
                bool levelFolder=entryDifficulty>=0; // ordinary entries use -1; level/rating entries carry a chart difficulty.
                IEnumerable<int[]> charts=settings.AllDifficulties?Enumerable.Range(0,5).Select(d=>new[]{d,d}):new[]{levelFolder?new[]{entryDifficulty,entryDifficulty}:difficulty.ToArray()};
                foreach(var pair in charts)
                {
                    var levels=new string[2];bool valid=true;
                    for(int p=0;p<2;p++)
                    {
                        if(!Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry){pair[p]=-1;continue;}
                        int d=pair[p];
                        if(d<0||d>=5||d>=available.Length||!available[d]||d>=notes.Count||BatchMembers.Int(notes[d],"maxNotes")<=0){valid=false;break;}
                        int levelId=BatchMembers.Int(notes[d],"musicLevelID");
                        string enumName=Enum.GetName(typeof(MusicLevelID),levelId)??"";
                        levels[p]=enumName.StartsWith("Level")?enumName.Substring(5).Replace("P","+"):"";
                    }
                    if(valid)entry.Charts.Add(new BatchChart{Difficulty=pair,Levels=levels});
                }
            }
        }
        // Keep native row positions for the initial cursor; the pure builder filters unsupported rows.
        int category=BatchMembers.Int(Selection,"CurrentCategorySelect");int row=BatchMembers.Int(Selection,"CurrentMusicSelect");
        return BatchPolicy.Queue(categories,category,row,settings,Guid.NewGuid().GetHashCode());
    }
    private static bool AllowedCategory(string name)
    {
        foreach(int id in new[]{12,195,196,198,107,300})
        {
            var genre=Singleton<DataManager>.Instance.GetMusicGenre(id);
            if(genre!=null&&!string.IsNullOrEmpty(genre.genreName)&&name.Contains(genre.genreName))return false;
        }
        return !(bool)AccessTools.Method(typeof(MusicSelectProcess),"IsTournamentFolder",new[]{typeof(string)}).Invoke(Selection,new object[]{name});
    }
    public void Begin(BatchJob job)
    {
        CheckPlayers();string error=Check();if(error!=null)throw new InvalidOperationException(error);
        EnsureUploaderExclusive();
        _originalLimit=GameManager.TempMaxTrackCount;Limit=(int)GameManager.GetMaxTrackCount();
        if(Limit<=0||Limit>1000)throw new InvalidOperationException("Unsupported native track limit.");
        if(GameManager.MusicTrackNumber!=PendingLogs+1)throw new InvalidOperationException("Track number and pending native logs are inconsistent; refusing to start.");
        _job=job;_savedMode=Runtime.SettingsStore.PreferredMode??AutoPlay.autoPlayMode.ToString();
        _container=(ProcessDataContainer)BatchMembers.Get(Selection,"container");_background.Clear();
        foreach(var control in BatchMembers.Items(BatchMembers.Get(_container.processManager,"_processList")))
        {
            var process=BatchMembers.Get(control,"Process");if(!ReferenceEquals(process,Selection))_background.Add(process);
        }
        RankAutoPlay.Cancel(true);FastSkip.CancelPending();
        _originalMap=GameManager.NextMapSelect;_originalCharacter=GameManager.NextCharaSelect;
        _originalFreedomPause=GameManager.IsFreedomTimerPause;
        if(!GameManager.IsFreedomMode)GameManager.TempMaxTrackCount=(uint)Limit+1;
        else GameManager.PauseFreedomModeTimer(true);
        GameManager.NextMapSelect=GameManager.NextCharaSelect=false;AllowResultLeave=false;_resetRequested=false;Frozen=false;
        var manager=((ProcessDataContainer)BatchMembers.Get(Selection,"container")).processManager;
        manager.IsTimeCounting(false);manager.SendMessage(new Message(ProcessType.CommonProcess,20031));
    }
    private static void EnsureUploaderExclusive()
    {
        foreach(var target in new[]{AccessTools.Method(typeof(ResultProcess),"OnStart"),AccessTools.Method(typeof(GameProcess),"OnRelease")})
        {
            var info=HarmonyLib.Harmony.GetPatchInfo(target);if(info==null)continue;
            foreach(var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
            {
                var method=patch.PatchMethod;if(method.DeclaringType?.Assembly==typeof(BatchNative).Assembly)continue;
                string name=(method.DeclaringType?.FullName??"")+"."+method.Name;
                if(name.IndexOf("ImmediateSave",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("AutoSave",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Upload",StringComparison.OrdinalIgnoreCase)>=0)
                    throw new InvalidOperationException("Another score uploader is active: "+name+". Disable it before using Batch; external configs were not changed.");
            }
        }
    }
    public bool AlreadyMet(BatchSong song,BatchChart chart)
    {
        for(int p=0;p<2;p++)
        {
            if(chart.Difficulty[p]<0)continue;
            var score=BatchMembers.Call(Selection,"GetUserScore",p,chart.Difficulty[p],song.MusicId);
            if(score==null)return false;
            var goal=chart.Goals[p];
            decimal achievement=Convert.ToDecimal(BatchMembers.Get(score,"achivement"))/10000m;
            Bounds(goal.Rank,out decimal lower,out _,out _);
            if(achievement<lower||BatchMembers.Int(score,"combo")<(int)goal.Minimum)return false;
        }
        return true;
    }
    internal static void Bounds(string rank,out decimal lower,out decimal upper,out bool highest)
    {
        var id=(MusicClearrankID)Enum.Parse(typeof(MusicClearrankID),"Rank_"+rank.Replace("+","P"));
        lower=GameManager.ConvAchiveIntToDecimal(id.GetAchvement());
        highest=rank=="SSS+";
        upper=highest?101m:GameManager.ConvAchiveIntToDecimal(((MusicClearrankID)((int)id+1)).GetAchvement());
    }
    public void Open(BatchSong song,BatchChart chart)
    {
        int scoreType=song.ScoreType;BatchMembers.Set(Selection,"ScoreType",scoreType);
        var categories=BatchMembers.Items(BatchMembers.Get(Selection,"CombineMusicDataList")).ToArray();
        var names=BatchMembers.Items(BatchMembers.Get(Selection,"CategoryNameList")).Select(n=>n.ToString()).ToArray();
        var matches=new List<(int,int)>();
        for(int c=0;c<categories.Length;c++)
        {
            int row=0;
            foreach(var combined in BatchMembers.Items(categories[c]))
            {
                if(AllowedCategory(names[c])&&Convert.ToInt32(BatchMembers.Call(combined,"GetID",scoreType))==song.MusicId)matches.Add((c,row));
                row++;
            }
        }
        if(matches.Count==0)throw new BatchChartException("Song no longer exists in the current selection lists.");
        var position=matches.OrderBy(m=>names[m.Item1]==song.Category?0:1).First();
        BatchMembers.Set(Selection,"CurrentCategorySelect",position.Item1);BatchMembers.Set(Selection,"CurrentMusicSelect",position.Item2);
        var available=(bool[])BatchMembers.Call(Selection,"IsPlayableMusic",song.MusicId);
        var indexes=(int[])BatchMembers.Get(Selection,"DifficultySelectIndex");
        var current=(Array)BatchMembers.Get(Selection,"CurrentDifficulty");
        for(int p=0;p<2;p++)if(chart.Difficulty[p]>=0)
        {
            int difficulty=chart.Difficulty[p];
            if(!available[difficulty])throw new BatchChartException("Requested difficulty is no longer selectable.");
            indexes[p]=difficulty;current.SetValue(Enum.ToObject(current.GetType().GetElementType(),difficulty),p);
        }
        BatchMembers.Call(Selection,"CalcMonitorDifficulty",false);
        foreach(var monitor in BatchMembers.Items(BatchMembers.Get(Selection,"MonitorArray")))BatchMembers.Call(monitor,"SetDeployList",false);
        BatchMembers.Set(Selection,"TrialPlayIndex",0);
        BatchMembers.Call(Selection,"OnGameStart");
        for(int p=0;p<2;p++)if(chart.Difficulty[p]>=0 && (GameManager.SelectMusicID[p]!=song.MusicId||GameManager.SelectDifficultyID[p]!=chart.Difficulty[p]))
            throw new InvalidOperationException("Native selection changed the requested chart; batch paused.");
    }
    public Task<object> Generate(BatchSong song,BatchChart chart,CancellationToken token)
    {
        if(!ScorePlanAdapter.CanGenerate(PlaySession.IsParty,out var error))throw new InvalidOperationException(error);
        var plans=ScorePlanAdapter.Capture(FastSkip.SkipMode.Random,0,false,chart.Goals.First(g=>g!=null).Rank,MinimumCombo.None);
        foreach(var plan in plans)
        {
            if(plan.Score.SessionInfo.musicId!=song.MusicId||plan.Score.SessionInfo.difficulty!=chart.Difficulty[plan.Player])throw new InvalidOperationException("Loaded chart differs from batch task.");
            var goal=chart.Goals[plan.Player];Bounds(goal.Rank,out var low,out var high,out var top);
            plan.Request.Lower=low;plan.Request.Upper=high;plan.Request.IncludeUpper=top;
            plan.Request.Minimum=goal.Minimum;plan.Request.ExcludeTheory=!goal.Theory;
        }
        return Task.Run<object>(()=>{
            foreach(var plan in plans)
            {plan.Result=ScorePlanner.Generate(plan.Request,token);if(!plan.Result.Success)throw new BatchChartException(plan.Result.Error);}
            return plans;
        },token);
    }
    public void Commit(object result)
    {
        var plans=(ScorePlanAdapter.Plan[])result;
        ScorePlanAdapter.Apply(plans);FastSkip.CompleteForBatch(PlaySession.Owner);
    }
    public void VerifyResult(BatchSong song,BatchChart chart)
    {
        for(int p=0;p<2;p++)if(chart.Difficulty[p]>=0)
        {
            var score=Singleton<GamePlayManager>.Instance.GetGameScore(p);var goal=chart.Goals[p];
            Bounds(goal.Rank,out var low,out var high,out bool top);decimal achievement=score.GetAchivement();
            if(score.SessionInfo.musicId!=song.MusicId||score.SessionInfo.difficulty!=chart.Difficulty[p]||
                achievement<low||(top?achievement>high:achievement>=high)||(int)score.ComboType<(int)goal.Minimum||
                (!goal.Theory&&(int)score.ComboType==4))
                throw new InvalidOperationException("Native result differs from batch target for P"+(p+1)+"; check other score mods before continuing.");
        }
    }
    public void Abandon()
    {ReturningWithoutScore=true;PlaySession.SetPause(true);PlaySession.Retry();}
    public void LeaveResult(){AllowResultLeave=true;BatchMembers.Call(Result,"ToNextCheck");}
    public BatchUpload CaptureUpload()
    {
        if(_job==null)throw new InvalidOperationException("No active batch.");
        EnsureUploaderExclusive();
        _uploadCount=PendingLogs;
        _uploadIdentity=Enumerable.Range(0,_uploadCount).Select(i=>(object)Singleton<GamePlayManager>.Instance.GetGameScore(0,i)).ToArray();
        return BatchPackets.Capture(_job.Id,_job.Uploads.Count,_uploadCount,_job.Users);
    }
    public void RequestReset()
    {
        _resetRequested=true;
        if(Scene==BatchScene.Selection)ResetLogs();
    }
    internal void ResetLogs()
    {
        if(!_resetRequested)return;
        var game=Singleton<GamePlayManager>.Instance;
        if(PendingLogs!=_uploadCount || Enumerable.Range(0,_uploadCount).Any(i=>!ReferenceEquals(_uploadIdentity[i],game.GetGameScore(0,i))))
            throw new InvalidOperationException("Play logs changed after upload; refusing to clear them.");
        game.ClaerLog();GameManager.MusicTrackNumber=1;
        _resetRequested=false;AllowResultLeave=false;
    }
    public void Freeze(bool freeze){Frozen=freeze;if(PlaySession.Playing)PlaySession.SetPause(freeze);}
    public void End()
    {
        if(PendingLogs!=0)throw new InvalidOperationException("Unconfirmed logs remain; cannot finish the batch.");
        GameManager.TempMaxTrackCount=_originalLimit;GameManager.NextMapSelect=_originalMap;GameManager.NextCharaSelect=_originalCharacter;
        if(GameManager.IsFreedomMode)GameManager.PauseFreedomModeTimer(_originalFreedomPause);
        Frozen=false;GameManager.MusicTrackNumber=1;
        Runtime.SettingsStore.SelectMode(_savedMode);
        if(Selection!=null)
        {
            var manager=((ProcessDataContainer)BatchMembers.Get(Selection,"container")).processManager;
            manager.IsTimeCounting(true);manager.SendMessage(new Message(ProcessType.CommonProcess,20031));
        }
    }
    public void Save(BatchJob job)
    {
        BatchController.Save(job);
        if(BatchController.JournalError!=null)throw new System.IO.IOException(BatchController.JournalError);
    }
}
