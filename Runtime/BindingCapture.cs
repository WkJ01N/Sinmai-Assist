using System;
using System.Collections.Generic;
using System.Linq;

namespace SinmaiAssist.Runtime;

// Platform independent state machine; keyboard polling and persistence are adapters.
internal sealed class BindingCapture
{
    public string Waiting { get; private set; }
    public string Error { get; private set; }
    public void Click(string id, Func<string, string> get, Action<string, string> set)
    {
        Error = null;
        if (Waiting == id) { Waiting = null; return; }
        Waiting = null;
        if (get(id) != "None") set(id, "None");
        else Waiting = id;
    }
    public void Cancel() { Waiting = null; }
    public bool Accept(string key, IEnumerable<string> ids, Func<string, string> get, Action<string, string> set)
    {
        if (Waiting == null) return false;
        if (key == "Escape") { Cancel(); return false; }
        var owner = ids.FirstOrDefault(id => id != Waiting && get(id) == key && key != "None");
        if (owner != null) { Error = "Already bound: " + owner; return false; }
        set(Waiting, key);
        Waiting = null;
        Error = null;
        return true;
    }
}
