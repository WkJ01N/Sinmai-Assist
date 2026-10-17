using System;
using System.Collections.Generic;

namespace SinmaiAssist.Runtime;

internal interface IGameAudioSession : IDisposable
{
    string Id { get; }
    uint ProcessId { get; }
    bool Muted { get; set; }
}

// Only retain original state, never COM objects. Sessions are opened and released
// on the game thread on each scan, including after endpoint changes.
internal sealed class AudioSessionMute
{
    private readonly Dictionary<string,bool> _original=new Dictionary<string,bool>();
    public bool NeedsRestore=>_original.Count!=0;
    public int Matched { get; private set; }
    public string Error { get; private set; }
    public void Update(bool mute,uint processId,IEnumerable<IGameAudioSession> sessions)
    {
        Error=null;Matched=0;
        foreach(var session in sessions)
        {
            try
            {
                if(session.ProcessId!=processId)continue;
                Matched++;
                if(mute)
                {
                    if(!_original.ContainsKey(session.Id))_original.Add(session.Id,session.Muted);
                    if(!session.Muted)session.Muted=true;
                }
                else if(_original.TryGetValue(session.Id,out bool original))
                {
                    // A user may have unmuted externally. Do not undo that edit.
                    if(session.Muted&&!original)session.Muted=false;
                    _original.Remove(session.Id);
                }
            }
            catch(Exception ex){Error="Game audio session mute/restore failed: "+ex.Message;}
            finally {try {session.Dispose();}catch(Exception ex){Error="Game audio session release failed: "+ex.Message;}}
        }
    }
}
