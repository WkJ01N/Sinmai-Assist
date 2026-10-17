using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using SinmaiAssist.Cheat;
using SinmaiAssist.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SinmaiAssist.Runtime;

internal static class SettingsStore
{
    public static RuntimeSettingsDocument Document { get; private set; } = new RuntimeSettingsDocument();
    public static string NameInput = "", SelectedId;
    public static string PreferredMode => AutoPlaySelection.PreferredMode;
    private static string _directory, _savedFingerprint;
    private static readonly ISerializer Yaml = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new KeyBindConfig.Converter()).Build();
    private static readonly System.Reflection.PropertyInfo[] SettingProperties=typeof(RuntimeSettings).GetProperties();
    private static RuntimeSettings _lastCompared;
    private static bool _dirtyInvalidated=true,_dirtyCache;
    private static bool _migrationPending;
    private const int CurrentFormat = 3;
    private static ISerializer Serializer()=>Yaml;
    public static void InvalidateDirty()=>_dirtyInvalidated=true;
    public static RuntimeSettings Capture(bool copyBatch=true)
    {
        var c = SinmaiAssist.MainConfig;
        return new RuntimeSettings {
            SpeedEnabled=c.Cheat.SpeedHack.Enable, GlobalSpeed=c.Cheat.SpeedHack.EnableGlobalSpeed, Speed=c.Cheat.SpeedHack.SpeedMultiplier,
            AutoPlayMode=PreferredMode=="Rank" || RankAutoPlay.Requested ? "Rank" : AutoPlay.autoPlayMode.ToString(), DisableModeUpdate=AutoPlay.DisableUpdate,
            SkipMode=FastSkip.Mode.ToString(), CustomAchievement=FastSkip.CustomAchivement, ForceMiss=FastSkip.Force1Miss,
            SkipRank=NormalizeRank(c.Cheat.RandomSkip.TargetRank), SkipCombo=c.Cheat.RandomSkip.MinimumCombo.ToString(),
            AutoRank=RankAutoPlay.TargetRank, AutoCombo=RankAutoPlay.Minimum.ToString(), ShowInfo=c.ModSetting.ShowInfo,
            ShowFps=c.Common.ShowFPS, ShowPanel=c.ModSetting.ShowPanel, MaskServer=c.ModSetting.MaskTitleServerUrl,
            LoopCount=PracticeLoop.Count, PreRollSeconds=PracticeLoop.PreRoll, IntervalSeconds=PracticeLoop.Interval,
            PracticeSpeed=PracticeLoop.Speed, PreservePracticePitch=PracticeLoop.PreservePitch,
            MuteAll=MuteState.Master,MuteMusic=MuteState.Get(SoundGroup.Music),MuteMenuMusic=MuteState.Get(SoundGroup.MenuMusic),
            MuteGameEffects=MuteState.Get(SoundGroup.GameEffects),MuteMenuEffects=MuteState.Get(SoundGroup.MenuEffects),
            MuteVoices=MuteState.Get(SoundGroup.Voices),MuteNotifications=MuteState.Get(SoundGroup.Notifications),
            Batch=copyBatch?BatchController.Settings.Copy():BatchController.Settings };
    }
    private static string NormalizeRank(string text) => (text ?? "SSS").ToUpperInvariant().Replace("P", "+");
    public static void Initialize(string directory)
    {
        _directory = Path.GetFullPath(directory);
        BatchController.Initialize(_directory);
        var path = Path.Combine(_directory, "RuntimeSettings.yml");
        bool existed = File.Exists(path);
        _migrationPending = false;
        try
        {
            Document = new ConfigManager<RuntimeSettingsDocument>(path).GetConfig();
            Document.Presets ??= new List<RuntimePreset>();
            var ids = new HashSet<string>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var preset in Document.Presets)
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id) ||
                    string.IsNullOrWhiteSpace(preset.Name) || !names.Add(preset.Name) || preset.Settings == null)
                    throw new ArgumentException("Invalid preset library.");
                preset.Settings.Validate();
            }
            Document.Saved?.Validate();
            if (Document.FormatVersion < 1 || Document.FormatVersion > CurrentFormat) throw new ArgumentException("Unsupported runtime settings format.");
            if (Document.FormatVersion < 2)
            {
                foreach (var settings in Document.Presets.Select(p=>p.Settings).Concat(new[]{Document.Saved}).Where(s=>s!=null))
                    if (settings.IntervalSeconds == 0) settings.IntervalSeconds = 5;
            }
            _migrationPending=existed && Document.FormatVersion<CurrentFormat;
            Document.FormatVersion=CurrentFormat;
        }
        catch (Exception ex)
        {
            MelonLogger.Error(ex);
            try { if (File.Exists(path)) File.Move(path, path + ".broken." + Guid.NewGuid().ToString("N")); }
            catch (Exception backupError) { MelonLogger.Error(backupError); }
            Document = new RuntimeSettingsDocument();
            Commands.Status = "Runtime settings unavailable; defaults loaded. See log for backup status.";
        }
        if (Document.Saved != null) Apply(Document.Saved);
        _savedFingerprint = Fingerprint();
        _lastCompared=null;InvalidateDirty();
        if (_migrationPending) Commands.Status = "Runtime settings upgraded. Legacy v1 zero intervals become 5s; v2 zero intervals are retained. Save to keep the upgrade.";
    }
    private static string Fingerprint(RuntimeSettings current=null) => Serializer().Serialize(new RuntimeSettingsDocument { Saved=current??Capture(), Presets=Document.Presets }) +
        Serializer().Serialize(SinmaiAssist.KeyBindConfig);
    public static bool Dirty
    {
        get
        {
            var current=Capture(false);
            if(!_dirtyInvalidated && _lastCompared!=null && current.Batch.Same(_lastCompared.Batch) && SettingProperties.Where(p=>p.Name!="Batch").All(p=>Equals(p.GetValue(current),p.GetValue(_lastCompared)))) return _dirtyCache;
            _lastCompared=current.Copy();_dirtyInvalidated=false;_dirtyCache=_migrationPending || _savedFingerprint!=Fingerprint(current);return _dirtyCache;
        }
    }
    public static void Save()
    {
        BatchController.RequireIdle();
        if(!GUI.BatchPanel.InputValid || !PracticeLoop.InputValid || (FastSkip.Mode==FastSkip.SkipMode.Custom && !FastSkip.CustomInputValid))
            throw new InvalidOperationException("Correct the invalid batch / practice / custom score inputs before saving.");
        var current = Capture(); current.Validate();
        foreach(var preset in Document.Presets) preset.Settings.Validate();
        var staged = new RuntimeSettingsDocument { FormatVersion=CurrentFormat, Saved=current.Copy(), Presets=Document.Presets };
        AtomicConfigFile.WriteMany(new Dictionary<string, string> {
            [Path.Combine(_directory, "RuntimeSettings.yml")] = Serializer().Serialize(staged),
            [Path.Combine(_directory, "KeyBindConfig.yml")] = Serializer().Serialize(SinmaiAssist.KeyBindConfig) });
        Document.Saved = current;
        Document.FormatVersion = CurrentFormat; _migrationPending = false;
        _savedFingerprint = Fingerprint();
        InvalidateDirty();
        Commands.Status = "Settings, presets and shortcuts saved.";
    }
    public static void Apply(RuntimeSettings settings)
    {
        BatchController.RequireIdle();
        settings.Validate();
        if(settings.AutoPlayMode=="Rank" && (PracticeLoop.Session || RankAutoPlay.IsBusy))
        {
            RankAutoPlay.ReportBlocked("Finish rank planning or restart the practice song before applying a rank preset.");
            throw new InvalidOperationException("Finish rank planning or restart the practice song before applying a rank preset.");
        }
        var c = SinmaiAssist.MainConfig;
        c.Cheat.SpeedHack.Enable = settings.SpeedEnabled;
        c.Cheat.SpeedHack.EnableGlobalSpeed = settings.GlobalSpeed;
        SpeedHack.ApplySpeed(settings.Speed, false);
        c.ModSetting.ShowInfo=settings.ShowInfo; c.Common.ShowFPS=settings.ShowFps;
        c.ModSetting.ShowPanel=settings.ShowPanel; c.ModSetting.MaskTitleServerUrl=settings.MaskServer;
        c.Cheat.RandomSkip.TargetRank=settings.SkipRank;
        c.Cheat.RandomSkip.MinimumCombo=(MinimumCombo)Enum.Parse(typeof(MinimumCombo), settings.SkipCombo);
        FastSkip.Mode=(FastSkip.SkipMode)Enum.Parse(typeof(FastSkip.SkipMode), settings.SkipMode);
        FastSkip.CustomAchivement=settings.CustomAchievement; FastSkip.CustomInputValid=true; FastSkip.Force1Miss=settings.ForceMiss;
        GUI.FastSkipPanel.SyncInput();
        RankAutoPlay.TargetRank=settings.AutoRank;
        RankAutoPlay.Minimum=(MinimumCombo)Enum.Parse(typeof(MinimumCombo), settings.AutoCombo);
        PracticeLoop.Count=settings.LoopCount; PracticeLoop.PreRoll=settings.PreRollSeconds; PracticeLoop.Interval=settings.IntervalSeconds;
        PracticeLoop.Speed=settings.PracticeSpeed; PracticeLoop.PreservePitch=settings.PreservePracticePitch;
        BatchController.Settings=settings.Batch.Copy();
        GUI.BatchPanel.SyncInputs();
        GUI.ChartControllerPanel.SyncInputs();
        AutoPlay.DisableUpdate=settings.DisableModeUpdate;
        SelectMode(settings.AutoPlayMode);
        MuteState.Apply(settings.MuteAll,settings.MuteMusic,settings.MuteMenuMusic,settings.MuteGameEffects,settings.MuteMenuEffects,settings.MuteVoices,settings.MuteNotifications);
    }
    public static void SelectMode(string mode)
    {
        AutoPlaySelection.Select(mode);
    }
    public static void RestoreModeAtSongStart()
    {
        AutoPlaySelection.RestoreAtSongStart();
    }
    public static RuntimePreset Selected => Document.Presets.FirstOrDefault(p => p.Id == SelectedId);
    private static string ValidName()
    {
        string name = (NameInput ?? "").Trim();
        if (name.Length == 0 || name.Length > 64) throw new ArgumentException("Preset name must be 1-64 characters.");
        if (Document.Presets.Any(p => p.Id != SelectedId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A preset with this name already exists.");
        return name;
    }
    public static void Create()
    {
        string name=ValidName();
        if (Document.Presets.Any(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Name already exists.");
        var snapshot=Capture();snapshot.Validate();
        var p = new RuntimePreset { Name=name, Settings=snapshot.Copy() };
        Document.Presets.Add(p); SelectedId=p.Id; RefreshPresets();
    }
    public static void Overwrite() { var snapshot=Capture();snapshot.Validate();RequireSelected().Settings=snapshot.Copy();InvalidateDirty(); }
    public static void Rename() { RequireSelected().Name=ValidName(); RefreshPresets(); }
    public static void Delete()
    {
        var p=RequireSelected(); Document.Presets.Remove(p);
        SinmaiAssist.KeyBindConfig.Actions.Remove("preset:"+p.Id); SelectedId=null; RefreshPresets();
    }
    private static RuntimePreset RequireSelected() => Selected ?? throw new InvalidOperationException("Select a preset first.");
    public static void ApplySelected() => Apply(RequireSelected().Settings.Copy());
    public static void RefreshPresets()
    {
        InvalidateDirty();
        Commands.RemovePresetOperations();
        foreach (var preset in Document.Presets)
        {
            var p=preset;
            Commands.Add("preset:"+p.Id, "Presets", p.Name, ()=>Apply(p.Settings.Copy()));
        }
    }
}
