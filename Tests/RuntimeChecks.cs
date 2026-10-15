using SinmaiAssist.Config;
using SinmaiAssist.Cheat;
using SinmaiAssist.Runtime;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Mod=SinmaiAssist.SinmaiAssist;

internal static class RuntimeChecks
{
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    public static void Run()
    {
        string dir=Path.Combine(AppContext.BaseDirectory,"Runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            var serializer=new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).WithTypeConverter(new KeyBindConfig.Converter()).Build();
            var defaults=new KeyBindConfig();
            foreach(var section in new object[]{defaults.AutoPlay,defaults.ChartController,defaults.SpeedHack,defaults.SinmaiAssist})
            foreach(var p in section.GetType().GetProperties()) Check(((KeyBindConfig.Key)p.GetValue(section)).KeyCode==KeyCode.None,"every default is None");
            string keyPath=Path.Combine(dir,"KeyBindConfig.yml");
            File.WriteAllText(keyPath,"autoPlay:\n  critical: F\nactions:\n  skip: G\n");
            var manager=new ConfigManager<KeyBindConfig>(keyPath,new KeyBindConfig.Converter());
            Mod.KeyBindConfig=manager.GetConfig();ConfigManager<KeyBindConfig>.FillDefaults(Mod.KeyBindConfig,new KeyBindConfig());
            Check(Mod.KeyBindConfig.AutoPlay.Critical.KeyCode==KeyCode.F && Mod.KeyBindConfig.AutoPlay.Good.KeyCode==KeyCode.None,"old key retained, new default None");
            Mod.MainConfig=new MainConfig();
            Commands.All.Clear();
            int count=0;
            Commands.Add("first","Test","First",()=>count++);
            Commands.Add("second","Test","Second",()=>count++);
            SettingsStore.Initialize(dir);
            Check(!SettingsStore.Dirty,"new settings start clean");
            Mod.MainConfig.Cheat.SpeedHack.SpeedMultiplier=1.7f;
            Check(SettingsStore.Dirty,"temporary change is dirty");
            var disk=new ConfigManager<RuntimeSettingsDocument>(Path.Combine(dir,"RuntimeSettings.yml")).GetConfig();
            Check(disk.Saved==null,"temporary change is not persisted");
            SettingsStore.NameInput="Practice";SettingsStore.Create();
            string id=SettingsStore.SelectedId;
            Mod.MainConfig.Cheat.SpeedHack.SpeedMultiplier=2.2f;
            Check(SettingsStore.Selected.Settings.Speed==1.7f,"preset is a detached snapshot");
            SettingsStore.ApplySelected();
            Check(Mod.MainConfig.Cheat.SpeedHack.SpeedMultiplier==1.7f,"preset application");
            Commands.SetKey("first","A");SettingsStore.Save();
            Check(!SettingsStore.Dirty,"explicit save clears dirty state");
            Mod.MainConfig.Cheat.SpeedHack.SpeedMultiplier=3;
            SettingsStore.Initialize(dir);
            Check(Mod.MainConfig.Cheat.SpeedHack.SpeedMultiplier==1.7f,"startup restores last manual save");
            Check(SettingsStore.Document.Presets.Single().Id==id,"preset identities survive restart");
            SettingsStore.SelectedId=id; SettingsStore.NameInput="Renamed";SettingsStore.Rename();
            Check(new ConfigManager<RuntimeSettingsDocument>(Path.Combine(dir,"RuntimeSettings.yml")).GetConfig().Presets[0].Name=="Practice","rename is draft until save");
            SettingsStore.Save();
            string original=File.ReadAllText(Path.Combine(dir,"RuntimeSettings.yml"));
            string locked=Path.Combine(dir,"locked");Directory.CreateDirectory(locked);
            bool failed=false;
            try {AtomicConfigFile.WriteMany(new Dictionary<string,string>{{Path.Combine(dir,"RuntimeSettings.yml"),"replacement"},{locked,"fail"}});} catch(IOException){failed=true;}
            Check(failed && File.ReadAllText(Path.Combine(dir,"RuntimeSettings.yml"))==original,"multi-file failure rolls back earlier replacement");
            Directory.Delete(locked);
            File.WriteAllText(Path.Combine(dir,"RuntimeSettings.yml"),"saved:\n  speed: -2\n");
            SettingsStore.Initialize(dir);
            Check(SettingsStore.Document.Saved==null && Directory.GetFiles(dir,"RuntimeSettings.yml.broken.*").Length==1,"invalid runtime values backed up separately");
            Check(File.ReadAllText(keyPath).Contains("critical: F"),"runtime recovery retains shortcut file");

            Commands.SetKey("first","None");Commands.SetKey("second","B");
            var capture=new BindingCapture();
            capture.Click("first",Commands.GetKey,Commands.SetKey);
            Check(capture.Waiting=="first","click None starts capture");
            capture.Click("first",Commands.GetKey,Commands.SetKey);
            Check(capture.Waiting==null && Commands.GetKey("first")=="None","second waiting click cancels");
            capture.Click("first",Commands.GetKey,Commands.SetKey);
            Check(!capture.Accept("B",Commands.All.Keys,Commands.GetKey,Commands.SetKey) && capture.Waiting=="first","duplicate binding rejected");
            Check(capture.Accept("A",Commands.All.Keys,Commands.GetKey,Commands.SetKey),"new key accepted");
            capture.Click("first",Commands.GetKey,Commands.SetKey);
            Check(Commands.GetKey("first")=="None" && capture.Waiting==null,"bound click clears to None");
            capture.Click("first",Commands.GetKey,Commands.SetKey);capture.Accept("Escape",Commands.All.Keys,Commands.GetKey,Commands.SetKey);
            Check(capture.Waiting==null,"Escape cancels capture");
            Commands.SetKey("first","A");
            DebugInput.Down.Add(KeyCode.A);Commands.TextEditing=true;Commands.Update();Check(count==0,"typing suppresses hotkeys");
            Commands.TextEditing=false;Commands.Update();Check(count==1,"key dispatch once per update");DebugInput.Clear();
            Commands.Enqueue("first");Commands.Enqueue("first");Commands.Update();Check(count==2,"duplicate GUI enqueue coalesced");
            Commands.SetKey("second","A");DebugInput.Down.Add(KeyCode.A);Commands.Update();Check(count==2,"imported conflicting key disabled");DebugInput.Clear();
            Check(!Commands.HasUsableKey("first"),"conflicting panel shortcut allows fallback entry");
            Commands.SetKey("first","None");Commands.SetKey("second","None");
            Commands.ClickBinding("first");Commands.Update();DebugInput.Down.Add(KeyCode.B);Commands.Update();
            Check(count==2 && Commands.GetKey("first")=="B","captured key does not execute");
            Commands.Update();Check(count==2,"capture remains suppressed until release");DebugInput.Clear();Commands.Update();
            DebugInput.Down.Add(KeyCode.B);Commands.Update();Check(count==3,"binding executes after release");DebugInput.Clear();

            var loop=new LoopState();
            loop.Configure(1500,2500,10000,2,new[]{new LoopState.Span{Start=1000,End=1800},new LoopState.Span{Start=2400,End=3100},new LoopState.Span{Start=2900,End=3500}});
            Check(loop.A==1000 && loop.B==3500,"long-note boundaries expand transitively");
            loop.Complete(2);loop.Complete(2);Check(loop.Completed==1,"round counted once");
            Check(!loop.Wait(4,true) && loop.WaitRemaining==2,"pause freezes interval");
            Check(!loop.Wait(1,false) && loop.Wait(1,false),"real-time interval completion");
            loop.BeginRound();loop.Complete(0);Check(loop.Finished && !loop.Wait(10,false),"finite count stops at B");
            loop.Configure(0,1000,2000,0,Array.Empty<LoopState.Span>());
            for(int i=0;i<100;i++){loop.Complete(0);Check(!loop.Finished && loop.Wait(0,false),"unlimited repeats");loop.BeginRound();}
            bool rejected=false;try{loop.Configure(2,1,10,0,Array.Empty<LoopState.Span>());}catch(ArgumentException){rejected=true;}
            Check(rejected,"invalid interval rejected");
            MigrationChecks(dir);
            BatchPersistenceChecks(dir);
            ClockChecks();
            Console.WriteLine("PASS: production settings/preset persistence, rollback, recovery, legacy keys, capture/dispatch suppression and loop policies.");
        }
        finally {Directory.Delete(dir,true);}
    }
    private static void MigrationChecks(string root)
    {
        string dir=Path.Combine(root,"migration");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"RuntimeSettings.yml");
        File.WriteAllText(path,"saved:\n  intervalSeconds: 0\npresets:\n  - id: old-zero\n    name: Immediate\n    settings:\n      intervalSeconds: 0\n  - id: old-nonzero\n    name: Rest\n    settings:\n      intervalSeconds: 8\n");
        string old=File.ReadAllText(path);
        SettingsStore.Initialize(dir);
        Check(PracticeLoop.Interval==5 && PracticeLoop.Speed==1 && PracticeLoop.PreservePitch,"legacy zero migrates and practice defaults filled");
        Check(SettingsStore.Document.Presets[0].Settings.IntervalSeconds==5 && SettingsStore.Document.Presets[1].Settings.IntervalSeconds==8,"preset migration retains nonzero rest");
        Check(SettingsStore.Dirty && File.ReadAllText(path)==old,"migration is dirty and does not save automatically");
        PracticeLoop.Interval=0;PracticeLoop.Speed=.75f;PracticeLoop.PreservePitch=false;
        SettingsStore.NameInput="Custom";SettingsStore.SelectedId=null;SettingsStore.Create();
        var preset=SettingsStore.Selected;
        SettingsStore.Save();SettingsStore.Initialize(dir);
        Check(SettingsStore.Document.FormatVersion==3 && !SettingsStore.Dirty && PracticeLoop.Interval==0,"saved version preserves deliberately chosen zero on restart");
        Check(PracticeLoop.Speed==.75f && !PracticeLoop.PreservePitch,"manual save restores independent practice settings");
        PracticeLoop.Speed=1.5f;PracticeLoop.PreservePitch=true;
        SettingsStore.Apply(preset.Settings.Copy());
        Check(PracticeLoop.Speed==.75f && !PracticeLoop.PreservePitch,"preset restores independent practice settings");
        foreach(float speed in new[]{float.NaN,float.PositiveInfinity,.49f,2.01f})
        {
            bool rejected=false;try{new RuntimeSettings{PracticeSpeed=speed}.Validate();}catch(ArgumentException){rejected=true;}
            Check(rejected,"invalid practice speed rejected");
        }
        Check(new RuntimeSettings().IntervalSeconds==5 && new RuntimeSettings().PreservePracticePitch,"new settings default five seconds and original pitch");
    }
    private static void ClockChecks()
    {
        var clock=new RateClock();clock.Reset(0,0,.5);
        Check(clock.Sample(100,.5)==50,"clock integrates half speed");
        Check(clock.Sample(100,2)==50 && clock.Sample(200,2)==250,"rate change scales future delta only");
        Check(clock.Sample(200,1)==250 && clock.Sample(300,1)==350,"return to 1x retains accumulated offset");
        Check(clock.Sample(300,1)==350,"paused raw timer freezes position");
        clock.Reset(0,1500,2);
        Check(clock.Sample(100,2)==1700,"seek anchors full content position");
        for(int i=0;i<10000;i++)clock.Sample(100+i*.125,2);
        Check(Math.Abs(clock.Position-(1500+2*(100+9999*.125)))<1e-8,"double precision integration does not accumulate frame rounding");
    }
    private static void BatchPersistenceChecks(string root)
    {
        string dir=Path.Combine(root,"batch");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"RuntimeSettings.yml");
        File.WriteAllText(path,"formatVersion: 2\nsaved:\n  intervalSeconds: 0\npresets:\n  - id: v2\n    name: Zero\n    settings:\n      intervalSeconds: 0\n");
        SettingsStore.Initialize(dir);
        Check(PracticeLoop.Interval==0&&SettingsStore.Document.Presets[0].Settings.IntervalSeconds==0,"version 2 zero is not migrated again");
        Check(BatchController.Settings.SongCount==10&&!BatchController.Settings.Boost&&SettingsStore.Dirty,"version 2 adds default batch settings in memory");
        BatchController.Settings.SongCount=23;BatchController.Settings.Profiles[0].Outcomes[0].Weight=45;
        SettingsStore.NameInput="Batch preset";SettingsStore.SelectedId=null;SettingsStore.Create();var preset=SettingsStore.Selected;
        BatchController.Settings.Profiles[0].Outcomes[0].Weight=99;
        Check(preset.Settings.Batch.Profiles[0].Outcomes[0].Weight==45,"preset deep copy isolates batch probability edits");
        SettingsStore.Save();SettingsStore.Initialize(dir);
        Check(BatchController.Settings.SongCount==23&&BatchController.Settings.Profiles[0].Outcomes[0].Weight==99,"manual batch save restored after restart");
        Check(!SettingsStore.Dirty,"restored batch clean");BatchController.Settings.Profiles[0].Outcomes[0].Weight++;
        Check(SettingsStore.Dirty,"nested probability edits make settings dirty");
        SinmaiAssist.GUI.BatchPanel.InputValid=false;bool rejected=false;
        try{SettingsStore.Save();}catch(InvalidOperationException){rejected=true;}
        finally{SinmaiAssist.GUI.BatchPanel.InputValid=true;}
        Check(rejected,"invalid uncommitted batch text prevents save");
    }
}
