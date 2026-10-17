using System;
using System.Reflection;
using MAI2.Util;
using Manager;
using SinmaiAssist.Cheat;
using SinmaiAssist.Config;
using SinmaiAssist.GUI;
using SinmaiAssist.Utils;

namespace SinmaiAssist.Runtime;

internal static class GameActions
{
    private static string Enabled(bool enabled,string name) => SinmaiAssist.MainConfig.ModSetting.SafeMode ? "Unavailable in safe mode." :
        !enabled ? name + " module is disabled in Config.yml." : null;
    private static string Auto() => Enabled(SinmaiAssist.MainConfig.Cheat.AutoPlay,"AutoPlay");
    private static string Chart() => Enabled(SinmaiAssist.MainConfig.Cheat.ChartController,"ChartController") ?? PlaySession.RequirePlay();
    private static string Skip() => Enabled(SinmaiAssist.MainConfig.Cheat.FastSkip,"FastSkip") ?? PlaySession.RequirePlay() ??
        (PracticeLoop.Session || RankAutoPlay.IsBusy || FastSkip.IsBusy ? "Another score operation or practice is active." : null);
    private static string Native() => Enabled(true,"");
    private static string UserReady(int player)
    {
        string error=Native();if(error!=null)return error;
        var user=Singleton<UserDataManager>.Instance.GetUserData(player);
        return user?.IsEntry==true && user.Detail!=null ? null : $"P{player+1} user data is not ready.";
    }
    private static string AnyUserReady() => Native() ?? (UserReady(0)==null || UserReady(1)==null ? null : "No entered player data is ready.");
    private static void Add(string id,string group,string label,Action action,Func<string> guard=null) => Commands.Add(id,group,label,action,guard);
    private static void Legacy(string id,string group,string label,Action action,object section,string property,Func<string> guard=null,bool hold=false)
    {
        PropertyInfo p=section.GetType().GetProperty(property);
        Commands.Add(id,group,label,action,guard,()=> (KeyBindConfig.Key)p.GetValue(section),key=>p.SetValue(section,key),hold);
    }
    public static void Initialize()
    {
        var c=SinmaiAssist.MainConfig; var k=SinmaiAssist.KeyBindConfig;
        k.Actions ??= new System.Collections.Generic.Dictionary<string,KeyBindConfig.Key>();
        Legacy("panel","Display","Toggle main panel",()=>c.ModSetting.ShowPanel=!c.ModSetting.ShowPanel,k.SinmaiAssist,"ShowUserPanel");
        Add("info","Display","Toggle Show Info",()=>c.ModSetting.ShowInfo=!c.ModSetting.ShowInfo);
        Add("fps","Display","Toggle Show FPS",()=>c.Common.ShowFPS=!c.Common.ShowFPS);
        Add("maskServer","Display","Mask server URL",()=>c.ModSetting.MaskTitleServerUrl=!c.ModSetting.MaskTitleServerUrl);
        foreach(var name in new[]{"AutoPlay","FastSkip","Batch","ChartController","DummyLogin","UserData","Graphic","Mute","Debug","Settings","Hotkeys"})
        {
            string tab=name;Add("panel."+name,"Navigation","Open "+name,()=>MainGUI.Open(tab));
        }
        Add("save","Settings","Save settings, presets and shortcuts",SettingsStore.Save);
        Add("mute.all","Audio","Mute whole game",()=>MuteState.Master=!MuteState.Master,Native);
        foreach(SoundGroup value in Enum.GetValues(typeof(SoundGroup)))
        {
            var group=value;
            Add("mute."+group,"Audio","Mute "+MutePanel.Labels[(int)group],()=>MuteState.Set(group,!MuteState.Get(group)),()=>Native()??(AudioMute.Installed?null:"Category mute hooks are unavailable."));
        }
        Add("preset.create","Settings","Create preset",SettingsStore.Create);
        Add("preset.overwrite","Settings","Overwrite selected preset",SettingsStore.Overwrite);
        Add("preset.rename","Settings","Rename selected preset",SettingsStore.Rename);
        Add("preset.delete","Settings","Delete selected preset",SettingsStore.Delete);
        Add("preset.apply","Settings","Apply selected preset",SettingsStore.ApplySelected);
        Add("batch.start","Batch","Start batch",()=>{if(!BatchPanel.InputValid)throw new InvalidOperationException("Correct batch inputs first.");BatchController.Start();});
        Add("batch.preview","Batch","Preview queue",BatchPanel.Preview);
        Add("batch.pause","Batch","Pause / resume batch",BatchController.Run.TogglePause);
        Add("batch.stop","Batch","Stop batch and save pending scores",BatchController.Run.Stop);
        Add("batch.resumeSaved","Batch","Resume saved job (same users)",BatchController.ResumeSaved);
        Add("batch.reviewSaved","Batch","Reviewed server history: resume after restart",BatchController.ReviewSaved);
        Add("batch.journal","Batch","Save progress again",BatchController.RetryJournal);
        Add("batch.confirmSaved","Batch","I checked server: unknown upload was saved",()=>BatchController.Run.ResolveUpload(true));
        Add("batch.retryUnknown","Batch","Explicitly retry unknown upload (may duplicate logs)",()=>BatchController.Run.ResolveUpload(false));
        Func<string> batchIdle=()=>BatchController.Active?"Stop Batch before editing its frozen settings.":null;
        Add("batch.all","Batch","All difficulties",()=>BatchController.Settings.AllDifficulties=!BatchController.Settings.AllDifficulties,batchIdle);
        Add("batch.categories","Batch","Count categories instead of songs",()=>BatchController.Settings.ByCategories=!BatchController.Settings.ByCategories,batchIdle);
        Add("batch.cross","Batch","Continue into right-hand categories",()=>BatchController.Settings.CrossCategories=!BatchController.Settings.CrossCategories,batchIdle);
        Add("batch.existing","Batch","Skip already achieved charts",()=>BatchController.Settings.SkipExisting=!BatchController.Settings.SkipExisting,batchIdle);
        Add("batch.boost","Batch","Boost lower-level charts",()=>BatchController.Settings.Boost=!BatchController.Settings.Boost,batchIdle);
        Add("batch.template","Batch","Reset probability template",()=>{BatchController.Settings.Profiles=BatchSettings.CreateProfiles();BatchPanel.SyncInputs();},batchIdle);
        Add("batch.addOutcome","Batch","Add probability outcome",BatchPanel.AddOutcome,batchIdle);
        Add("batch.removeOutcome","Batch","Remove selected probability outcome",BatchPanel.RemoveOutcome,batchIdle);
        foreach(var name in new[]{"None","Critical","Perfect","Great","Good","Random","RandomAllPerfect","RandomFullComboPlus","RandomFullCombo"})
        {
            string mode=name;
            Legacy("auto."+name,"AutoPlay",name,()=>SettingsStore.SelectMode(mode),k.AutoPlay,name,Auto);
        }
        Add("auto.disableUpdate","AutoPlay","Disable game mode synchronization",()=>AutoPlay.DisableUpdate=!AutoPlay.DisableUpdate,Auto);
        Add("auto.rank","AutoPlay","Enable / replan rank control",()=>SettingsStore.SelectMode("Rank"),Auto);
        Add("auto.rankOff","AutoPlay","Stop rank control",()=>SettingsStore.SelectMode("None"),Auto);
        foreach(var name in AutoPlayPanel.Ranks)
        {
            string rank=name;
            Add("auto.rankTarget."+rank,"AutoPlay targets","AutoPlay target "+rank,()=>RankAutoPlay.TargetRank=rank,Auto);
            Add("skip.rankTarget."+rank,"FastSkip targets","Skip target "+rank,()=>c.Cheat.RandomSkip.TargetRank=rank,()=>Enabled(c.Cheat.FastSkip,"FastSkip"));
            Add("batch.rank."+rank,"Batch","Batch target "+rank,()=>BatchController.Settings.TargetRank=rank,batchIdle);
        }
        foreach(MinimumCombo value in Enum.GetValues(typeof(MinimumCombo)))
        {
            var combo=value;
            Add("auto.rankCombo."+combo,"AutoPlay targets","AutoPlay minimum "+combo,()=>RankAutoPlay.Minimum=combo,Auto);
            Add("skip.rankCombo."+combo,"FastSkip targets","Skip minimum "+combo,()=>c.Cheat.RandomSkip.MinimumCombo=combo,()=>Enabled(c.Cheat.FastSkip,"FastSkip"));
            Add("batch.combo."+combo,"Batch","Batch minimum "+combo,()=>BatchController.Settings.Minimum=combo,batchIdle);
        }
        Add("auto.pause","AutoPlay","Pause / resume song",()=> {if(PracticeLoop.Session)PracticeLoop.TogglePause();else PlaySession.TogglePause();},()=>Auto() ?? PlaySession.RequirePlay() ??
            (RankAutoPlay.IsBusy ? "Rank planning is in progress." : null));
        Add("speed.toggle","Speed","Enable SpeedHack",()=> {c.Cheat.SpeedHack.Enable=!c.Cheat.SpeedHack.Enable; SpeedHack.ApplyCurrentSpeed(false);},Native);
        Add("speed.global","Speed","Enable global speed",()=> {c.Cheat.SpeedHack.EnableGlobalSpeed=!c.Cheat.SpeedHack.EnableGlobalSpeed; SpeedHack.ApplyGlobalSpeedState();},Native);
        Func<string> speed=()=>Native() ?? (c.Cheat.SpeedHack.Enable ? null : "Enable SpeedHack first.");
        Legacy("speed.up","Speed","Speed +0.1",SpeedHack.IncreaseSpeed,k.SpeedHack,"Increase",speed);
        Legacy("speed.down","Speed","Speed -0.1",SpeedHack.DecreaseSpeed,k.SpeedHack,"Decrease",speed);
        Legacy("speed.reset","Speed","Speed x1.0",SpeedHack.ResetSpeed,k.SpeedHack,"Reset",speed);
        Add("skip","FastSkip","Skip song",()=>FastSkip.SkipButton=true,Skip);
        foreach(var name in new[]{"Default","Custom","Random"})
        {
            string mode=name;
            Add("skip."+name,"FastSkip",name+" skip mode",()=>FastSkip.Mode=(FastSkip.SkipMode)Enum.Parse(typeof(FastSkip.SkipMode),mode),
                ()=>Enabled(c.Cheat.FastSkip,"FastSkip"));
        }
        Add("skip.miss","FastSkip","Force at least one Miss",()=>FastSkip.Force1Miss=!FastSkip.Force1Miss,()=>Enabled(c.Cheat.FastSkip,"FastSkip"));
        foreach(ChartController.Button button in Enum.GetValues(typeof(ChartController.Button)))
        {
            if(button==ChartController.Button.None) continue;
            var b=button; Action run=()=>ChartController.Execute(b);
            string legacy=b==ChartController.Button.Pause ? "Pause" : b==ChartController.Button.Set ? "SetRecord" : b==ChartController.Button.Back ? "ReturnRecord" : null;
            if(legacy!=null) Legacy("chart."+b,"Chart",b.ToString(),run,k.ChartController,legacy,Chart);
            else Add("chart."+b,"Chart",b.ToString(),run,Chart);
        }
        Func<string> nudge=()=>Chart() ?? (PracticeLoop.Session ? "Seeking unavailable in practice." : null);
        Legacy("chart.forward","Chart","Hold to move forward while paused",()=>ChartController.Nudge(16),k.ChartController,"Forward",nudge,true);
        Legacy("chart.backward","Chart","Hold to move backward while paused",()=>ChartController.Nudge(-16),k.ChartController,"Backward",nudge,true);
        Add("loop.a","Practice","Set A at current position",()=>PracticeLoop.A=Math.Max(0,PlaySession.Time),Chart);
        Add("loop.b","Practice","Set B at current position",()=>PracticeLoop.B=Math.Max(0,PlaySession.Time),Chart);
        Add("loop.start","Practice","Start A/B practice",PracticeLoop.Start,Chart);
        Add("loop.pause","Practice","Pause / resume practice",PracticeLoop.TogglePause,()=>Chart() ?? (!PracticeLoop.Session ? "No practice session." : null));
        Add("loop.stop","Practice","Stop practice at current position",PracticeLoop.Stop,Chart);
        Add("loop.retry","Practice","Exit practice and restart whole song",PlaySession.Retry,Chart);
        Func<string> practiceSettings=()=>Enabled(c.Cheat.ChartController,"ChartController");
        Add("loop.speedUp","Practice","Practice speed +0.05",()=>PracticeLoop.AdjustSpeed(.05f),practiceSettings);
        Add("loop.speedDown","Practice","Practice speed -0.05",()=>PracticeLoop.AdjustSpeed(-.05f),practiceSettings);
        Add("loop.speedReset","Practice","Practice speed x1.00",()=>PracticeLoop.Speed=1,practiceSettings);
        Add("loop.pitch","Practice","Preserve original practice pitch",()=>PracticeLoop.PreservePitch=!PracticeLoop.PreservePitch,practiceSettings);
        Add("graphic.fullscreen","Graphic","Toggle full screen",Common.Graphic.ToggleFullscreen,Native);
        Add("graphic.apply","Graphic","Apply graphic inputs",GraphicPanel.Apply,Native);
        Add("login.code","Login","Login using current code",DummyLoginPanel.LoginCode,()=>Enabled(c.Common.DummyLogin.Enable,"DummyLogin"));
        Add("login.id","Login","Login using current ID",DummyLoginPanel.LoginId,()=>Enabled(c.Common.DummyLogin.Enable,"DummyLogin"));
        for(int i=0;i<2;i++)
        {
            int p=i;
            Add("user.export"+p,"UserData",$"Export P{p+1} data",()=>User.ExportBackupData(p),()=>UserReady(p));
            Add("debug.option"+p,"Debug",$"Save P{p+1} default option",()=>Common.ChangeDefaultOption.SaveOptionFile(p),()=>UserReady(p));
        }
        foreach(var type in new[]{"Icon","Plate","Title","Partner","Frame","Music","MaiMile"})
        {
            string item=type; Add("user.add"+item,"UserData","Add "+item+" using current input",()=>UserDataPanel.ExecuteAdd(item),AnyUserReady);
        }
        Add("user.new","UserData","Toggle new collection flag",UserDataPanel.ToggleNew,Native);
        Add("debug.touch","Debug","Touch area display",()=>Common.InputManager.TouchAreaDisplayButton=true,Native);
        Add("debug.message","Debug","Send test message",()=>{GameMessageManager.SendMessage(0,"Hello World!\nMonitorId: 0");GameMessageManager.SendMessage(1,"Hello World!\nMonitorId: 1");},Native);
        Add("debug.sound","Debug","Play test sound",()=>SoundManager.PlaySE(Mai2.Mai2Cue.Cue.SE_ENTRY_AIME_ERROR,1),Native);
        Add("debug.utage","Debug","Switch Utage mode",UnlockUtage.SwitchUtageMode,Native);
        Add("debug.log","Debug","Toggle Unity log",()=>{c.Common.UnityLogger.Enable=!c.Common.UnityLogger.Enable;DebugPanel.UnityLogger=c.Common.UnityLogger.Enable;},Native);
        SettingsStore.RefreshPresets();
    }
}
