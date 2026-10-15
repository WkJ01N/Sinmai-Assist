using System;
using System.Collections.Generic;
using System.Globalization;
using SinmaiAssist.Cheat;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class ChartControllerPanel
{
    private static readonly Dictionary<string,string> Inputs=new Dictionary<string,string>();
    private static readonly Dictionary<string,double?> Last=new Dictionary<string,double?>();
    private static Process.GameProcess _inputOwner;
    internal static void SyncInputs() { Inputs.Clear();Last.Clear();PracticeLoop.InputValid=true; }
    public static void OnGUI()
    {
        if(_inputOwner!=PlaySession.Owner) { _inputOwner=PlaySession.Owner;SyncInputs(); }
        GUILayout.Label($"{(PlaySession.Playing ? PlaySession.Paused ? "Paused" : "Playing" : "Not playing")}  {PlaySession.Time/1000:0.000}s",MainGUI.Style.Title);
        if(PlaySession.PreparingMusic)GUILayout.Label("Preparing practice music...",MainGUI.Style.Text);
        CommandUI.Button("chart.Pause","Pause / resume");
        GUILayout.BeginHorizontal();
        foreach(var pair in new[]{("TimeSkipSub3","-2.5s"),("TimeSkipSub2","-1s"),("TimeSkipSub","-0.1s"),
            ("TimeSkipAdd","+0.1s"),("TimeSkipAdd2","+1s"),("TimeSkipAdd3","+2.5s")}) CommandUI.Button("chart."+pair.Item1,pair.Item2);
        GUILayout.EndHorizontal();
        CommandUI.Button("chart.Reset","Restart whole song");
        GUILayout.Label($"Recorded: {ChartController.RecordTime/1000.0:0.000}s",MainGUI.Style.Text);
        GUILayout.BeginHorizontal(); CommandUI.Button("chart.Set","Set marker"); CommandUI.Button("chart.Back","Return to marker"); GUILayout.EndHorizontal();
        GUILayout.Label("A/B Practice (no song result saved)",MainGUI.Style.Title);
        bool valid=Number("A (seconds)",PracticeLoop.A/1000,v=>PracticeLoop.A=v*1000,true);
        valid &= Number("B (seconds)",PracticeLoop.B/1000,v=>PracticeLoop.B=v*1000,true);
        valid &= Number("Repeats (0 = unlimited)",PracticeLoop.Count,v=>{if(v.HasValue)PracticeLoop.Count=(int)v;},false,true);
        valid &= Number("Pre-roll (seconds)",PracticeLoop.PreRoll,v=>{if(v.HasValue)PracticeLoop.PreRoll=v.Value;});
        valid &= Number("Interval (real seconds)",PracticeLoop.Interval,v=>{if(v.HasValue)PracticeLoop.Interval=v.Value;});
        valid &= Number("Practice speed (0.50 - 2.00)",PracticeLoop.Speed,v=>{if(v.HasValue)PracticeLoop.Speed=(float)v.Value;},minimum:.5,maximum:2);
        GUILayout.BeginHorizontal();
        CommandUI.Button("loop.speedDown","-0.05x");CommandUI.Button("loop.speedReset","1.00x");CommandUI.Button("loop.speedUp","+0.05x");
        GUILayout.EndHorizontal();
        CommandUI.Toggle("loop.pitch",PracticeLoop.PreservePitch,"Preserve original pitch (next round)");
        if(PracticeLoop.Session)
            GUILayout.Label($"Current: {PracticeLoop.ActiveSpeed:0.00}x / {(PracticeLoop.ActivePreservePitch?"original pitch":"pitch follows speed")}\nNext round: {PracticeLoop.Speed:0.00}x / {(PracticeLoop.PreservePitch?"original pitch":"pitch follows speed")}",MainGUI.Style.Text);
        PracticeLoop.InputValid=valid;
        if(!valid) GUILayout.Label("Enter non-negative numbers; repeats must be an integer, speed 0.50 - 2.00.",MainGUI.Style.ErrorMessage);
        GUILayout.BeginHorizontal(); CommandUI.Button("loop.a","Set A now"); CommandUI.Button("loop.b","Set B now"); GUILayout.EndHorizontal();
        CommandUI.Button("loop.start"); CommandUI.Button("loop.pause"); CommandUI.Button("loop.stop"); CommandUI.Button("loop.retry");
        if(PracticeLoop.Session)
        {
            GUILayout.Label($"Actual: {PracticeLoop.WindowA/1000:0.000}s - {PracticeLoop.WindowB/1000:0.000}s\nRound {PracticeLoop.State.Completed+(PracticeLoop.State.Counted?0:1)} / completed {PracticeLoop.State.Completed}",MainGUI.Style.Text);
            for(int p=0;p<2;p++)
            {
                if(!MAI2.Util.Singleton<Manager.UserDataManager>.Instance.GetUserData(p).IsEntry) continue;
                var s=MAI2.Util.Singleton<Manager.GamePlayManager>.Instance.GetGameScore(p);
                GUILayout.Label($"P{p+1}: C {s.CriticalNum} / P {s.PerfectNum} / Gr {s.GreatNum} / Gd {s.GoodNum} / M {s.MissNum}\nCombo {s.Combo} / Max {s.MaxCombo}",MainGUI.Style.Text);
            }
        }
        GUILayout.Label(PracticeLoop.Status,MainGUI.Style.Text);
    }
    private static bool Number(string label,double? current,Action<double?> set,bool nullable=false,bool integer=false,double minimum=0,double maximum=double.MaxValue)
    {
        if(!Inputs.ContainsKey(label) || !Last.TryGetValue(label,out var previous) || previous!=current)
        {Inputs[label]=current?.ToString("0.###",CultureInfo.InvariantCulture)??"";Last[label]=current;}
        GUILayout.Label(label,MainGUI.Style.Text);
        string text=CommandUI.Text(Inputs[label]); Inputs[label]=text;
        if(nullable && string.IsNullOrWhiteSpace(text)) {set(null);Last[label]=null;return true;}
        if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) || double.IsNaN(value) ||
            double.IsInfinity(value) || value<minimum || value>maximum || (integer && (value>int.MaxValue || value!=Math.Truncate(value)))) return false;
        set(value);Last[label]=value;return true;
    }
}
