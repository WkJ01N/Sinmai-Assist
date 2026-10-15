using System.Reflection;
using Manager;
using MAI2.Util;
using SinmaiAssist.Cheat;

internal static class Program
{
    private static int _checks;
    private static readonly uint[,] Scores=new uint[6,15];
    private static void Check(bool value,string message){if(!value)throw new Exception(message);_checks++;}
    private static object Call(Type type,string name,params object[] args)
    {
        try{return type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}
        catch(TargetInvocationException ex){throw ex.InnerException;}
    }
    private static Process.GameProcess World(bool two=false,float? musicOrigin=104)
    {
        PracticeAudio.Release();
        RankAutoPlay.Cancel(true);PracticeLoop.ResetSong();
        SinmaiAssist.SinmaiAssist.MainConfig=new();
        PracticeLoop.Speed=1;PracticeLoop.PreservePitch=true;PracticeLoop.Interval=5;
        PracticeAudio.Installed=true;
        Singleton<SoundCtrl>.Instance.Music.ResetParameters();
        RankAutoPlay.Installed=PlaySession.Installed=PracticeLoop.Installed=true;
        ScorePlanAdapter.Allowed=true;AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.None;
        Singleton<UserDataManager>.Instance.Users[0].IsEntry=true;Singleton<UserDataManager>.Instance.Users[1].IsEntry=two;
        Singleton<GamePlayManager>.Instance.QuickRetry=false;
        for(int p=0;p<2;p++)
        {
            Singleton<GamePlayManager>.Instance.Scores[p]=new();
            var reader=NotesManager.Instance(p).Reader;reader.Notes=new();
            reader.Notes.Add(new(){indexNote=0,time=new(){msec=1000},end=new(){msec=1000}});
            reader.Notes.Add(new(){indexNote=1,time=new(){msec=2000},end=new(){msec=3000}});
            reader.Notes.Add(new(){indexNote=2,time=new(){msec=4000},end=new(){msec=4000}});
        }
        NotesManager.Current=0;NotesManager.IsPaused=false;UnityEngine.Time.realtimeSinceStartup=0;
        SoundManager.OnSeek=null;SoundManager.Resumes=0;
        SoundManager.Ready=true;
        var process=new Process.GameProcess();Call(typeof(PlaySession),"Start",process);
        if(musicOrigin.HasValue)
        {
            NotesManager.Current=musicOrigin.Value;
            Call(typeof(PlaySession),"CaptureMusicOrigin");SoundManager.StartMusic();
        }
        NotesManager.Current=500;return process;
    }
    private static void Advance(Process.GameProcess process,float realMilliseconds,float speed=1)
    {
        if(!NotesManager.IsPaused)NotesManager.Current+=realMilliseconds*speed;
        Call(typeof(PlaySession),"AfterUpdate",process);
    }
    private static void MusicSyncChecks()
    {
        var process=World(musicOrigin:null);
        int starts=NotesManager.Starts;bool rejected=false;
        try{PlaySession.Seek(1200,true);}catch(InvalidOperationException e){rejected=e.Message.Contains("startup time");}
        Check(rejected&&NotesManager.Starts==starts&&!PlaySession.Paused,"missing origin rejects seek without changing timeline");
        PracticeLoop.A=1500;PracticeLoop.B=3500;rejected=false;
        try{PracticeLoop.Start();}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&!PracticeLoop.Session,"practice preflight rejects missing origin before starting session");
        NotesManager.Current=float.NaN;Call(typeof(PlaySession),"CaptureMusicOrigin");
        Check(!PlaySession.MusicOrigin.HasValue,"invalid native clock cannot seed origin");

