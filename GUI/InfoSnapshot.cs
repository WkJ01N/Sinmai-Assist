using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SinmaiAssist.GUI;

// Pure snapshot logic: no native/game types are referenced in the refresh loop.
internal sealed class InfoSnapshot
{
    internal sealed class Field
    {
        public string Label;
        public Func<string> Read;
    }

    private readonly Field[] _fields;
    private readonly Action<string> _report;
    private readonly HashSet<string> _reported = new HashSet<string>();
    private bool _wasVisible;
    private double _nextRefresh;
    public string Text { get; private set; } = "Sinmai-Assist\nPowered by MelonLoader";

    public InfoSnapshot(Field[] fields, Action<string> report)
    {
        _fields = fields;
        _report = report;
    }

    public void Update(bool visible, double now, string heading, bool safeMode)
    {
        if (!visible) { _wasVisible = false; return; }
        if (_wasVisible && now < _nextRefresh) return;
        _wasVisible = true;
        _nextRefresh = now + 1.0;
        var text = new StringBuilder();
        text.AppendLine(heading);
        text.AppendLine("Powered by MelonLoader");
        foreach (var field in _fields)
        {
            string value;
            try
            {
                value = field.Read();
                if (string.IsNullOrWhiteSpace(value)) value = "Not ready";
            }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                value = "Unavailable";
                if (_reported.Add(field.Label + "|" + ex.GetType().FullName + "|" + ex.Message))
                    _report($"[ShowInfo] {field.Label}: {ex.GetType().Name}: {ex.Message}");
            }
            text.Append(field.Label).Append(": ").AppendLine(value);
        }
        if (safeMode) text.AppendLine("Safe Mode");
        Text = text.ToString().TrimEnd();
    }
}

internal static class InfoPanelLayout
{
    public const float MaxWidth = 600f;
    public static float Width(float screenWidth) => Math.Max(0, Math.Min(MaxWidth, screenWidth - 20));
    public static float Height(float screenHeight, float textHeight) =>
        Math.Max(0, Math.Min(textHeight + 4, screenHeight - 44));
}
