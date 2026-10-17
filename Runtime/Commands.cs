using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using SinmaiAssist.Config;
using UnityEngine;

namespace SinmaiAssist.Runtime;

internal static class Commands
{
    internal sealed class Operation
    {
        public string Id, Group, Label;
        public Action Run;
        public Func<string> Unavailable;
        public Func<KeyBindConfig.Key> ReadKey;
        public Action<KeyBindConfig.Key> WriteKey;
        public bool Hold;
        internal KeyCode CachedKey;
    }
    public static readonly Dictionary<string, Operation> All = new Dictionary<string, Operation>();
    private static readonly HashSet<string> Pending = new HashSet<string>();
    internal static readonly BindingCapture Capture = new BindingCapture();
    private static readonly KeyCode[] KeyboardKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
        .Where(k => k > KeyCode.None && k < KeyCode.Mouse0).Distinct().ToArray();
    private static bool _waitForRelease;
    public static bool TextEditing;
    public static string Status = "";
    private static bool _bindingsDirty=true;
    private static Operation[] _dispatch=Array.Empty<Operation>();
    public static void Add(string id, string group, string label, Action run, Func<string> unavailable = null,
        Func<KeyBindConfig.Key> read = null, Action<KeyBindConfig.Key> write = null, bool hold = false)
    {
        All[id] = new Operation { Id=id, Group=group, Label=label, Run=run, Unavailable=unavailable,
            ReadKey=read, WriteKey=write, Hold=hold };
        _bindingsDirty=true;
    }
    public static string GetKey(string id)
    {
        if (!All.TryGetValue(id, out var op)) return "None";
        var key = op.ReadKey?.Invoke();
        if (key == null) SinmaiAssist.KeyBindConfig.Actions.TryGetValue(id, out key);
        return key == null || key.KeyCode == KeyCode.None ? "None" : key.KeyCode.ToString();
    }
    public static void SetKey(string id, string key)
    {
        if (All[id].WriteKey != null) All[id].WriteKey(key);
        else SinmaiAssist.KeyBindConfig.Actions[id] = key;
        _bindingsDirty=true;SettingsStore.InvalidateDirty();
    }
    public static bool HasUsableKey(string id)
    {
        if(!_bindingsDirty) return _dispatch.Any(op=>op.Id==id);
        string key=GetKey(id);
        return key!="None" && All.Keys.Count(other=>GetKey(other)==key)==1;
    }
    public static void Enqueue(string id) { if (All.ContainsKey(id)) Pending.Add(id); }
    public static void ClickBinding(string id)
    {
        Capture.Click(id, GetKey, SetKey);
        _waitForRelease = true;
    }
    public static void SuppressUntilRelease() { _waitForRelease=true; }
    public static void Update()
    {
        if(_bindingsDirty)
        {
            foreach(var op in All.Values) op.CachedKey=(KeyCode)Enum.Parse(typeof(KeyCode),GetKey(op.Id));
            _dispatch=All.Values.GroupBy(op=>op.CachedKey).Where(g=>g.Key!=KeyCode.None && g.Count()==1).Select(g=>g.First()).ToArray();
            _bindingsDirty=false;
        }
        if (Capture.Waiting != null)
        {
            if (!_waitForRelease)
            {
                var key = KeyboardKeys.FirstOrDefault(UnityEngine.Input.GetKeyDown);
                if (key != KeyCode.None) { Capture.Accept(key.ToString(), All.Keys, GetKey, SetKey); _waitForRelease = true; }
            }
            if (!KeyboardKeys.Any(UnityEngine.Input.GetKey)) _waitForRelease = false;
        }
        else if (_waitForRelease)
        {
            if (!KeyboardKeys.Any(UnityEngine.Input.GetKey)) _waitForRelease = false;
        }
        else if (!TextEditing && !UnityEngine.Input.GetMouseButtonDown(0))
        {
            foreach (var op in _dispatch)
            {
                var key = op.CachedKey;
                if (op.Hold ? UnityEngine.Input.GetKey(key) : UnityEngine.Input.GetKeyDown(key)) Pending.Add(op.Id);
            }
        }
        // Save observes the final runtime values if another operation is requested in the same frame.
        var requested = Pending.OrderBy(id=>id=="save"?1:0).ToArray(); Pending.Clear();
        foreach (var id in requested)
        {
            if (!All.TryGetValue(id, out var op)) continue;
            try
            {
                string error = op.Unavailable?.Invoke();
                if(Cheat.BatchController.Active && op.Group!="Batch" && op.Group!="Display" && op.Group!="Navigation" && op.Group!="Speed" && op.Group!="Audio")
                    error="Stop Batch before changing score/session settings or data.";
                if (error != null) { Status = error; if(id=="auto.rank")Cheat.RankAutoPlay.ReportBlocked(error); continue; }
                Status=""; op.Run();
            }
            catch (Exception ex) { Status = op.Label + ": " + ex.Message; MelonLogger.Error(ex); }
        }
    }
    public static void RemovePresetOperations()
    {
        foreach (var id in All.Keys.Where(k => k.StartsWith("preset:", StringComparison.Ordinal)).ToArray())
        { All.Remove(id); Pending.Remove(id); }
        Capture.Cancel();
        _bindingsDirty=true;
    }
}
