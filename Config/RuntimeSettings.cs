using System;
using System.Collections.Generic;

namespace SinmaiAssist.Config;

public sealed class RuntimeSettings
{
    public bool SpeedEnabled { get; set; }
    public bool GlobalSpeed { get; set; }
    public float Speed { get; set; } = 1;
    public string AutoPlayMode { get; set; } = "None";
    public bool DisableModeUpdate { get; set; }
    public string SkipMode { get; set; } = "Default";
    public int CustomAchievement { get; set; }
    public bool ForceMiss { get; set; }
    public string SkipRank { get; set; } = "SSS";
    public string SkipCombo { get; set; } = "None";
    public string AutoRank { get; set; } = "SSS";
    public string AutoCombo { get; set; } = "None";
    public bool ShowInfo { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowPanel { get; set; } = true;
    public bool MaskServer { get; set; } = true;
    public int LoopCount { get; set; }
    public double PreRollSeconds { get; set; } = 3;
    public double IntervalSeconds { get; set; } = 5;
    public float PracticeSpeed { get; set; } = 1;
    public bool PreservePracticePitch { get; set; } = true;
    public bool MuteAll { get; set; }
    public bool MuteMusic { get; set; }
    public bool MuteMenuMusic { get; set; }
    public bool MuteGameEffects { get; set; }
    public bool MuteMenuEffects { get; set; }
    public bool MuteVoices { get; set; }
    public bool MuteNotifications { get; set; }
    public BatchSettings Batch { get; set; } = new BatchSettings();

    public RuntimeSettings Copy() { var copy=(RuntimeSettings)MemberwiseClone();copy.Batch=Batch.Copy();return copy; }
    public void Validate()
    {
        if (float.IsNaN(Speed) || float.IsInfinity(Speed) || Speed < .1f ||
            float.IsNaN(PracticeSpeed) || float.IsInfinity(PracticeSpeed) || PracticeSpeed < .5f || PracticeSpeed > 2f ||
            CustomAchievement < 0 || CustomAchievement > 101 || LoopCount < 0 ||
            double.IsNaN(PreRollSeconds) || double.IsInfinity(PreRollSeconds) || PreRollSeconds < 0 ||
            double.IsNaN(IntervalSeconds) || double.IsInfinity(IntervalSeconds) || IntervalSeconds < 0)
            throw new ArgumentException("Invalid runtime settings values.");
        if (!IsRank(SkipRank) || !IsRank(AutoRank) || !IsCombo(SkipCombo) || !IsCombo(AutoCombo))
            throw new ArgumentException("Invalid target rank or combo.");
        if(Batch==null)throw new ArgumentException("Missing batch settings.");Batch.Validate();
        if (!EnumText(AutoPlayMode, "None Critical Perfect Great Good Random RandomAllPerfect RandomFullComboPlus RandomFullCombo Rank") ||
            !EnumText(SkipMode, "Default Custom Random")) throw new ArgumentException("Invalid runtime mode.");
    }
    public static bool IsRank(string text) => EnumText(text, "D C B BB BBB A AA AAA S S+ SS SS+ SSS SSS+");
    private static bool IsCombo(string text) => EnumText(text, "None FC FCPlus AP APPlus");
    private static bool EnumText(string text, string allowed) => text != null && Array.IndexOf(allowed.Split(' '), text) >= 0;
}

public sealed class RuntimePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Preset";
    public RuntimeSettings Settings { get; set; } = new RuntimeSettings();
}

public sealed class RuntimeSettingsDocument
{
    // Missing in legacy YAML. Only a manual save writes the upgraded version.
    public int FormatVersion { get; set; } = 1;
    // Null distinguishes a new installation from an explicitly saved default snapshot.
    public RuntimeSettings Saved { get; set; }
    public List<RuntimePreset> Presets { get; set; } = new List<RuntimePreset>();
}
