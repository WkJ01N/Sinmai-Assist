using SinmaiAssist.Cheat;
using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

internal static class MutePanel
{
    internal static readonly string[] Labels={"Song music and preview","Menu background music","Judgement and gameplay effects","Menu sound effects","Voices","System and result notifications"};
    public static void OnGUI()
    {
        GUILayout.Label("Mute",MainGUI.Style.Title);
        CommandUI.Toggle("mute.all",MuteState.Master,"Mute whole game");
        GUILayout.Label("Category settings are kept while whole-game mute is on.",MainGUI.Style.Text);
        foreach(SoundGroup group in System.Enum.GetValues(typeof(SoundGroup)))
            CommandUI.Toggle("mute."+group,MuteState.Get(group),Labels[(int)group]);
        GUILayout.Label(AudioMute.Status,MainGUI.Style.Text);
    }
}
