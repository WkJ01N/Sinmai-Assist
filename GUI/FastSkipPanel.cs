using SinmaiAssist.Cheat;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class FastSkipPanel
{
    private static string _scoreInput = "0";
    
    public static void OnGUI()
    {
        GUILayout.Label($"Skip Mode: {FastSkip.Mode}", MainGUI.Style.Text);
        bool enabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = enabled && !FastSkip.IsBusy;
        if (GUILayout.Button("Skip", new GUIStyle(MainGUI.Style.Button){ fontSize=20 }, GUILayout.Height(45f))) FastSkip.SkipButton = true;
        UnityEngine.GUI.enabled = enabled;
        GUILayout.Label($"Mode Setting", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Default", MainGUI.Style.Button)) FastSkip.Mode = FastSkip.SkipMode.Default;
        if (GUILayout.Button("Custom", MainGUI.Style.Button)) FastSkip.Mode = FastSkip.SkipMode.Custom;
        if (GUILayout.Button("Random", MainGUI.Style.Button)) FastSkip.Mode = FastSkip.SkipMode.Random;
        GUILayout.EndHorizontal();
        if (FastSkip.Mode == FastSkip.SkipMode.Custom)
        {
            GUILayout.Label($"Custom Setting", MainGUI.Style.Title);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Custom Score(0 - 101): ", MainGUI.Style.Text);
            _scoreInput = GUILayout.TextField(_scoreInput);
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
            FastSkip.Force1Miss = GUILayout.Toggle(FastSkip.Force1Miss, "Force Add 1 miss");
        }
        else if (FastSkip.Mode == FastSkip.SkipMode.Random)
        {
            var config = SinmaiAssist.MainConfig.Cheat.RandomSkip;
            string[] ranks = { "D", "C", "B", "BB", "BBB", "A", "AA", "AAA", "S", "S+", "SS", "SS+", "SSS", "SSS+" };
            int selected = System.Array.FindIndex(ranks, r => string.Equals(r.Replace("+", "P"), (config.TargetRank ?? "SSS").Replace("+", "P"), System.StringComparison.OrdinalIgnoreCase));
            GUILayout.Label("Target Rank", MainGUI.Style.Title);
            int rank = GUILayout.SelectionGrid(System.Math.Max(0, selected), ranks, 4, MainGUI.Style.Button);
            config.TargetRank = ranks[rank];
            GUILayout.Label("Minimum Combo", MainGUI.Style.Title);
            config.MinimumCombo = (MinimumCombo)GUILayout.SelectionGrid((int)config.MinimumCombo,
                new[] { "Any", "FC", "FC+", "AP" }, 4, MainGUI.Style.Button);
            GUILayout.Label("Regenerate the whole chart. Higher combo flags are allowed.", MainGUI.Style.Text);
        }
        if (!string.IsNullOrEmpty(FastSkip.Status)) GUILayout.Label(FastSkip.Status, MainGUI.Style.Text);
    }
}
