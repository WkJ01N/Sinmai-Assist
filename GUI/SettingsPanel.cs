using SinmaiAssist.Runtime;
using UnityEngine;

namespace SinmaiAssist.GUI;

internal static class SettingsPanel
{
    public static void OnGUI()
    {
        GUILayout.Label(SettingsStore.Dirty ? "Unsaved changes" : "Settings saved", MainGUI.Style.Title);
        CommandUI.Button("save");
        GUILayout.Label("Presets (saved only with Save)",MainGUI.Style.Title);
        foreach(var p in SettingsStore.Document.Presets)
            if(GUILayout.Button((SettingsStore.SelectedId==p.Id ? "> " : "")+p.Name,MainGUI.Style.Button))
            {SettingsStore.SelectedId=p.Id; SettingsStore.NameInput=p.Name;}
        SettingsStore.NameInput=CommandUI.Text(SettingsStore.NameInput);
        CommandUI.Button("preset.apply"); CommandUI.Button("preset.create"); CommandUI.Button("preset.overwrite");
        CommandUI.Button("preset.rename"); CommandUI.Button("preset.delete");
        CommandUI.Toggle("maskServer",SinmaiAssist.MainConfig.ModSetting.MaskTitleServerUrl,"Mask server URL");
        CommandUI.Toggle("panel",SinmaiAssist.MainConfig.ModSetting.ShowPanel,"Show main panel");
        CommandUI.Toggle("info",SinmaiAssist.MainConfig.ModSetting.ShowInfo,"Show info");
        CommandUI.Toggle("fps",SinmaiAssist.MainConfig.Common.ShowFPS,"Show FPS");
    }
}
