using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class DebugPanel
{
    public static bool UnityLogger;
    public static void OnGUI()
    {
        GUILayout.Label("Test tools",MainGUI.Style.Title);
        foreach(var id in new[]{"debug.touch","debug.message","debug.option0","debug.option1","debug.sound","debug.utage"}) CommandUI.Button(id);
        var speed=SinmaiAssist.MainConfig.Cheat.SpeedHack;
        GUILayout.Label("SpeedHack",MainGUI.Style.Title);
        CommandUI.Toggle("speed.toggle",speed.Enable,"Enable SpeedHack");
        CommandUI.Toggle("speed.global",speed.EnableGlobalSpeed,"Enable global speed");
        GUILayout.Label($"Current speed: x{speed.SpeedMultiplier:0.0}",MainGUI.Style.Text);
        GUILayout.BeginHorizontal();CommandUI.Button("speed.down","-0.1");CommandUI.Button("speed.up","+0.1");CommandUI.Button("speed.reset","Reset");GUILayout.EndHorizontal();
        CommandUI.Button("info");CommandUI.Button("fps");
        CommandUI.Toggle("debug.log",SinmaiAssist.MainConfig.Common.UnityLogger.Enable,"Log Unity debug");
    }
}
