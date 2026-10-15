using SinmaiAssist.Cheat;
using SinmaiAssist.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class BatchChecks
{
    private static int _checks;
    private static void Check(bool condition,string message){_checks++;if(!condition)throw new Exception("Batch: "+message);}
    private static void Reject(Action action,string message)
    {bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,message);}
    private static BatchEntry Entry(int id,int difficulty=2,int player=0,int scoreType=1)=>new()
    {
        MusicId=id,Name="Music "+id,Category="Category",ScoreType=scoreType,
        Charts=new(){new(){Difficulty=player==0?new[]{difficulty,-1}:new[]{-1,difficulty},Levels=player==0?new[]{"12+",null}:new[]{null,"11"}}}
    };
    private static BatchJob Job(int count=1,bool dual=false,int player=0)
    {
        var settings=new BatchSettings{SongCount=count,SkipExisting=false};
        var entries=Enumerable.Range(10,count).Select(i=>Entry(i,player:player)).ToList();
        if(dual)foreach(var entry in entries){entry.Charts[0].Difficulty[1]=3;entry.Charts[0].Levels[1]="14";}
        return new(){Users=dual?new[]{"111","222"}:player==0?new[]{"111",null}:new[]{null,"222"},Settings=settings,
            Songs=BatchPolicy.Queue(new List<List<BatchEntry>>{entries},0,0,settings,1)};
    }
    public static void Run()
    {
        QueueAndGoals();Uploads();Flows();Recovery();
        Console.WriteLine($"PASS: {_checks} batch queue, probabilities, AP+, persistence, state-machine and upload protection checks.");
    }
    private static void QueueAndGoals()
    {
        var settings=new BatchSettings();Check(settings.SongCount==10&&!settings.CrossCategories&&!settings.Boost&&settings.SkipExisting,"defaults");
        var entries=new List<List<BatchEntry>>{new(){Entry(10),Entry(11),Entry(12)},new(){Entry(12,3),Entry(13),Entry(11,4)},new(){Entry(14)}};
        settings.ByCategories=true;settings.CategoryCount=2;
        var songs=BatchPolicy.Queue(entries,0,1,settings,1);
        Check(songs.Select(s=>s.MusicId).SequenceEqual(new[]{11,12,13}),"start at cursor, include partial category, no third category");
        Check(songs[0].Charts.Count==2&&songs[1].Charts.Count==2,"merge duplicate song/type difficulty tasks across categories");
        Check(new BatchJob{Songs=songs}.Processed==0,"count only finished songs");
        songs[0].Charts[0].Outcome="Failed";Check(new BatchJob{Songs=songs}.Processed==0,"individual difficulty is not a song");
        songs[0].Charts[1].Outcome="Already met";Check(new BatchJob{Songs=songs}.Processed==1,"partial failure still counts once");
        Check(entries[0][1].Charts[0].Outcome==null&&entries[0][1].Charts[0].Goals==null,"queue freezes detached chart objects");
        entries[0][1].Charts[0].Difficulty[0]=0;Check(songs[0].Charts[0].Difficulty[0]==2,"queue does not alias chart arrays");
        settings.ByCategories=false;settings.SongCount=10;
        Check(BatchPolicy.Queue(entries,0,1,settings,1).Count==2,"no category crossing by default");
        settings.CrossCategories=true;settings.SongCount=3;
        Check(BatchPolicy.Queue(entries,0,1,settings,1).Count==3,"unique song quota");
        settings.ByCategories=true;settings.CategoryCount=9;
        Check(BatchPolicy.Queue(entries,2,0,settings,1).Single().MusicId==14,"rightmost category never wraps");
        var twoTypes=new List<List<BatchEntry>>{new(){Entry(10,scoreType:0),Entry(10,scoreType:1),Entry(1),Entry(100000)}};
        Check(BatchPolicy.Queue(twoTypes,0,0,settings,1).Count==2,"dedup includes score type; exclude random/utage placeholders");
        settings=new BatchSettings{Minimum=MinimumCombo.APPlus};Reject(settings.Validate,"explicit SSS/AP+ conflict");
        settings.TargetRank="SSS+";settings.Validate();
        Check(BatchPolicy.Draw(settings,"14",new Random(1)).Theory,"base AP+ is theoretical");
        settings=new BatchSettings{Boost=true};
        var boundary=settings.Profiles.Single(p=>p.Level=="12+");
        Check(boundary.Outcomes.Select(w=>w.Weight).SequenceEqual(new double[]{60,5,5,10,20}),"12+ joint template exact weights");
        foreach(var profile in settings.Profiles)
        {Check(Math.Abs(profile.Outcomes.Sum(w=>w.Weight)-100)<1e-9,"normalized template");Check(profile.Outcomes.Last().Weight<=80,"AP+ maximum 80%");}
        Check(settings.Profiles.Single(p=>p.Level=="12").Outcomes.Last().Weight==25,"one display step increases AP+ by five points");
        var random=new Random(21);int theory=0,baseCount=0;
        for(int i=0;i<50000;i++)
        {
            var goal=BatchPolicy.Draw(settings,"12+",random);if(goal.Theory)theory++;else if(goal.Rank=="SSS")baseCount++;
            Check(!goal.Theory||goal.Minimum==MinimumCombo.APPlus&&goal.Rank=="SSS+","theory outcome consistency");
        }
        Check(Math.Abs(theory/50000d-.2)<.01&&Math.Abs(baseCount/50000d-.6)<.01,"joint sampling follows configured weights");
        Check(!BatchPolicy.Draw(settings,"13",new Random(2)).Theory&&BatchPolicy.Draw(settings,"13",new Random(3)).Rank=="SSS","boost threshold inclusive, above unchanged");
        settings.Minimum=MinimumCombo.AP;settings.TargetRank="SSS+";
        for(int i=0;i<1000;i++){var goal=BatchPolicy.Draw(settings,"10",random);Check(goal.Minimum>=MinimumCombo.AP&&goal.Rank=="SSS+","boost never downgrades base rank/combo");}
        var copy=settings.Copy();copy.Profiles[0].Outcomes[0].Weight++;
        Check(!settings.Same(copy)&&settings.Profiles[0].Outcomes[0].Weight!=copy.Profiles[0].Outcomes[0].Weight,"deep copy and dirty comparison include nested weight");
        var yaml=new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var parser=new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var snapshot=parser.Deserialize<BatchJob>(yaml.Serialize(Job(2,dual:true)));
        Check(snapshot.Songs.Count==2&&snapshot.Songs[0].Charts[0].Goals[1].Rank=="SSS","journal serializes task and player goal without computed properties");
        copy.Profiles[0].Outcomes[0].Weight=double.NaN;Reject(copy.Validate,"invalid probability weight rejected");
        settings.Minimum=MinimumCombo.None;settings.TargetRank="SSS";
        boundary.Outcomes=new(){new(){UseBaseCombo=false,Combo=MinimumCombo.APPlus,Rank="SSS",Weight=1}};
        var explicitTheory=BatchPolicy.Draw(settings,"12+",random);
        Check(explicitTheory.Theory&&explicitTheory.Rank=="SSS+","AP+ weight is a separate theoretical outcome");
    }
    private static BatchUploadOperation Operation(string id,Action<Action<BatchUploadReply,string>> send)=>new(){Receipt=new(){Id=id},Send=send};
    private static void Uploads()
    {
        int first=0,second=0;var timestamps=new List<double>();double now=0;
        var upload=new BatchUpload(new(){Operation("P1",r=>{first++;r(BatchUploadReply.Accepted,null);}),Operation("P2",r=>{second++;timestamps.Add(now);r(second<3?BatchUploadReply.Rejected:BatchUploadReply.Accepted,"rejected");})});
        upload.Tick(now=0);upload.Tick(now=.1);upload.Tick(now=1);Check(second==1,"first retry waits real second");
        upload.Tick(now=1.1);upload.Tick(now=3);Check(second==2,"second retry waits two seconds");
        upload.Tick(now=3.1);Check(upload.Done&&first==1&&second==3,"partial dual-player acceptance never resent");
        Check(timestamps.SequenceEqual(new[]{.1,1.1,3.1}),"exact 1s/2s retry schedule");
        first=0;upload=new(new(){Operation("paused",r=>{first++;r(BatchUploadReply.Accepted,null);})});
        upload.Tick(0,false);Check(first==0,"pause blocks starting another upload phase");
        upload.Tick(1,true);Check(first==1&&upload.Done,"resume permits next upload phase");
        int sends=0;Action<BatchUploadReply,string> callback=null;
        upload=new(new(){Operation("lost",r=>{sends++;callback=r;})},timeout:5);
        upload.Tick(0);upload.Tick(5.1);Check(upload.Blocked&&upload.Operations[0].Receipt.State=="Unknown","timeout remains unknown");
        callback(BatchUploadReply.Accepted,null);Check(!upload.Done,"late callback cannot silently resolve timed-out request");
        for(int i=0;i<10;i++)upload.Tick(20+i);Check(sends==1,"unknown response never automatically resent");
        Reject(upload.RetryRejected,"ordinary resume refuses unknown");upload.ResolveUnknown(true);Check(upload.Done,"explicit server confirmation records success");
        upload=new(new(){Operation("reject",r=>r(BatchUploadReply.Rejected,"known rejection"))});
        upload.Tick(0);upload.Tick(1);upload.Tick(3);Check(upload.Blocked&&upload.Operations[0].Receipt.Attempts==3,"known reject gets two automatic retries");
        upload=new(new(){Operation("exception",_=>throw new IOException("transport error"))});upload.Tick(0);
        Check(upload.Blocked&&upload.Operations[0].Receipt.State=="Unknown","send exception cannot be treated as unsent");
        int retries=0;upload=new(new(){Operation("retry",r=>{retries++;r(retries==1?BatchUploadReply.Unknown:BatchUploadReply.Accepted,null);})});
        upload.Tick(0);upload.ResolveUnknown(false);upload.Tick(1);Check(upload.Done&&retries==2,"explicit retry requires an unknown resolution");
    }
    private static void Pump(BatchRun run,Host host,int steps=1000)
    {for(int i=0;i<steps&&run.Active&&!run.Paused;i++){run.Tick();host.Advance();}}
    private static void Flows()
    {
        foreach(int player in new[]{0,1,2})
        foreach(double speed in new[]{.5,1,3})
        {
            var job=Job(7,dual:player==2,player:player==1?1:0);var host=new Host(job.Users){Scale=speed};
            var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(!run.Active&&!run.Paused&&job.Finished,"automatic solo/2P-only/dual flow completes with SpeedHack");
            Check(job.Processed==7&&host.Committed==7,"song count independent of native tracks");
            Check(host.UploadSizes.SequenceEqual(new[]{4,3})&&host.Logs==0&&host.ResetCount==2,"four-track batches and final flush");
            Check(host.ReadBeforeClear&&host.Restored&&host.Snapshots.All(s=>s>0),"reset only after native next-track reads, restore after final clear");
            Check(job.Songs.All(s=>s.Charts.All(c=>c.Saved)),"accepted receipts persist task durability");
        }
        {
            var job=Job(2);var host=new Host(job.Users){Logs=4};var run=new BatchRun(host);run.Start(job);run.Tick();
            Check(host.Opened==0&&run.Stage=="Upload","flush full pre-existing batch before opening another song");Pump(run,host);
            Check(host.UploadSizes.SequenceEqual(new[]{4,2}),"pre-batch logs included once");
        }
        {
            var job=Job();var song=job.Songs[0];
            song.Charts=Enumerable.Range(0,5).Select(d=>new BatchChart{Difficulty=new[]{d,-1},Levels=new[]{"12+",null},Goals=new[]{new BatchGoal{Rank="SSS"},null}}).ToList();
            var host=new Host(job.Users);var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(job.Processed==1&&host.Committed==5&&host.UploadSizes.SequenceEqual(new[]{4,1}),"five difficulties count as one song across native upload batches");
        }
        {
            var job=Job(10);var host=new Host(job.Users){Logs=2};var run=new BatchRun(host);run.Start(job);run.Stop();Pump(run,host);
            Check(host.Opened==0&&host.UploadSizes.SequenceEqual(new[]{2})&&job.Processed==0,"stop before opening flushes pre-existing logs and keeps queue unfinished");
        }
        {
            var job=Job(2);var host=new Host(job.Users){Failures=3};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(job.Songs[0].Charts[0].Attempts==3&&job.Songs[0].Charts[0].Outcome=="Failed","three failed searches skip chart");
            Check(host.Abandoned==1&&host.Committed==1&&host.UploadSizes.SequenceEqual(new[]{1}),"failed chart creates no empty/miss result or uploaded log");
            Check(ReferenceEquals(host.GeneratedGoals[0],host.GeneratedGoals[1])&&ReferenceEquals(host.GeneratedGoals[1],host.GeneratedGoals[2]),"retry target fixed");
        }
        {
            var job=Job();job.Settings.SkipExisting=true;var host=new Host(job.Users){Met=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(host.Opened==0&&job.Processed==1&&job.Songs[0].Charts[0].Outcome=="Already met","existing chart still counts a processed song");
        }
        {
            var job=Job();var host=new Host(job.Users){HoldGeneration=true};var run=new BatchRun(host);run.Start(job);Pump(run,host,4);
            Check(run.Stage=="Generate","hold worker while native clocks frozen");run.TogglePause();int attempts=job.Songs[0].Charts[0].Attempts;
            host.Now+=500;run.Tick();Check(run.Paused&&host.Frozen&&attempts==job.Songs[0].Charts[0].Attempts,"paused timeout and planning state do not advance tasks");
            run.Stop();Pump(run,host);Check(!run.Active&&host.Abandoned==1&&host.UploadSizes.Count==0,"stop cancels worker and returns without empty result");
        }
        {
            var job=Job(2);var host=new Host(job.Users){RejectUpload=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(run.Paused&&run.Upload.Blocked&&host.Logs==2&&host.ResetCount==0,"known upload rejection protects logs and track number");
            host.RejectUpload=false;run.TogglePause();Pump(run,host);Check(!run.Active&&host.Logs==0,"manual retry after rejected upload completes cleanup");
        }
        {
            var job=Job(2,dual:true);var host=new Host(job.Users){UnknownSecond=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(run.Paused&&host.AcceptedSends==1&&host.ResetCount==0,"second-player unknown retains all logs and first-player acknowledgement");
            run.Stop();Check(run.Active&&run.Paused,"stop cannot discard unknown submission");
            run.ResolveUpload(true);run.TogglePause();Pump(run,host);Check(!run.Active&&host.AcceptedSends==1,"resolve without repeating accepted player phase");
        }
        {
            var job=Job();var host=new Host(job.Users);var run=new BatchRun(host);run.Start(job);host.Users=new[]{"wrong",null};run.Tick();
            Check(run.Paused&&host.Opened==0,"identity changes pause before any action");
            Reject(run.TogglePause,"cannot resume with different login");host.Users=job.Users;run.TogglePause();Pump(run,host);Check(!run.Active,"resume after matching identities");
        }
        {
            var job=Job();var host=new Host(job.Users){Fatal=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(run.Paused&&job.Songs[0].Charts[0].Attempts==1&&host.Abandoned==0,"runtime interface errors pause batch rather than skipping as chart failures");
        }
        {
            var job=Job();var host=new Host(job.Users);var run=new BatchRun(host);run.Start(job);host.Scene=BatchScene.Unknown;run.Tick();Check(run.Paused,"unknown scene pauses");
        }
        {
            var job=Job();var host=new Host(job.Users){FailAcknowledgementSave=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
            Check(run.Paused&&host.Logs==1&&host.ResetCount==0,"failed durable acknowledgement write prevents log clear");
        }
    }
    private static void Recovery()
    {
        var job=Job();var chart=job.Songs[0].Charts[0];chart.Outcome="Success";
        job.Uploads.Add(new(){Id="unknown",State="Sending"});Check(BatchRecovery.NeedsReview(job),"crash during request requires review");
        Reject(()=>new BatchRun(new Host(job.Users)).Start(job),"unconfirmed upload cannot auto resume");
        Reject(()=>BatchRecovery.Review(job,new[]{"wrong",null},0),"review checks login identity");
        Reject(()=>BatchRecovery.Review(job,job.Users,1),"restart review cannot discard current pending logs");
        var goal=chart.Goals[0];BatchRecovery.Review(job,job.Users,0);
        Check(!BatchRecovery.NeedsReview(job)&&chart.Outcome==null&&chart.VerifyHistory&&ReferenceEquals(goal,chart.Goals[0]),"restart rewinds only unconfirmed tasks and retains frozen goals");
        var host=new Host(job.Users){Met=true};var run=new BatchRun(host);run.Start(job);Pump(run,host);
        Check(host.Opened==0&&chart.Outcome=="Already met","recovery checks best scores even when normal skip-existing was disabled");
        job=Job();chart=job.Songs[0].Charts[0];chart.Attempts=3;
        host=new Host(job.Users);run=new BatchRun(host);run.Start(job);Pump(run,host);
        Check(host.Opened==0&&chart.Outcome=="Failed"&&chart.Attempts==3,"restart retains the total three-attempt budget");
    }
    private sealed class Host : IBatchRuntime
    {
        public Host(string[] users){Users=users;}
        public double Now {get;set;}
        public double Scale=1;
        public BatchScene Scene {get;set;}=BatchScene.Selection;
        public string[] Users {get;set;}
        public int Logs,Opened,Committed,Abandoned,ResetCount,Failures,AcceptedSends;
        public bool Met,HoldGeneration,RejectUpload,UnknownSecond,Fatal,FailAcknowledgementSave,Frozen,Restored;
        public bool ReadBeforeClear=true;
        private bool _reset,_leaving,_abandoning;
        public readonly List<int> UploadSizes=new(),Snapshots=new();
        public readonly List<BatchGoal[]> GeneratedGoals=new();
        public int PendingLogs=>Logs;
        public int Limit=>4;
        public bool ResetDone=>!_reset;
        public void Begin(BatchJob job){}
        public bool AlreadyMet(BatchSong song,BatchChart chart)=>Met;
        public void Open(BatchSong song,BatchChart chart){Opened++;Scene=BatchScene.Opening;}
        public Task<object> Generate(BatchSong song,BatchChart chart,CancellationToken token)
        {
            GeneratedGoals.Add(chart.Goals);
            if(Fatal)throw new InvalidOperationException("missing native interface");
            if(Failures-->0)return Task.FromException<object>(new BatchChartException("unreachable chart"));
            if(HoldGeneration)return new TaskCompletionSource<object>().Task;
            return Task.FromResult<object>(chart.Goals);
        }
        public void Commit(object result){Committed++;Scene=BatchScene.Result;}
        public void VerifyResult(BatchSong song,BatchChart chart){}
        public void Abandon(){Abandoned++;Logs--;_abandoning=true;Scene=BatchScene.Transition;}
        public void LeaveResult(){_leaving=true;Scene=BatchScene.Transition;}
        public BatchUpload CaptureUpload()
        {
            UploadSizes.Add(Logs);Snapshots.Add(Logs);
            var ops=new List<BatchUploadOperation>();
            for(int p=0;p<2;p++)if(Users[p]!=null)
            {
                int player=p;
                ops.Add(Operation("P"+p,r=>{
                    if(UnknownSecond&&player==1)r(BatchUploadReply.Unknown,"lost second player's reply");
                    else if(RejectUpload)r(BatchUploadReply.Rejected,"server rejection");
                    else{AcceptedSends++;r(BatchUploadReply.Accepted,null);}
                }));
            }
            return new(ops);
        }
        public void RequestReset(){_reset=true;if(Scene==BatchScene.Selection)Clear();}
        private void Clear(){Check(Logs>0,"clear only positive acknowledged log batch");Logs=0;ResetCount++;_reset=false;}
        public void Freeze(bool freeze)=>Frozen=freeze;
        public void End(){Check(Logs==0&&Scene==BatchScene.Selection,"finish only at selection with no pending log");Restored=true;}
        public void Save(BatchJob job){if(FailAcknowledgementSave&&job.Songs.SelectMany(s=>s.Charts).Any(c=>c.Saved))throw new IOException("disk full");}
        public void Advance()
        {
            Now+=.05; // real time is independent of game Scale.
            if(Scene==BatchScene.Opening){Logs++;Scene=BatchScene.Playing;}
            if(Scene==BatchScene.Transition)
            {
                if(_reset){ReadBeforeClear&=_leaving;Clear();}
                Check(_leaving||_abandoning,"return through a native result or retry cleanup");
                Scene=BatchScene.Selection;_leaving=_abandoning=false;
            }
        }
    }
}
