using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MAI2.Util;
using Manager;
using Manager.UserDatas;
using MAI2System;
using DB;

namespace SinmaiAssist.Cheat;

internal static class BatchPackets
{
    private const string AllType="Net.Packet.Mai2.PacketUpsertUserAll",LogType="Net.Packet.Mai2.PacketUploadUserPlaylog";
    private static readonly System.Type All=AccessTools.TypeByName(AllType);
    private static readonly System.Type Log=AccessTools.TypeByName(LogType);
    private static readonly System.Type Helper=AccessTools.TypeByName("Net.Packet.Helper.PacketHelper");
    private static ConstructorInfo AllConstructor=>RequireConstructor(All,AllType,typeof(int),typeof(ulong),typeof(Net.VO.Mai2.UserAll),
        typeof(List<Net.VO.Mai2.UserPlaylog>),typeof(Action<int>),typeof(Action<Net.Packet.PacketStatus>));
    private static ConstructorInfo LogConstructor=>RequireConstructor(Log,LogType,typeof(ulong),typeof(Net.VO.Mai2.UserPlaylog),
        typeof(Action<int>),typeof(Action<Net.Packet.PacketStatus>));
    private static ConstructorInfo RequireConstructor(System.Type type,string typeName,params System.Type[] parameters)=>type?.GetConstructor(parameters)??
        throw new MissingMethodException("Missing native constructor: "+typeName+"("+string.Join(", ",parameters.Select(p=>p.FullName))+")");
    public static void Check()
    {
        _=AllConstructor;_=LogConstructor;
        foreach(var requirement in BatchInterfaces.UploadMethods)requirement.Resolve(AccessTools.TypeByName);
        if(typeof(UserRate).GetConstructor(new[]{typeof(int),typeof(int),typeof(uint),typeof(uint)})==null)
            throw new MissingMethodException("Missing native constructor: Manager.UserDatas.UserRate(System.Int32, System.Int32, System.UInt32, System.UInt32)");
    }
    public static BatchUpload Capture(string jobId,int transaction,int count,string[] users)
    {
        Check();var operations=new List<BatchUploadOperation>();
        for(int player=0;player<2;player++)
        {
            if(users[player]==null)continue;
            var user=Singleton<UserDataManager>.Instance.GetUserData(player);
            PrepareUser(user);
            // The native converters produce VO snapshots. Reuse those exact
            // objects on a rejected-request retry, never export live data again.
            var all=BatchMembers.Call(All,"ToUserAll",player,user);
            var logs=BatchMembers.Call(All,"Convert",player,user,count);
            ulong userId=ulong.Parse(users[player]);int index=0;
            foreach(var log in (IEnumerable)logs)
                operations.Add(Operation(jobId+":"+transaction+":P"+player+":Log"+index++,LogConstructor,new[]{(object)userId,log}));
            operations.Add(Operation(jobId+":"+transaction+":P"+player+":All",AllConstructor,new[]{(object)player,userId,all,logs}));
        }
        return new BatchUpload(operations);
    }
    // Prepare the fields normally finalized by SDGB DataSaveProcess, while
    // retaining the active login and its credit/play-count lifecycle.
    private static void PrepareUser(UserData user)
    {
        var detail=user.Detail;
        detail.EventWatchedDate=TimeManager.GetDateString(TimeManager.PlayBaseTime);
        user.CalcTotalValue();
        var rating=user.RatingList;
        float Border(List<UserRate> rates)=> (float)Math.Ceiling(((rates.Count==0?0:rates.Last().SingleRate)+1f)/GameManager.TheoryRateBorderNum*10d);
        float oldBorder=Border(rating.RatingList),newBorder=Border(rating.NewRatingList);
        var oldCandidates=new List<UserRate>();var newCandidates=new List<UserRate>();
        for(int d=0;d<user.ScoreDic.Length;d++)
        {
            if(user.ScoreDic[d]==null)continue;
            foreach(var pair in user.ScoreDic[d])
            {
                if(pair.Value.achivement>=RatingTableID.Rate_22.GetAchive())continue;
                var music=Singleton<DataManager>.Instance.GetMusic(pair.Key);if(music==null)continue;
                var candidate=new UserRate(pair.Key,d,pair.Value.achivement,(uint)music.version);
                if(candidate.OldFlag)
                {if(candidate.Level>=oldBorder&&!rating.RatingList.Contains(candidate))oldCandidates.Add(candidate);}
                else if(candidate.Level>=newBorder&&!rating.NewRatingList.Contains(candidate))newCandidates.Add(candidate);
            }
        }
        oldCandidates.Sort();oldCandidates.Reverse();newCandidates.Sort();newCandidates.Reverse();
        rating.NextRatingList=oldCandidates.Take(20).ToList();rating.NextNewRatingList=newCandidates.Take(20).ToList();
        string day=TimeManager.GetLogDateString(TimeManager.PlayBaseTime);
        string last=string.IsNullOrEmpty(detail.DailyBonusDate)?TimeManager.GetLogDateString(0L):detail.DailyBonusDate;
        if(detail.IsNetMember>=2&&TimeManager.GetUnixTime(day)>TimeManager.GetUnixTime(last)&&
            Singleton<UserDataManager>.Instance.IsSingleUser()&&!GameManager.IsFreedomMode&&
            !(bool)BatchMembers.Call(AccessTools.TypeByName("Monitor.Entry.Parts.Screens.DoneEntry"),"IsWeekdayBonus",user))detail.DailyBonusDate=day;
        detail.LastPlayMode=GameManager.IsFreedomMode?1:0;
        detail.LastGameId=BatchMembers.Get(typeof(AMDaemon.System),"GameId").ToString();
        detail.LastRomVersion=Singleton<SystemConfig>.Instance.config.romVersionInfo.versionNo.versionString;
        detail.LastDataVersion=Singleton<SystemConfig>.Instance.config.dataVersionInfo.versionNo.versionString;
    }
    private static BatchUploadOperation Operation(string id,ConstructorInfo constructor,object[] snapshot)
    {
        return new BatchUploadOperation{Receipt=new BatchUploadStep{Id=id},Send=reply=>{
            var args=new object[snapshot.Length+2];Array.Copy(snapshot,args,snapshot.Length);
            args[snapshot.Length]=new Action<int>(code=>reply(code==1?BatchUploadReply.Accepted:BatchUploadReply.Rejected,"Server returnCode="+code));
            var errorType=constructor.GetParameters().Last().ParameterType.GetGenericArguments()[0];
            args[snapshot.Length+1]=typeof(BatchPackets).GetMethod(nameof(ErrorCallback),BindingFlags.NonPublic|BindingFlags.Static)
                .MakeGenericMethod(errorType).Invoke(null,new object[]{reply});
            object packet;
            try{packet=constructor.Invoke(args);}
            catch(Exception ex){reply(BatchUploadReply.Rejected,"Request was not queued: "+(ex.InnerException??ex).Message);return;}
            BatchMembers.Call(Helper,"StartPacket",packet);
        }};
    }
    private static Action<T> ErrorCallback<T>(Action<BatchUploadReply,string> reply)=>error=>reply(BatchUploadReply.Unknown,"Network outcome unknown: "+error);
}
