using System;
using System.Collections.Generic;
using System.Linq;

namespace SinmaiAssist.Cheat;

public sealed class BatchUploadStep
{
    public string Id { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public int TotalAttempts { get; set; }
    public string Error { get; set; }
}
internal enum BatchUploadReply { Accepted, Rejected, Unknown }
internal sealed class BatchUploadOperation
{
    public BatchUploadStep Receipt;
    public Action<Action<BatchUploadReply,string>> Send;
}
// Serial requests and immutable payload factories make partial two-player
// acknowledgements explicit. A lost response is never treated as a rejection.
internal sealed class BatchUpload
{
    public readonly List<BatchUploadOperation> Operations;
    private readonly double _timeout;
    private double _started,_next,_now;
    private bool _inFlight;
    private int _generation;
    public bool Blocked { get; private set; }
    public string Error { get; private set; }
    public bool Done => Operations.All(o=>o.Receipt.State=="Accepted");
    public BatchUpload(List<BatchUploadOperation> operations,double timeout=90){Operations=operations;_timeout=timeout;}
    public void Tick(double now,bool allowSend=true)
    {
        _now=now;
        if(Blocked||Done)return;
        var operation=Operations.First(o=>o.Receipt.State!="Accepted");
        if(_inFlight)
        {
            if(now-_started>_timeout){_generation++;_inFlight=false;operation.Receipt.State="Unknown";Block("Upload response timed out; outcome is unknown.");}
            return;
        }
        if(!allowSend||now<_next)return;
        _inFlight=true;_started=now;operation.Receipt.Attempts++;operation.Receipt.TotalAttempts++;operation.Receipt.State="Sending";
        int generation=++_generation;
        try
        {
            operation.Send((reply,error)=> {
                if(generation!=_generation)return;
                _inFlight=false;operation.Receipt.Error=error;
                if(reply==BatchUploadReply.Accepted){operation.Receipt.State="Accepted";return;}
                operation.Receipt.State=reply==BatchUploadReply.Rejected?"Rejected":"Unknown";
                if(reply==BatchUploadReply.Unknown || operation.Receipt.Attempts>=3)Block(error??"Upload failed.");
                else _next=_now+operation.Receipt.Attempts;
            });
        }
        catch(Exception ex){_inFlight=false;operation.Receipt.State="Unknown";operation.Receipt.Error=ex.Message;Block(ex.Message);}
    }
    private void Block(string error){Blocked=true;Error=error;}
    public void RetryRejected()
    {
        if(Operations.Any(o=>o.Receipt.State=="Unknown"))throw new InvalidOperationException("Unknown upload outcome: inspect server records before confirming or retrying explicitly.");
        foreach(var op in Operations.Where(o=>o.Receipt.State=="Rejected"))op.Receipt.Attempts=0;
        Blocked=false;Error=null;
    }
    public void ResolveUnknown(bool confirmedSaved)
    {
        var op=Operations.FirstOrDefault(o=>o.Receipt.State=="Unknown");
        if(op==null)throw new InvalidOperationException("No unknown upload to resolve.");
        op.Receipt.State=confirmedSaved?"Accepted":"Pending";op.Receipt.Attempts=0;
        Blocked=Operations.Any(o=>o.Receipt.State=="Unknown");Error=null;_generation++;
    }
}
