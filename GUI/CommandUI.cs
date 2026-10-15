using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

internal static class CommandUI
{
    private static int _control;
    public static void Begin() { _control = 0; }
    public static void End()
    {
        Commands.TextEditing = UnityEngine.GUI.GetNameOfFocusedControl().StartsWith("sa-input-");
        if(Commands.TextEditing && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            UnityEngine.GUI.FocusControl("");Commands.TextEditing=false;Commands.SuppressUntilRelease();
        }
    }
    public static string Text(string value, params GUILayoutOption[] options)
    {
        UnityEngine.GUI.SetNextControlName("sa-input-" + _control++);
        return GUILayout.TextField(value ?? "", options);
    }
    public static string Area(string value, params GUILayoutOption[] options)
    {
        UnityEngine.GUI.SetNextControlName("sa-input-" + _control++);
        return GUILayout.TextArea(value ?? "", options);
    }
    public static void Button(string id, string label = null, params GUILayoutOption[] options)
    {
        if (GUILayout.Button(label ?? Commands.All[id].Label, MainGUI.Style.Button, options)) Commands.Enqueue(id);
    }
    public static void Toggle(string id, bool value, string label)
    {
        if (GUILayout.Toggle(value, label) != value) Commands.Enqueue(id);
    }
}
