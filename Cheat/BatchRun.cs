using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SinmaiAssist.Cheat;

internal enum BatchScene { Selection, Opening, Playing, Result, Transition, Unknown }
internal interface IBatchRuntime
{
    double Now { get; }
    BatchScene Scene { get; }
    string[] Users { get; }
    int PendingLogs { get; }
    int Limit { get; }
    void Begin(BatchJob job);
    bool AlreadyMet(BatchSong song,BatchChart chart);
    void Open(BatchSong song,BatchChart chart);
    Task<object> Generate(BatchSong song,BatchChart chart,CancellationToken token);
    void Commit(object result);
    void VerifyResult(BatchSong song,BatchChart chart);
    void Abandon();
    void LeaveResult();
    BatchUpload CaptureUpload();
    void RequestReset();
    bool ResetDone { get; }
    void Freeze(bool freeze);
    void End();
    void Save(BatchJob job);
}
internal sealed class BatchRun
{
    private readonly IBatchRuntime _host;
    public BatchJob Job { get; private set; }
    public bool Active { get; private set; }
    public bool Paused { get; private set; }
    public string Status { get; private set; }="Idle";
    public string Stage { get; private set; }="Idle";
    public BatchUpload Upload { get; private set; }
    private Task<object> _generation;
    private CancellationTokenSource _cancel;
    private BatchSong _song;
    private BatchChart _chart;
    private bool _stopping,_needsReset,_resultLeft;
    private double _phaseStart,_retryAt;
    private string _receipts;
    public BatchRun(IBatchRuntime host){_host=host;}
    public void Start(BatchJob job)
    {
        if(Active)throw new InvalidOperationException("A batch is already active.");
        job.Settings.Validate();
        if(job.Users==null || !job.Users.SequenceEqual(_host.Users))throw new InvalidOperationException("Batch users differ from the current login.");
        if(job.Uploads.Any(u=>u.State!="Accepted"&&u.State!="Reviewed"))throw new InvalidOperationException("Previous upload has an unconfirmed outcome. Check server records before starting a new batch.");
        Job=job;job.Finished=false;_stopping=false;Paused=false;Upload=null;_generation=null;_retryAt=0;_needsReset=_resultLeft=false;
        _host.Begin(job);Active=true;SetStage("Select");
    }
    public void Tick()
    {
        if(!Active)return;
        try
        {
            bool usersMatch=Job.Users.SequenceEqual(_host.Users);
            if(Upload!=null && !Upload.Done)
            {
                Upload.Tick(_host.Now,!Paused&&usersMatch);
                string receipts=string.Join(";",Upload.Operations.Select(o=>o.Receipt.State+o.Receipt.Attempts));
                if(receipts!=_receipts){_receipts=receipts;_host.Save(Job);}
                if(Upload.Blocked){Pause(Upload.Error);return;}
            }
            if(!usersMatch){Pause("Login or entered players changed. Restore the original users to continue.");return;}
            if(Paused)return;
            if(_host.Scene==BatchScene.Unknown){Pause("Unknown game scene; batch paused without taking further action.");return;}
            if(_host.Now-_phaseStart>90 && Stage!="Upload"){Pause("Batch timed out in "+Stage+"; inspect the current scene.");return;}
            switch(Stage)
            {
                case "Select": Select();break;
                case "Opening":
                    if(_host.Scene==BatchScene.Playing)
                    {
                        _host.Freeze(true);
                        if(_stopping){_host.Abandon();SetStage("Return");}
                        else BeginGenerate();
                    }
                    else if(_host.Scene==BatchScene.Selection && _stopping)FinishOrUpload();
                    break;
                case "Generate":
                    if(_stopping){CancelGeneration();_host.Abandon();SetStage("Return");break;}
                    if(_generation==null){if(_host.Now>=_retryAt)BeginGenerate();break;}
                    if(!_generation.IsCompleted)break;
                    var task=_generation;_generation=null;_cancel?.Dispose();_cancel=null;
                    try {var result=task.GetAwaiter().GetResult();_host.Commit(result);SetStage("Result");}
                    catch(BatchChartException ex){ChartFailure(ex.Message);}
                    break;
                case "Result":
                    if(_host.Scene!=BatchScene.Result)break;
                    _host.VerifyResult(_song,_chart);
                    _chart.Outcome="Success";Notice("Success: "+_song.Name+" "+string.Join("/",_chart.Difficulty));
                    FinishOrUpload();break;
                case "Upload":
                    if(Upload==null||!Upload.Done)break;
                    foreach(var chart in Job.Songs.SelectMany(s=>s.Charts).Where(c=>c.Outcome=="Success"))chart.Saved=true;
                    _host.Save(Job); // Persist acknowledgements before clearing native logs.
                    _host.RequestReset();_needsReset=true;Upload=null;_host.Save(Job);
                    GoBack();break;
                case "Return":
                    if(_host.Scene!=BatchScene.Selection || (_needsReset&&!_host.ResetDone))break;
                    _needsReset=false;_resultLeft=false;
                    if(_stopping || Job.Songs.All(s=>s.Done))FinishOrUpload();else SetStage("Select");
                    break;
            }
        }
        catch(Exception ex){Pause(ex.Message);}
    }
    private void Select()
    {
        if(_host.Scene!=BatchScene.Selection)return;
        if(_host.Now<_retryAt)return;
        if(_host.PendingLogs>=_host.Limit){FinishOrUpload();return;}
        if(_stopping||Job.Songs.All(s=>s.Done)){FinishOrUpload();return;}
        _song=Job.Songs.First(s=>!s.Done);_chart=_song.Charts.First(c=>!c.Done);
        if((Job.Settings.SkipExisting||_chart.VerifyHistory)&&_host.AlreadyMet(_song,_chart))
        {_chart.Outcome="Already met";Notice("Already met: "+_song.Name);return;}
        if(_chart.Attempts>=3)
        {_chart.Outcome="Failed";_chart.Error="Three attempts exhausted before restart.";Notice(_chart.Error);return;}
        try{_host.Open(_song,_chart);SetStage("Opening");}
        catch(BatchChartException ex){ChartFailure(ex.Message,true);}
    }
    private void BeginGenerate()
    {
        if(_chart.Attempts>=3){ChartFailure("Three attempts exhausted.");return;}
        _chart.Attempts++;Notice("Attempt "+_chart.Attempts+": "+_song.Name);
        _cancel=new CancellationTokenSource();
        try{_generation=_host.Generate(_song,_chart,_cancel.Token);SetStage("Generate");}
        catch(BatchChartException ex){_cancel.Dispose();_cancel=null;ChartFailure(ex.Message);}
        catch{_cancel.Dispose();_cancel=null;throw;}
    }
    private void ChartFailure(string error,bool unopened=false)
    {
        if(unopened)_chart.Attempts++;
        _chart.Error=error;Notice("Attempt failed: "+error);
        if(_chart.Attempts<3)
        {
            _retryAt=_host.Now+_chart.Attempts;
            if(unopened)SetStage("Select");else SetStage("Generate");
            return;
        }
        _chart.Outcome="Failed";_host.Save(Job);
        if(unopened)SetStage("Select");else{_host.Abandon();SetStage("Return");}
    }
    private void FinishOrUpload()
    {
        bool finished=_stopping||Job.Songs.All(s=>s.Done);
        if(_host.PendingLogs>0 && (_host.PendingLogs>=_host.Limit || finished))
        {
            Upload=_host.CaptureUpload();
            _host.Freeze(true);
            Job.Uploads.AddRange(Upload.Operations.Select(o=>o.Receipt));_receipts=null;
            SetStage("Upload");return;
        }
        if(finished && _host.Scene==BatchScene.Selection){Finish();return;}
        GoBack();
    }
    private void GoBack()
    {
        if(_host.Scene==BatchScene.Result&&!_resultLeft){_host.LeaveResult();_resultLeft=true;}
        SetStage("Return");
    }
    private void SetStage(string stage){Stage=stage;_phaseStart=_host.Now;Status=stage;_host.Freeze(stage=="Generate"||stage=="Upload"||Paused);_host.Save(Job);}
    private void Notice(string text)
    {
        Status=text;Job.Events.Add(DateTime.UtcNow.ToString("o")+" "+text);
        _host.Save(Job);
    }
    public void Pause(string reason)
    {
        if(Paused&&Status==reason)return;Paused=true;_host.Freeze(true);
        try{Notice(reason);}catch(Exception ex){Status=reason+" / progress could not be saved: "+ex.Message;}
    }
    public void TogglePause()
    {
        if(!Active)throw new InvalidOperationException("No active batch.");
        if(!Paused){Pause("Paused by user.");return;}
        if(!Job.Users.SequenceEqual(_host.Users))throw new InvalidOperationException("Restore the original users first.");
        if(Upload?.Blocked==true)Upload.RetryRejected();
        Paused=false;_phaseStart=_host.Now;Status=Stage;
        _host.Freeze(Stage=="Generate"||Stage=="Upload");_host.Save(Job);
    }
    public void ResolveUpload(bool saved)
    {
        if(Upload==null)throw new InvalidOperationException("No upload transaction.");
        Upload.ResolveUnknown(saved);_host.Save(Job);
    }
    public void Stop()
    {
        if(!Active)return;
        _stopping=true;
        // Unknown uploads still require an explicit resolution; never drop data.
        if(Upload?.Blocked==true){Pause("Stop requested; resolve pending upload before cleanup.");return;}
        Paused=false;_phaseStart=_host.Now;Notice("Stopping at a safe scene boundary.");
        _host.Freeze(Stage=="Generate"||Stage=="Upload");
    }
    private void Finish()
    {
        Active=false;
        try{_host.End();}catch{Active=true;throw;}
        Paused=false;Job.Finished=true;Stage="Finished";Notice("Batch finished. Returned to song selection.");
    }
    private void CancelGeneration()
    {
        _cancel?.Cancel();_cancel?.Dispose();_cancel=null;
        if(_generation!=null)_=_generation.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        _generation=null;
    }
    public void Shutdown()
    {
        CancelGeneration();
        if(!Active)return;
        try{Notice("Game closed; resume requires explicit review.");}
        catch(Exception ex){Status="Could not save batch progress on exit: "+ex.Message;}
        finally{Active=false;}
    }
}
internal static class BatchRecovery
{
    public static bool NeedsReview(BatchJob job)=>job.Uploads.Any(u=>u.State!="Accepted"&&u.State!="Reviewed") ||
        job.Songs.SelectMany(s=>s.Charts).Any(c=>c.Outcome=="Success"&&!c.Saved);
    public static void Review(BatchJob job,string[] users,int pendingLogs)
    {
        if(!job.Users.SequenceEqual(users))throw new InvalidOperationException("Restore the saved job's original players first.");
        if(pendingLogs!=0)throw new InvalidOperationException("Recovery review requires a fresh song selection with no pending native logs. Keep the current game open if its upload is still unresolved.");
        foreach(var receipt in job.Uploads.Where(u=>u.State!="Accepted"))receipt.State="Reviewed";
        foreach(var chart in job.Songs.SelectMany(s=>s.Charts).Where(c=>c.Outcome=="Success"&&!c.Saved))
        {chart.Outcome=null;chart.VerifyHistory=true;}
        job.Events.Add(DateTime.UtcNow.ToString("o")+" User reviewed prior server outcomes after restart. Recheck unsaved tasks against current native history; never resend old payloads.");
    }
}
internal sealed class BatchChartException : Exception {public BatchChartException(string message):base(message){}}
