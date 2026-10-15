using SinmaiAssist.Cheat;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class FastSkipPanel
{
    private static string _scoreInput = "0";
    internal static void SyncInput() { _scoreInput=FastSkip.CustomAchivement.ToString(); }
    
    public static void OnGUI()
    {
        GUILayout.Label($"Skip Mode: {FastSkip.Mode}", MainGUI.Style.Text);
        bool enabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = enabled && !FastSkip.IsBusy;
        CommandUI.Button("skip", "Skip", GUILayout.Height(45f));
        UnityEngine.GUI.enabled = enabled;
        GUILayout.Label($"Mode Setting", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        CommandUI.Button("skip.Default","Default");
        CommandUI.Button("skip.Custom","Custom");
        CommandUI.Button("skip.Random","Random");
        GUILayout.EndHorizontal();
        if (FastSkip.Mode == FastSkip.SkipMode.Custom)
        {
            GUILayout.Label($"Custom Setting", MainGUI.Style.Title);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Custom Score(0 - 101): ", MainGUI.Style.Text);
            _scoreInput = CommandUI.Text(_scoreInput);
            GUILayout.EndHorizontal();
            FastSkip.CustomInputValid = int.TryParse(_scoreInput, out int scoreValue) && scoreValue >= 0 && scoreValue <= 101;
            if (FastSkip.CustomInputValid)
            {
                FastSkip.CustomAchivement = scoreValue;
                GUILayout.Label($"Custom Score: {scoreValue} %", MainGUI.Style.Text);
            }
            else
            {
                GUILayout.Label("Enter an integer from 0 to 101.", MainGUI.Style.ErrorMessage);
            }
            CommandUI.Toggle("skip.miss",FastSkip.Force1Miss,"Force Add 1 miss");
        }
        else if (FastSkip.Mode == FastSkip.SkipMode.Random)
        {
            var config = SinmaiAssist.MainConfig.Cheat.RandomSkip;
            GUILayout.Label("Target Rank", MainGUI.Style.Title);
            AutoPlayPanel.DrawRank((config.TargetRank??"SSS").ToUpperInvariant(),"skip.rankTarget.");
            GUILayout.Label("Minimum Combo", MainGUI.Style.Title);
            AutoPlayPanel.DrawCombo(config.MinimumCombo,"skip.rankCombo.");
            GUILayout.Label("Regenerate the whole chart. Higher combo flags are allowed.", MainGUI.Style.Text);
        }
        if (!string.IsNullOrEmpty(FastSkip.Status)) GUILayout.Label(FastSkip.Status, MainGUI.Style.Text);
    }
}
