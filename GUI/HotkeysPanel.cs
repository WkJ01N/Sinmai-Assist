using System.Linq;
using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

internal static class HotkeysPanel
{
    public static void OnGUI()
    {
        GUILayout.Label(SettingsStore.Dirty ? "Unsaved changes" : "Settings saved",MainGUI.Style.Title);
        GUILayout.Label("Click None, then press a key.\nClick bound key to clear. Click waiting / Esc to cancel.",MainGUI.Style.Text);
        if(Commands.Capture.Error!=null) GUILayout.Label(Commands.Capture.Error,MainGUI.Style.ErrorMessage);
        var conflicts=Commands.All.Keys.GroupBy(Commands.GetKey).Where(g=>g.Key!="None" && g.Count()>1).Select(g=>g.Key).ToArray();
        if(conflicts.Length>0) GUILayout.Label("Conflicting keys disabled: "+string.Join(", ",conflicts),MainGUI.Style.ErrorMessage);
        foreach(var group in Commands.All.Values.GroupBy(o=>o.Group))
        {
            GUILayout.Label(group.Key,MainGUI.Style.Title);
            foreach(var op in group)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label(op.Label,MainGUI.Style.Text,GUILayout.Width(210));
                string text=Commands.Capture.Waiting==op.Id ? "Press key..." : Commands.GetKey(op.Id);
                if(GUILayout.Button(text,MainGUI.Style.Button,GUILayout.Width(100))) Commands.ClickBinding(op.Id);
                GUILayout.EndHorizontal();
            }
        }
        CommandUI.Button("save");
    }
}
