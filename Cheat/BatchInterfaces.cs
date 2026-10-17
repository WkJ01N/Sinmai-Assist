using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SinmaiAssist.Cheat;

// Shared by the runtime preflight and metadata-only tests. Optional parameters
// still belong to the CLR signature and must be supplied to reflection calls.
internal sealed class BatchMethod
{
    public readonly string TypeName,Name;
    public readonly string[] Parameters;
    public BatchMethod(string typeName,string name,params string[] parameters)
    {TypeName=typeName;Name=name;Parameters=parameters;}
    public override string ToString()=>TypeName+"."+Name+"("+string.Join(", ",Parameters)+")";
    public MethodInfo Resolve(Func<string,Type> resolveType)
    {
        Type Resolve(string name)=>resolveType(name)??throw new TypeLoadException("Missing native type: "+name+" required by "+this);
        return Require(Resolve(TypeName),Name,Parameters.Select(Resolve).ToArray());
    }
    public static MethodInfo Require(Type type,string name,params Type[] parameters)
    {
        for(var current=type;current!=null;current=current.BaseType)
        {
            var method=current.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                .SingleOrDefault(m=>m.Name==name&&!m.IsGenericMethod&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(parameters));
            if(method!=null)return method;
        }
        throw new MissingMethodException("Missing native method: "+type.FullName+"."+name+"("+string.Join(", ",parameters.Select(p=>p.FullName))+")");
    }
}

internal static class BatchInterfaces
{
    public static readonly BatchMethod DeployList=new BatchMethod("Monitor.MusicSelectMonitor","SetDeployList","System.Boolean","System.Boolean");
    public static readonly BatchMethod TournamentFolder=new BatchMethod("Process.MusicSelectProcess","IsTournamentFolder","System.String");
    public static readonly BatchMethod[] FlowMethods={
        new BatchMethod("Process.MusicSelectProcess","OnGameStart"),
        new BatchMethod("Process.ResultProcess","ToNextCheck"),
        new BatchMethod("Process.NextTrackProcess","ProcessingProcess"),
        new BatchMethod("Manager.GamePlayManager","RemoveScoreList","System.Int32"),
        new BatchMethod("Manager.GamePlayManager","GetScoreListCount"),
        new BatchMethod("Manager.GamePlayManager","GetGameScore","System.Int32","System.Int32"),
        new BatchMethod("Manager.GamePlayManager","ClaerLog"),
        new BatchMethod("Process.MusicSelectProcess","GetCurrentDifficulty","System.Int32"),
        new BatchMethod("Process.MusicSelectProcess","GetUserScore","System.Int32","System.Int32","System.Int32"),
        new BatchMethod("Process.MusicSelectProcess","CalcMonitorDifficulty","System.Boolean"),
        new BatchMethod("Process.MusicSelectProcess","IsPlayableMusic","System.Int32"),
        new BatchMethod("Process.MusicSelectProcess","TimerCountUp"),
        new BatchMethod("Process.MusicSelectProcess","IsTournamentFolder","System.Int32"),
        TournamentFolder,DeployList,
        new BatchMethod("Monitor.NextTrackMonitor","SetNextTrack","System.UInt32"),
        new BatchMethod("Manager.UserData","IsAimeUser"),
        new BatchMethod("Process.UnlockProcess","OnUpdate"),
        new BatchMethod("Process.MusicSelectProcess+CombineMusicSelectData","GetID","MAI2System.ConstParameter+ScoreKind")
    };
    public static readonly BatchMethod[] UploadMethods={
        new BatchMethod("Net.Packet.Mai2.PacketUpsertUserAll","ToUserAll","System.Int32","Manager.UserData"),
        new BatchMethod("Net.Packet.Mai2.PacketUpsertUserAll","Convert","System.Int32","Manager.UserData","System.Int32"),
        new BatchMethod("Net.Packet.Helper.PacketHelper","StartPacket","Net.Packet.Packet"),
        new BatchMethod("Monitor.Entry.Parts.Screens.DoneEntry","IsWeekdayBonus","Manager.UserData")
    };
    public static readonly Dictionary<string,string[]> Members=new Dictionary<string,string[]>{
        ["Process.MusicSelectProcess"]=new[]{"container","_mainSequence","CombineMusicDataList","CategoryNameList","CurrentCategorySelect","CurrentMusicSelect","ScoreType","DifficultySelectIndex","CurrentDifficulty","MonitorArray","TrialPlayIndex"},
        ["Process.NextTrackProcess"]=new[]{"isAddAnim","isAddProcess","_monitors","container","toProcess","_mode"},
        ["Process.RetryWaitProcess"]=new[]{"_state","container"},
        ["Manager.ProcessManager"]=new[]{"_processList"},
        ["Manager.ProcessManager+ProcessControle"]=new[]{"State","Process"},
        ["Process.UnlockProcess"]=new[]{"_state"},
        ["Manager.GameManager"]=new[]{"TempMaxTrackCount","MusicTrackNumber","NextMapSelect","NextCharaSelect","IsFreedomTimerPause"},
        ["Process.MusicSelectProcess+CombineMusicSelectData"]=new[]{"musicSelectData"},
        ["Process.MusicSelectProcess+MusicSelectData"]=new[]{"MusicData","ScoreData","Difficulty","isEnable","isExistsScore"}
    };
}
