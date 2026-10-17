using SinmaiAssist.Runtime;

internal static class AudioChecks
{
    private static void Check(bool success,string message){if(!success)throw new Exception("Mute: "+message);}
    public static void Run()
    {
        MuteState.Apply(false,false,false,false,false,false,false);
        Check(!MuteState.Master&&Enum.GetValues<SoundGroup>().All(g=>!MuteState.Get(g)),"old configuration defaults leave every category audible");
        var expected=new Dictionary<SoundGroup,IEnumerable<int>>{
            [SoundGroup.Music]=new[]{2},[SoundGroup.MenuMusic]=new[]{3,7,42},
            [SoundGroup.Voices]=new[]{4,5,6,8,9,43,44},[SoundGroup.Notifications]=new[]{0,1,10,41,45},
            [SoundGroup.MenuEffects]=Enumerable.Range(11,6).Concat(Enumerable.Range(46,6)),
            [SoundGroup.GameEffects]=Enumerable.Range(17,24).Concat(Enumerable.Range(52,24))};
        foreach(var pair in expected)foreach(int player in pair.Value)Check(MuteState.Classify(player)==pair.Key,"native channel "+player);
        Check(expected.Values.SelectMany(v=>v).Order().SequenceEqual(Enumerable.Range(0,76)),"all native player slots are accounted for");
        int notifications=0;void Changed()=>notifications++;MuteState.Changed+=Changed;
        MuteState.Apply(true,true,false,true,false,false,true);
        Check(notifications==1,"preset applies seven flags atomically");
        Check(Enum.GetValues<SoundGroup>().All(MuteState.IsMuted),"master covers every category");
        MuteState.Master=false;
        Check(MuteState.IsMuted(SoundGroup.Music)&&!MuteState.IsMuted(SoundGroup.Voices),"master release retains category settings");
        MuteState.Changed-=Changed;MuteState.Apply(false,false,false,false,false,false,false);
        var volume=new MutedVolume(.35f);
        Check(volume.Output(true)==0&&volume.Output(false)==.35f,"mute preserves initial game volume");
        Check(volume.Write(.65f,true)==0&&volume.Output(false)==.65f,"writes while muted update intended volume");
        Check(volume.Reset(true)==0&&volume.Output(false)==1,"parameter reset changes intended volume without unmuting");
        SessionChecks();
        Console.WriteLine("PASS: all 76 audio slots, atomic mute settings, volume restoration, PID isolation, new/changed sessions and retryable failures.");
    }
    private static void SessionChecks()
    {
        var manager=new AudioSessionMute();
        var game=new Session("device1:game",10);var alreadyMuted=new Session("device1:muted",10){Value=true};var other=new Session("device1:other",20);
        manager.Update(true,10,new[]{game,alreadyMuted,other});
        Check(game.Value&&alreadyMuted.Value&&!other.Value&&other.Writes==0&&manager.Matched==2,"only this process is muted");
        var secondDevice=new Session("device2:game",10);
        manager.Update(true,10,new[]{secondDevice});
        Check(secondDevice.Value,"new endpoint/session is muted");
        manager.Update(false,10,new[]{secondDevice,alreadyMuted,other});
        Check(!secondDevice.Value&&alreadyMuted.Value&&other.Writes==0&&manager.NeedsRestore,"restore baseline and retain a missing session for retry");
        manager.Update(false,10,new[]{game});
        Check(!game.Value&&!manager.NeedsRestore,"reappearing old endpoint restores correctly");
        manager.Update(true,10,new[]{game});game.FailWrite=true;
        manager.Update(false,10,new[]{game});
        Check(manager.Error!=null&&game.Value&&manager.NeedsRestore,"failed restoration is reported and retryable");
        game.FailWrite=false;manager.Update(false,10,new[]{game});
        Check(manager.Error==null&&!game.Value&&!manager.NeedsRestore,"later restore succeeds");
        manager.Update(true,10,new[]{alreadyMuted});alreadyMuted.Value=false;
        manager.Update(false,10,new[]{alreadyMuted});
        Check(!alreadyMuted.Value,"release does not reverse an external unmute");
        Check(game.Disposals>0&&other.Disposals>0,"COM-boundary wrappers always released, including foreign sessions");
    }
    private sealed class Session : IGameAudioSession
    {
        public string Id {get;}
        public uint ProcessId {get;}
        public bool Value,FailWrite;
        public int Writes,Disposals;
        public Session(string id,uint process){Id=id;ProcessId=process;}
        public bool Muted {get=>Value;set {if(FailWrite)throw new Exception("device unavailable");Value=value;Writes++;}}
        public void Dispose()=>Disposals++;
    }
}