        // Native start is delayed in real time; chart-clock units already include
        // startup speed. Later speed changes must not rescale this stored origin.
        foreach(float startupSpeed in new[]{0.5f,1f,2f})
        {
            float origin=104*startupSpeed;
            process=World(true,origin);
            Check(PlaySession.MusicOrigin==origin,"captures actual delayed startup including frame rounding and speed");
            SoundManager.OnSeek=()=>Check(PlaySession.Paused&&NotesManager.IsPaused&&SoundManager.Paused&&process.Movie.Paused.All(v=>v),"audio seek occurs while every timeline is paused");
            foreach(float laterSpeed in new[]{2f,0.5f,1f})
            {
                PlaySession.Seek(1200,true);
                Check(SoundManager.SeekPosition==(int)Math.Round(1200-origin,MidpointRounding.AwayFromZero)&&NotesManager.Current==1200&&!SoundManager.Paused,"seek subtracts startup origin once independently of later speed");
                Check(process.Movie.Positions.All(v=>v==1200),"video retains its independent seek position");
                Advance(process,10,laterSpeed);
                PlaySession.SetPause(true);PlaySession.Seek(20,true);
                Check(SoundManager.SeekPosition==0&&SoundManager.Paused&&PlaySession.Paused,"seek before origin remains paused at music zero");
                PlaySession.SetPause(false);
                Check(!PlaySession.Paused&&SoundManager.Paused,"chart resumes while waiting for audio origin");
                Advance(process,(origin-21)/laterSpeed,laterSpeed);
                Check(SoundManager.Paused,"music remains paused just before origin");
                PlaySession.SetPause(true);float clock=NotesManager.Current;
                Advance(process,1000,laterSpeed);
                Check(NotesManager.Current==clock&&SoundManager.Paused,"user pause freezes pending music start");
                PlaySession.SetPause(false);Advance(process,2/laterSpeed,laterSpeed);
                Check(!SoundManager.Paused&&!PlaySession.Paused,"music releases only after chart crosses original origin");
                int resumes=SoundManager.Resumes;Advance(process,10,laterSpeed);
                Check(SoundManager.Resumes==resumes,"pending audio release dispatches once");
                Call(typeof(PlaySession),"CaptureMusicOrigin");
                Check(PlaySession.MusicOrigin==origin,"repeated startup callback cannot overwrite song origin");
            }
            SoundManager.OnSeek=null;
        }
        process=World();PracticeLoop.A=1500;PracticeLoop.B=3500;PracticeLoop.Count=0;PracticeLoop.PreRoll=3;PracticeLoop.Interval=0;
        for(int round=0;round<3;round++)
        {
            if(round==0)PracticeLoop.Start();
            Check(NotesManager.Current==0&&SoundManager.SeekPosition==0&&SoundManager.Paused,"pre-roll crossing song zero delays music on every round");
            Advance(process,105);Check(!SoundManager.Paused,"loop audio begins at native origin");
            NotesManager.Current=(float)(PracticeLoop.WindowB+1);PracticeLoop.Update();PracticeLoop.Update();
        }
        PracticeLoop.Stop();PracticeLoop.Start();
        Check(NotesManager.Current==0&&SoundManager.Paused&&PlaySession.MusicOrigin==104,"stop and restart preserves song origin without accumulating delay");
        Call(typeof(PlaySession),"End",process);
        Check(!PlaySession.MusicOrigin.HasValue,"song release clears origin and pending audio state");
        process=World(musicOrigin:97);PlaySession.Seek(1200,false);
        Check(SoundManager.SeekPosition==1103,"new song acquires a fresh origin");
        process=World();PlaySession.Seek(0,false);PlaySession.SetPause(true);PlaySession.Seek(1500,false);PlaySession.SetPause(false);
        Check(!SoundManager.Paused&&SoundManager.SeekPosition==1396,"later seek cancels a stale pending audio release");
    }
    private static ScorePlanAdapter.Plan[] Plans(MinimumCombo minimum=MinimumCombo.None)
    {
        return Enumerable.Range(0,2).Where(p=>Singleton<UserDataManager>.Instance.Users[p].IsEntry).Select(p=>{
            var score=Singleton<GamePlayManager>.Instance.GetGameScore(p);
            var request=new ScorePlanner.Request{Scores=Scores,Minimum=minimum,Lower=0,Upper=100,IncludeUpper=true,Seed=p,
                Notes=Enumerable.Range(0,3).Select(i=>new ScorePlanner.Note{Index=i,Kind=0,FixedTiming=score.Results[i].Judged?score.Results[i].Timing:null}).ToArray()};
            return new ScorePlanAdapter.Plan{Player=p,Score=score,Notes=NotesManager.Instance(p).Reader.Notes,Request=request};
        }).ToArray();
    }
    private static void PracticeRateChecks()
    {
        var process=World(true);
        var config=SinmaiAssist.SinmaiAssist.MainConfig.Cheat.SpeedHack;
        config.Enable=true;config.SpeedMultiplier=1.5f;
        PracticeLoop.A=2500;PracticeLoop.B=3500;PracticeLoop.Count=2;PracticeLoop.PreRoll=1;
        PracticeLoop.Speed=.75f;
        PracticeLoop.Start();
        var music=Singleton<SoundCtrl>.Instance.Music;
        Check(PracticeLoop.Interval==5 && PlaybackRate.Effective==.75f,"default five-second rest and practice overrides SpeedHack without multiplying");
        Check(process.Movie.AllSpeed(.75f),"both movie players use practice speed");
        Check(music.StartedPool!=0 && music.StartedPitch==0 && Math.Abs(music.StartedStretch-1/.75f)<1e-6,"DSP route and reciprocal stretch applied before music start after native reset");
        int position=SoundManager.SeekPosition;
        music.RawTime=100;Check(Singleton<SoundCtrl>.Instance.GetPlayTiming(2)==position+75,"audio clock anchored at seek with active practice speed");
        PracticeLoop.Speed=1.25f;PracticeLoop.PreservePitch=false;
        config.Enable=false;config.SpeedMultiplier=2;
        Check(PlaybackRate.Effective==.75f && PracticeLoop.ActivePreservePitch && music.Pitch==0,"draft speed, pitch and SpeedHack edits do not affect current round");
        music.RawTime=200;Check(Singleton<SoundCtrl>.Instance.GetPlayTiming(2)==position+150,"audio elapsed history is not rescaled after draft edits");
        NotesManager.Current=(float)(PracticeLoop.WindowB+1);PracticeLoop.Update();
        Check(PlaySession.Paused&&SoundManager.Paused&&process.Movie.Paused.All(p=>p)&&PracticeLoop.State.WaitRemaining==5,"round completion freezes every timeline for five real seconds");
        UnityEngine.Time.realtimeSinceStartup=4;PracticeLoop.Update();
        Check(PracticeLoop.State.WaitRemaining==1 && PlaySession.Paused,"rest is independent of practice and SpeedHack multipliers");
        PracticeLoop.TogglePause();UnityEngine.Time.realtimeSinceStartup=20;PracticeLoop.Update();
        Check(PracticeLoop.State.WaitRemaining==1,"pause freezes remaining rest");
        PracticeLoop.TogglePause();UnityEngine.Time.realtimeSinceStartup=21;PracticeLoop.Update();
        Check(!PlaySession.Paused && PlaybackRate.Effective==1.25f && !PracticeLoop.ActivePreservePitch,"next round atomically applies pending speed and pitch");
        Check(music.StartedPool==0 && Math.Abs(music.StartedPitch-1200*Math.Log(1.25,2))<.001 && music.StartedStretch==1,"pitch-following mode uses default pool and new speed after native reset");
        Check(process.Movie.AllSpeed(1.25f),"movie speed changes with next round");
        NotesManager.Current=(float)(PracticeLoop.WindowB+1);PracticeLoop.Update();
        Check(!PracticeLoop.Running && PlaySession.Paused && PracticeLoop.State.Completed==2,"finite count finishes paused instead of restarting after rest");
        int disposed=CriAtomExStandardVoicePool.Disposed;
        PlaySession.Retry();
        Check(music.Stopped && music.Pool==0 && CriAtomExStandardVoicePool.Disposed==disposed+1,"retry stops native voice before pool disposal and restores default route");
        Call(typeof(PlaySession),"End",process);
        Check(PlaybackRate.Effective==1 && config.SpeedMultiplier==2 && !config.Enable,"leaving practice restores untouched disabled SpeedHack state");

        process=World();config=SinmaiAssist.SinmaiAssist.MainConfig.Cheat.SpeedHack;
        config.Enable=true;config.SpeedMultiplier=1.5f;
        PracticeLoop.A=1500;PracticeLoop.B=3500;PracticeLoop.Speed=.5f;PracticeLoop.PreservePitch=false;
        PracticeLoop.Start();PracticeLoop.Stop();PracticeLoop.Speed=2;PracticeLoop.Start();
        Check(PracticeLoop.ActiveSpeed==2 && PlaybackRate.Effective==2,"stop then start applies new practice speed immediately");
        PlaySession.Retry();Call(typeof(PlaySession),"End",process);
        Check(PlaybackRate.Effective==1.5f && config.Enable && config.SpeedMultiplier==1.5f,"native retry restores enabled SpeedHack multiplier");

        process=World();PracticeLoop.A=1500;PracticeLoop.B=3500;
        CriAtomExStandardVoicePool.FailAttach=true;disposed=CriAtomExStandardVoicePool.Disposed;bool failed=false;
        try{PracticeLoop.Start();}catch(InvalidOperationException e){failed=e.Message.Contains("Time stretch");}
        Check(failed&&!PracticeLoop.Running&&PlaySession.Paused&&SoundManager.Paused,"failed stretch preparation pauses rather than silently changing pitch mode");
        Check(CriAtomExStandardVoicePool.Disposed==disposed+1,"failed DSP attachment disposes partially created pool");
        CriAtomExStandardVoicePool.FailAttach=false;
        PracticeLoop.PreservePitch=false;PracticeLoop.Start();
        Check(PracticeLoop.Running&&!PlaySession.Paused&&Singleton<SoundCtrl>.Instance.Music.StartedPool==0,"user can recover by explicitly disabling preserve pitch");
        PracticeAudio.Installed=false;PracticeLoop.Stop();failed=false;
        try{PracticeLoop.Start();}catch(InvalidOperationException){failed=true;}
        Check(failed&&PlaySession.Paused,"missing native audio hook fails closed");PracticeAudio.Installed=true;
        foreach(float speed in new[]{.49f,2.01f,float.NaN})
        {
            PracticeLoop.Speed=speed;failed=false;try{PracticeLoop.Start();}catch(InvalidOperationException){failed=true;}
            Check(failed,"out of range practice speed rejected");
        }
        PracticeLoop.Speed=.5f;PracticeLoop.AdjustSpeed(-.05f);Check(PracticeLoop.Speed==.5f,"speed shortcut lower limit");
        PracticeLoop.Speed=2;PracticeLoop.AdjustSpeed(.05f);Check(PracticeLoop.Speed==2,"speed shortcut upper limit");
        PlaySession.Retry();Call(typeof(PlaySession),"End",process);
        process=World();PlaySession.Seek(0,false);Advance(process,117);
        Check(SoundManager.SeekPosition==13&&!SoundManager.Paused,"zero-origin threshold compensates 13ms frame overshoot rather than starting audio late at zero");
        process=World(musicOrigin:104.75f);PlaySession.Seek(1200,false);
        Check(SoundManager.SeekPosition==1095,"seek uses nearest millisecond rather than truncating downward");

        process=World();PracticeLoop.A=1500;PracticeLoop.B=3500;SoundManager.Ready=false;
        PracticeLoop.Start();
        Check(PlaySession.PreparingMusic && PlaySession.Paused && SoundManager.Paused,"asynchronous music preparation holds chart, movie and audio before playback");
        SoundManager.Ready=true;Call(typeof(PlaySession),"Update",process);
        Check(!PlaySession.PreparingMusic && !PlaySession.Paused,"ready music resumes requested playback together");
        PracticeLoop.Stop();SoundManager.Ready=false;PracticeLoop.Start();PracticeLoop.TogglePause();
        SoundManager.Ready=true;Call(typeof(PlaySession),"Update",process);
        Check(PlaySession.Paused,"explicit pause during preparation cancels automatic resume");
        PracticeLoop.Stop();SoundManager.Ready=false;PracticeLoop.Start();
        UnityEngine.Time.realtimeSinceStartup=6;Call(typeof(PlaySession),"Update",process);
        Check(!PracticeLoop.Running && PlaySession.Paused && !PlaySession.PreparingMusic,"native preparation timeout leaves practice stopped and paused");
        SoundManager.Ready=true;PlaySession.Retry();Call(typeof(PlaySession),"End",process);

        process=World();config=SinmaiAssist.SinmaiAssist.MainConfig.Cheat.SpeedHack;config.Enable=true;config.SpeedMultiplier=.5f;
        Call(typeof(SpeedHackNotesClockPatch),"StartPlayPostfix",0f);
        NotesManager.Current=100;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        Check(NotesManager.Current==50,"production notes postfix integrates raw stopwatch at half speed");
        config.SpeedMultiplier=2;NotesManager.Current=100;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        NotesManager.Current=200;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        Check(NotesManager.Current==250,"production notes postfix applies new rate only after transition anchor");
        config.SpeedMultiplier=1;NotesManager.Current=200;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        NotesManager.Current=300;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        Check(NotesManager.Current==350,"production notes postfix preserves offset on return to 1x");
        config.Enable=false;NotesManager.Current=300;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        NotesManager.Current=400;Call(typeof(SpeedHackNotesClockPatch),"UpdateTimerPostfix");
        Check(NotesManager.Current==450,"disabling SpeedHack preserves chart continuity");
        SpeedHackAudioClockPatch.Reset(2,1500);var native=Singleton<SoundCtrl>.Instance.Music;
        config.Enable=true;config.SpeedMultiplier=.5f;
        native.RawTime=0;Singleton<SoundCtrl>.Instance.GetPlayTiming(2);
        native.RawTime=100;Check(Singleton<SoundCtrl>.Instance.GetPlayTiming(2)==1550,"production audio timer keeps seek anchor at half speed");
        config.SpeedMultiplier=2;Singleton<SoundCtrl>.Instance.GetPlayTiming(2);
        native.RawTime=200;Check(Singleton<SoundCtrl>.Instance.GetPlayTiming(2)==1750,"production audio timer integrates after speed change without multiplying past history");
    }
    private static async Task Drain(Process.GameProcess process)
    {
        var timeout=System.Diagnostics.Stopwatch.StartNew();
        while(RankAutoPlay.IsBusy)
        {
            Check(timeout.ElapsedMilliseconds<5000,"rank worker bounded");
            await Task.Delay(2);Call(typeof(PlaySession),"Update",process);
        }
    }
    private static async Task Main()
    {
        for(int k=0;k<6;k++)for(int t=0;t<15;t++)Scores[k,t]=(uint)(t==0||t==14?0:t>=5&&t<=9?500:250);
        MusicSyncChecks();
        PracticeRateChecks();
        var process=World(true);Check(PlaySession.CanPractice,"timeline compatibility");
        PlaySession.SetPause(true);PlaySession.Seek(1200,true);
        Check(PlaySession.Paused && NotesManager.IsPaused && SoundManager.Paused,"seek restores pause state");
        Check(process.Movie.Positions.All(p=>p==1200)&&process.Movie.Paused.All(p=>p),"two-screen float movie adapter");
        Check(process.Monitors.All(m=>m.Controller.ActiveCount==0 && m.Controller.Clicks==1),"pooled note/slide lists and clicks reset");
        Check(process.Monitors.All(m=>!m.Controller.GradeVisible),"old judgement visuals cleared on each reset");
        Check(process.Monitors.All(m=>m.BarTime==1200),"seek updates canonical clock before monitor reset");
        Check(SoundManager.SeekPosition==1096,"music time differs from canonical chart time by native startup offset");
        Check(NotesManager.Instance(0).Reader.Composition._clickSeList[0].Played&&!NotesManager.Instance(0).Reader.Composition._clickSeList[1].Played,"past click sounds suppressed");

        process=World();PracticeLoop.A=2500;PracticeLoop.B=3500;PracticeLoop.Count=2;PracticeLoop.Interval=2;PracticeLoop.PreRoll=1;
        PracticeLoop.Start();
        Check(SoundManager.SeekPosition==PlaySession.MusicSeekPosition(PlaySession.Time),"first practice round uses song audio origin");
        bool rejected=false;try{RankAutoPlay.Request();}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&!RankAutoPlay.Requested,"practice excludes rank takeover");
        Check(PracticeLoop.State.A==2000 && PracticeLoop.WindowA<2000 && PracticeLoop.WindowB>3500,"crossing Hold expands with judge windows");
        Check(process.Monitors[0].Seeks==1&&process.Monitors[1].Seeks==0,"only entered player reset");
        var score=Singleton<GamePlayManager>.Instance.GetGameScore(0);
        Check(!PracticeLoop.IncludeJudge(1,score),"pre-roll excludes judgements");
        NotesManager.Current=(float)(PracticeLoop.WindowA+1);
        Check(PracticeLoop.IncludeJudge(1,score)&&!PracticeLoop.IncludeJudge(0,score),"segment filter admits chosen notes and excludes earlier notes");
        Check(score.Clears==2,"round cleared at reset and judgement-window entry");
        score.SetResult(1,0,NoteJudge.ETiming.Critical);
        NotesManager.Current=(float)(PracticeLoop.WindowB+1);PracticeLoop.Update();
        Check(PracticeLoop.State.Completed==1&&PlaySession.Paused,"first round pauses for interval");
        PracticeLoop.TogglePause();UnityEngine.Time.realtimeSinceStartup=8;PracticeLoop.Update();
        Check(PracticeLoop.State.WaitRemaining==2,"pause freezes real interval");
        PracticeLoop.TogglePause();UnityEngine.Time.realtimeSinceStartup=10;PracticeLoop.Update();
        Check(!PlaySession.Paused&&PracticeLoop.State.Completed==1&&score.Results.All(r=>!r.Judged),"next round resets score without adding a song");
        Check(SoundManager.SeekPosition==PlaySession.MusicSeekPosition(PlaySession.Time)&&PlaySession.MusicOrigin==104,"subsequent round does not accumulate startup offset");
        NotesManager.Current=(float)(PracticeLoop.WindowB+1);PracticeLoop.Update();
        Check(!PracticeLoop.Running&&PracticeLoop.State.Completed==2&&PlaySession.Paused,"finite repeats retain final round at B");
        var resultArgs=new object[]{true};Check(!(bool)Call(typeof(PracticeLoop),"KeepPracticeOpen",resultArgs)&&!(bool)resultArgs[0],"practice cannot take native end-of-song path");
        PlaySession.Retry();Check(process.Retries==1&&process.ResultSaves==0,"practice exit uses retry without result save");
        Call(typeof(PlaySession),"End",process);Check(!PracticeLoop.Session&&!PracticeLoop.A.HasValue,"release clears song state");

        process=World(true);Singleton<UserDataManager>.Instance.Users[0].IsEntry=false;
        PracticeLoop.A=1500;PracticeLoop.B=3500;PracticeLoop.Count=1;PracticeLoop.Interval=0;
        PracticeLoop.Start();Check(process.Monitors[0].Seeks==0&&process.Monitors[1].Seeks==1,"2P-only practice reset");
        Check(SoundManager.SeekPosition==PlaySession.MusicSeekPosition(PlaySession.Time),"2P-only practice shares corrected audio timeline");
        PracticeLoop.Stop();Check(PlaySession.Paused&&!PracticeLoop.Running,"manual stop pauses");
        Call(typeof(PlaySession),"End",process);

        process=World(true);ScorePlanAdapter.Source=()=>Plans();RankAutoPlay.Request();
        bool run=(bool)Call(typeof(PlaySession),"Update",process);
        Check(!run&&RankAutoPlay.IsBusy&&PlaySession.Paused,"planning freezes game update");await Drain(process);
        Check(RankAutoPlay.Active&&!PlaySession.Paused,"successful pair commits and resumes");
        Check(RankAutoPlay.TryTiming(new Monitor.NoteBase(0,0),out var first)&&RankAutoPlay.TryTiming(new Monitor.SlideRoot(1,1),out _),"Tap and independent Slide index lookup");
        string active=RankAutoPlay.ActiveTarget;
        ScorePlanAdapter.Source=()=>{var plans=Plans();plans[1].Request.Minimum=MinimumCombo.FC;plans[1].Request.Notes[0].FixedTiming=14;return plans;};
        RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);await Drain(process);
        Check(RankAutoPlay.Active&&RankAutoPlay.ActiveTarget==active,"one-player failure preserves old pair plan");
        Check(RankAutoPlay.TryTiming(new Monitor.NoteBase(0,0),out var retained)&&retained==first,"failed replacement does not mutate old assignments");
        var inputArgs=new object[]{true};Check((bool)Call(typeof(RankInputPatch),"IgnoreManual",inputArgs),"native controls work outside note update");
        var entered=new object[]{false};Call(typeof(RankNoteInputScope),"Enter",entered);
        inputArgs=new object[]{true};Check(!(bool)Call(typeof(RankInputPatch),"IgnoreManual",inputArgs)&&!(bool)inputArgs[0],"manual note input suppressed inside note update");
        var injected=new Exception("note exception");Check(ReferenceEquals(Call(typeof(RankNoteInputScope),"Leave",entered[0],injected),injected)&&!RankNoteInputScope.InNoteUpdate,"input scope clears on exceptions");
        RankAutoPlay.Cancel(true);Check(!RankAutoPlay.Active&&AutoPlay.autoPlayMode==AutoPlay.AutoPlayMode.None,"explicit cancellation returns manual control");
        ScorePlanAdapter.Source=()=>Plans();PlaySession.SetPause(true);RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);await Drain(process);
        Check(RankAutoPlay.Active&&PlaySession.Paused,"planning preserves an originally paused song");
        RankAutoPlay.Cancel(true);ScorePlanAdapter.Allowed=false;RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);
        Check(!RankAutoPlay.Active&&PlaySession.Paused,"preflight failure does not resume paused song");
        ScorePlanAdapter.Allowed=true;RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);RankAutoPlay.Cancel(true);
        await Task.Delay(5);Call(typeof(PlaySession),"Update",process);Check(!RankAutoPlay.Active&&!RankAutoPlay.IsBusy,"cancelled completed worker cannot commit");
        process=World();AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Critical;ScorePlanAdapter.Source=()=>Plans();
        RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);
        Singleton<GamePlayManager>.Instance.GetGameScore(0).Results[0]=new(){Judged=true,Timing=7};
        await Drain(process);
        Check(!RankAutoPlay.Active&&AutoPlay.autoPlayMode==AutoPlay.AutoPlayMode.Critical,"newly judged note invalidates snapshot without replacing old mode");
        process=World(true);AutoPlay.autoPlayMode=AutoPlay.AutoPlayMode.Critical;ScorePlanAdapter.Source=()=>Plans();
        RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);Singleton<UserDataManager>.Instance.Users[1].IsEntry=false;
        await Drain(process);Check(!RankAutoPlay.Active&&AutoPlay.autoPlayMode==AutoPlay.AutoPlayMode.Critical,"changed player set rejects atomic takeover");
        process=World();ScorePlanAdapter.Plan[] captured=null;
        ScorePlanAdapter.Source=()=>{captured=Plans(MinimumCombo.AP);captured[0].Request.Upper=101;return captured;};
        RankAutoPlay.Request();Call(typeof(PlaySession),"Update",process);await Drain(process);
        foreach(var plan in captured)
        {
            for(int i=0;i<plan.Request.Notes.Length;i++)plan.Score.Results[i]=new(){Judged=true,Timing=plan.Result.Timings[i]};
            plan.Score.Achievement=plan.Result.Achievement+0.00000000000000000001m;
        }
        RankAutoPlay.Verify();Check(RankAutoPlay.Status.StartsWith("Rank plan verified"),"verification tolerates decimal arithmetic rounding without changing result");
        Console.WriteLine($"PASS: {_checks} production timeline/practice/rank flow checks with native-boundary doubles (not in-game validation).");
    }
}
