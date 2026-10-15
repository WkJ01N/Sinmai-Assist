using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SinmaiAssist.Cheat;
using SinmaiAssist.Config;
using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

internal static class BatchPanel
{
    public static bool InputValid=true;
    private static readonly Dictionary<string,string> Text=new Dictionary<string,string>();
    private static readonly Dictionary<string,string> Last=new Dictionary<string,string>();
    private static string _preview="";
    private static int _level=23,_outcome;
    public static void SyncInputs(){Text.Clear();Last.Clear();InputValid=true;_outcome=0;}
    public static void Preview()
    {
        BatchController.RequireIdle();if(!InputValid)throw new InvalidOperationException("Correct batch inputs first.");
        var songs=BatchNative.BuildQueue(BatchController.Settings);
        _preview=$"{songs.Count} unique songs / {songs.Sum(s=>s.Charts.Count)} chart tasks\n"+
            string.Join("\n",songs.Take(8).Select(s=>s.Name+" ("+s.Charts.Count+")"))+"\nPreview only; goals are frozen when Start is clicked.";
    }
    private static BatchLevelProfile Profile
    {
        get
        {
            string name=BatchSettings.LevelName(_level+2);
            var profile=BatchController.Settings.Profiles.FirstOrDefault(p=>p.Level==name);
            if(profile==null){profile=new BatchLevelProfile{Level=name,Outcomes=new List<BatchWeight>{new BatchWeight()}};BatchController.Settings.Profiles.Add(profile);}
            return profile;
        }
    }
    public static void AddOutcome(){Profile.Outcomes.Add(new BatchWeight());SyncInputs();_outcome=Profile.Outcomes.Count-1;}
    public static void RemoveOutcome()
    {if(Profile.Outcomes.Count<=1)throw new InvalidOperationException("Keep at least one outcome.");Profile.Outcomes.RemoveAt(Math.Min(_outcome,Profile.Outcomes.Count-1));SyncInputs();}
    public static void OnGUI()
    {
        var run=BatchController.Run;var settings=BatchController.Settings;
        GUILayout.Label("Batch rank farming",MainGUI.Style.Title);
        GUILayout.Label(run.Status,MainGUI.Style.Text);
        if(run.Job!=null)
        {
            GUILayout.Label($"Songs: {run.Job.Processed}/{run.Job.Songs.Count}\nStage: {run.Stage}",MainGUI.Style.Text);
            GUILayout.Label(string.Join(" | ",run.Job.Songs.Where(s=>s.Done).GroupBy(s=>s.Outcome).Select(g=>g.Key+": "+g.Count())),MainGUI.Style.Text);
            var receipts=run.Job.Uploads;
            if(receipts.Count>0)GUILayout.Label($"Upload phases: {receipts.Count(u=>u.State=="Accepted")}/{receipts.Count} confirmed\n"+
                string.Join("\n",receipts.Where(u=>u.State!="Accepted"&&u.State!="Reviewed").Take(4).Select(u=>u.Id+": "+u.State)),MainGUI.Style.Text);
        }
        CommandUI.Button("batch.start");CommandUI.Button("batch.pause");CommandUI.Button("batch.stop");CommandUI.Button("batch.resumeSaved");
        if(!run.Active)
        {
            GUILayout.Label("After restart, unresolved scores require checking server history. Review resumes by rechecking native best scores, without resending old requests.",MainGUI.Style.Text);
            CommandUI.Button("batch.reviewSaved");
        }
        if(BatchController.JournalError!=null){GUILayout.Label(BatchController.JournalError,MainGUI.Style.ErrorMessage);CommandUI.Button("batch.journal");}
        if(run.Upload?.Blocked==true)
        {
            GUILayout.Label("Uploads blocked: "+run.Upload.Error,MainGUI.Style.ErrorMessage);
            if(run.Upload.Operations.Any(o=>o.Receipt.State=="Unknown"))
            {CommandUI.Button("batch.confirmSaved");CommandUI.Button("batch.retryUnknown");}
        }
        if(run.Active)
        {
            var song=run.Job.Songs.FirstOrDefault(s=>!s.Done);var chart=song?.Charts.FirstOrDefault(c=>!c.Done);
            if(chart!=null)GUILayout.Label(song.Name+"\nDiff: "+string.Join("/",chart.Difficulty)+" / attempts "+chart.Attempts+"\n"+
                string.Join(" | ",chart.Goals.Select((g,p)=>g==null?"":$"P{p+1}: {g}")),MainGUI.Style.Text);
            GUILayout.Label("Batch settings are frozen until Stop completes.",MainGUI.Style.Text);return;
        }
        CommandUI.Toggle("batch.all",settings.AllDifficulties,"All available difficulties");
        CommandUI.Toggle("batch.categories",settings.ByCategories,"Count categories (current partial category included)");
        bool valid=Number("Songs",settings.SongCount,v=>settings.SongCount=(int)v,true,1);
        valid&=Number("Categories",settings.CategoryCount,v=>settings.CategoryCount=(int)v,true,1);
        CommandUI.Toggle("batch.cross",settings.CrossCategories,"Continue right across categories (song-count mode)");
        CommandUI.Toggle("batch.existing",settings.SkipExisting,"Skip already achieved charts");
        GUILayout.Label("Base rank (strict interval)",MainGUI.Style.Text);AutoPlayPanel.DrawRank(settings.TargetRank,"batch.rank.");
        AutoPlayPanel.DrawCombo(settings.Minimum,"batch.combo.");
        CommandUI.Toggle("batch.boost",settings.Boost,"Boost lower-level charts");
        string threshold=Edit("Boost through (e.g. 12+)",settings.BoostThrough);
        if(BatchSettings.LevelOrdinal(threshold)>=0)settings.BoostThrough=threshold;else valid=false;
        if(settings.Boost)
        {
            GUILayout.Label("Edit display-level probability weights",MainGUI.Style.Title);
            _level=GUILayout.SelectionGrid(_level,Enumerable.Range(2,30).Select(BatchSettings.LevelName).ToArray(),6,MainGUI.Style.Button);
            var profile=Profile;
            _outcome=Math.Min(_outcome,profile.Outcomes.Count-1);
            _outcome=GUILayout.SelectionGrid(_outcome,profile.Outcomes.Select((w,i)=>i+": "+(w.Theory?"AP+":w.Rank+" / "+(w.UseBaseCombo?"Base":w.Combo.ToString()))+" : "+w.Weight.ToString("0.##")).ToArray(),1,MainGUI.Style.Button);
            var outcome=profile.Outcomes[_outcome];string prefix=profile.Level+"/"+_outcome+" ";
            string rank=Edit(prefix+"Rank (Base or SSS+ etc.)",outcome.Rank);
            if(rank=="Base"||BatchSettings.Ranks.Contains(rank))outcome.Rank=rank;else valid=false;
            string combo=Edit(prefix+"Combo (Base/None/FC/FCPlus/AP/APPlus)",outcome.UseBaseCombo?"Base":outcome.Combo.ToString());
            if(combo=="Base")outcome.UseBaseCombo=true;
            else if(Enum.TryParse(combo,out MinimumCombo parsed)&&Enum.IsDefined(typeof(MinimumCombo),parsed)){outcome.UseBaseCombo=false;outcome.Combo=parsed;}else valid=false;
            string theory=Edit(prefix+"Theory AP+ (true/false)",outcome.Theory.ToString().ToLowerInvariant());
            if(bool.TryParse(theory,out bool flag))outcome.Theory=flag;else valid=false;
            valid&=Number(prefix+"Weight",outcome.Weight,v=>outcome.Weight=v,false,0);
            CommandUI.Button("batch.addOutcome");CommandUI.Button("batch.removeOutcome");CommandUI.Button("batch.template");
            GUILayout.Label("Weights are normalized. Targets never fall below the base requirements.",MainGUI.Style.Text);
        }
        InputValid=valid;
        if(!valid)GUILayout.Label("Correct invalid inputs before starting or saving.",MainGUI.Style.ErrorMessage);
        CommandUI.Button("batch.preview");GUILayout.Label(_preview,MainGUI.Style.Text);
    }
    private static string Edit(string label,string current)
    {
        if(!Last.TryGetValue(label,out string previous)||previous!=current){Text[label]=current;Last[label]=current;}
        GUILayout.Label(label,MainGUI.Style.Text);string value=CommandUI.Text(Text[label]);Text[label]=value;return value;
    }
    private static bool Number(string label,double current,Action<double> set,bool integer,double minimum)
    {
        string value=Edit(label,current.ToString("0.######",CultureInfo.InvariantCulture));
        if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double number)||double.IsNaN(number)||double.IsInfinity(number)||number<minimum||number>1000000||(integer&&number!=Math.Truncate(number)))return false;
        set(number);return true;
    }
    public static void Overlay()
    {
        if(!BatchController.Active)return;
        var run=BatchController.Run;float x=Math.Max(8,Screen.width-328);
        GUILayout.BeginArea(new Rect(x,8,320,150),"Batch",UnityEngine.GUI.skin.window);
        GUILayout.Label($"{run.Job.Processed}/{run.Job.Songs.Count} songs | {run.Stage}\n{run.Status}");
        CommandUI.Button("batch.pause","Pause / resume");CommandUI.Button("batch.stop","Stop and save");
        GUILayout.EndArea();
    }
}
