using System;
using System.Text;
using UnityEngine;
using SinmaiAssist.Runtime;

namespace SinmaiAssist.GUI;

public class MainGUI
{
    private enum Toolbar
    {
        AutoPlay,
        FastSkip,
        Batch,
        ChartController,
        DummyLogin,
        UserData,
        Graphic,
        Debug,
        Settings,
        Hotkeys
    }
    
    private const float PanelWidth = 320f;
    private const int ButtonsPerRow = 3;
    
    private Rect _panelWindow;
    private Toolbar _toolbar = Toolbar.AutoPlay;
    private Vector2 _scroll;
    private static MainGUI _current;
    
    public static readonly Style Style = new Style();
    
    public MainGUI()
    {
        _current=this;
    }
    internal static void Open(string name)
    {
        if(_current==null) return;
        Commands.Capture.Cancel();
        _current._toolbar=(Toolbar)Enum.Parse(typeof(Toolbar),name);
        _current._scroll=Vector2.zero;SinmaiAssist.MainConfig.ModSetting.ShowPanel=true;
    }

    public void HandleInput()
    {
        // Commands.Update dispatches input, including while the panel is hidden.
    }
    
    public void OnGUI()
    {
        if (SinmaiAssist.MainConfig == null || SinmaiAssist.KeyBindConfig == null)
        {
            return;
        }

        CommandUI.Begin();
        if (SinmaiAssist.MainConfig.ModSetting.ShowPanel)
        {
            string title = BuildInfo.Name + (SinmaiAssist.MainConfig.ModSetting.SafeMode ? " (SafeMode)" : "");
            _panelWindow = GUILayout.Window(10086, _panelWindow, MainPanel, title);
            SinmaiAssist.MainConfig.ModSetting.ShowPanel = true;
        }
        else
        {
            _panelWindow = new Rect();
            Commands.TextEditing=false;
            Commands.Capture.Cancel();
            if (!Commands.HasUsableKey("panel") && UnityEngine.GUI.Button(new Rect(8,Screen.height-38,160,30),"Open Sinmai-Assist"))
                Commands.Enqueue("panel");
        }
        if(SinmaiAssist.MainConfig.ModSetting.ShowPanel) CommandUI.End();
        BatchPanel.Overlay();
    }
    
    private void MainPanel(int id)
    {
        GUILayout.BeginVertical($"{BuildInfo.Name} {BuildInfo.Version} ({BuildInfo.CommitHash??"NOT SET"})", GUILayout.Height(20f));
        ToolBarPanel();
        GUILayout.EndVertical();
        _scroll=GUILayout.BeginScrollView(_scroll,GUILayout.Width(PanelWidth+30),GUILayout.Height(Mathf.Min(540,Mathf.Max(160,Screen.height-180))));
        GUILayout.BeginVertical(GUILayout.Width(PanelWidth));
        if (_toolbar == Toolbar.AutoPlay && SinmaiAssist.MainConfig.Cheat.AutoPlay) AutoPlayPanel.OnGUI();
        else if (_toolbar == Toolbar.FastSkip && SinmaiAssist.MainConfig.Cheat.FastSkip) FastSkipPanel.OnGUI();
        else if (_toolbar == Toolbar.Batch) BatchPanel.OnGUI();
        else if (_toolbar == Toolbar.ChartController && SinmaiAssist.MainConfig.Cheat.ChartController) ChartControllerPanel.OnGUI();
        else if (_toolbar == Toolbar.DummyLogin && SinmaiAssist.MainConfig.Common.DummyLogin.Enable) DummyLoginPanel.OnGUI();
        else if (_toolbar == Toolbar.UserData) UserDataPanel.OnGUI();
        else if (_toolbar == Toolbar.Graphic) GraphicPanel.OnGUI();
        else if (_toolbar == Toolbar.Debug) DebugPanel.OnGUI();
        else if (_toolbar == Toolbar.Settings) SettingsPanel.OnGUI();
        else if (_toolbar == Toolbar.Hotkeys) HotkeysPanel.OnGUI();
        else DisablePanel();
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.Label(Commands.Status,new GUIStyle(Style.Text){wordWrap=true},GUILayout.Width(PanelWidth));
        UnityEngine.GUI.DragWindow();
    }
    
    private void ToolBarPanel()
    {
        string[] toolbarNames = Enum.GetNames(typeof(Toolbar));
        int toolbarCount = toolbarNames.Length;
        int rowCount = Mathf.CeilToInt((float)toolbarCount / ButtonsPerRow);
        int selectedToolbar = (int)_toolbar;

        for (int row = 0; row < rowCount; row++)
        {
            GUILayout.BeginHorizontal();

            int startIndex = row * ButtonsPerRow;
            int endIndex = Mathf.Min(startIndex + ButtonsPerRow, toolbarCount);

            string[] rowToolbarNames = new string[endIndex - startIndex];
            Array.Copy(toolbarNames, startIndex, rowToolbarNames, 0, endIndex - startIndex);

            int rowSelection = GUILayout.Toolbar(
                selectedToolbar - startIndex,
                rowToolbarNames,
                Style.Button,
                GUILayout.Width(PanelWidth),
                GUILayout.Height(20f)
            );
            
            if (rowSelection >= 0 && rowSelection < rowToolbarNames.Length)
                selectedToolbar = startIndex + rowSelection;

            GUILayout.EndHorizontal();
        }
        if(_toolbar!=(Toolbar)selectedToolbar) Commands.Enqueue("panel."+(Toolbar)selectedToolbar);
    }
    
    private void DisablePanel()
    {
        GUILayout.Label(_toolbar.ToString(), Style.DisablePanel, GUILayout.Width(PanelWidth), GUILayout.Height(120f));
        GUILayout.Label("is Disable", Style.DisablePanel, GUILayout.Width(PanelWidth), GUILayout.Height(160f));
    }
}
