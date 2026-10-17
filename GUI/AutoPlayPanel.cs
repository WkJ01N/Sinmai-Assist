using SinmaiAssist.Cheat;
using UnityEngine;
using SinmaiAssist.Runtime;

namespace SinmaiAssist.GUI;

public class AutoPlayPanel
{
    internal static readonly string[] Ranks={"D","C","B","BB","BBB","A","AA","AAA","S","S+","SS","SS+","SSS","SSS+"};
    public static void OnGUI()
    {
        GUILayout.Label("Mode: "+(SettingsStore.PreferredMode=="Rank" || RankAutoPlay.Requested ? "Rank ("+RankAutoPlay.Stage+")" : AutoPlay.autoPlayMode.ToString()),MainGUI.Style.Title);
        CommandUI.Toggle("auto.disableUpdate",AutoPlay.DisableUpdate,"Disable game mode synchronization");
        foreach(var mode in new[]{"None","Critical","Perfect","Great","Good","Random","RandomAllPerfect","RandomFullComboPlus","RandomFullCombo"})
            CommandUI.Button("auto."+mode);
        GUILayout.Label("Rank-controlled AutoPlay",MainGUI.Style.Title);
        GUILayout.Label("Configured target (apply with Enable / replan)",MainGUI.Style.Text);
        DrawRank(RankAutoPlay.TargetRank,"auto.rankTarget.");
        DrawCombo(RankAutoPlay.Minimum,"auto.rankCombo.");
        CommandUI.Button("auto.rank"); CommandUI.Button("auto.rankOff"); CommandUI.Button("auto.pause");
        if(RankAutoPlay.Active) GUILayout.Label("Active: "+RankAutoPlay.ActiveTarget,MainGUI.Style.Text);
        GUILayout.Label(RankAutoPlay.Status,MainGUI.Style.Text);
        if(!string.IsNullOrEmpty(RankAutoPlay.LastFailure)&&RankAutoPlay.LastFailure!=RankAutoPlay.Status)
            GUILayout.Label("Last failure: "+RankAutoPlay.LastFailure,new GUIStyle(MainGUI.Style.ErrorMessage){wordWrap=true});
    }
    internal static void DrawRank(string current,string prefix)
    {
        int selected=System.Array.FindIndex(Ranks,r=>r==current?.Replace("P","+"));
        int next=GUILayout.SelectionGrid(System.Math.Max(0,selected),Ranks,4,MainGUI.Style.Button);
        if(next!=selected) Commands.Enqueue(prefix+Ranks[next]);
    }
    internal static void DrawCombo(MinimumCombo current,string prefix)
    {
        var next=(MinimumCombo)GUILayout.SelectionGrid((int)current,new[]{"Any","FC","FC+","AP","AP+"},3,MainGUI.Style.Button);
        if(next!=current) Commands.Enqueue(prefix+next);
    }
}
