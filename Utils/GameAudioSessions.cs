using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SinmaiAssist.Runtime;

namespace SinmaiAssist.Utils;

// Windows Core Audio shared-mode sessions. Never touch endpoint volume or mute.
// COM vtable layouts follow mmdeviceapi.h, audiopolicy.h and audioclient.h.
internal static class GameAudioSessions
{
    private static readonly Guid Context=new Guid("DD088F71-5C74-4612-B11C-A5A9DE3D6F86");
    private static void Check(int hr)=>Marshal.ThrowExceptionForHR(hr);
    private static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    public static IEnumerable<IGameAudioSession> Open()
    {
        var result=new List<IGameAudioSession>();
        object root=null;IDevices devices=null;
        try
        {
            root=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
            Check(((IDeviceEnumerator)root).EnumAudioEndpoints(0,1,out devices)); // render, active
            Check(devices.GetCount(out uint count));
            for(uint d=0;d<count;d++)
            {
                IDevice device=null;object manager=null;ISessionEnumerator sessions=null;
                try
                {
                    Check(devices.Item(d,out device));Check(device.GetId(out string deviceId));
                    var iid=typeof(ISessionManager).GUID;
                    Check(device.Activate(ref iid,23,IntPtr.Zero,out manager));
                    Check(((ISessionManager)manager).GetSessionEnumerator(out sessions));Check(sessions.GetCount(out int total));
                    for(int s=0;s<total;s++)
                    {
                        object control=null;
                        try
                        {
                            Check(sessions.GetSession(s,out control));
                            var detail=(ISessionControl)control;
                            int hr=detail.GetProcessId(out uint process);
                            Check(hr);
                            if(hr!=0)continue; // shared across processes: do not claim ownership
                            Check(detail.GetSessionInstanceIdentifier(out string id));
                            result.Add(new Session(control,deviceId+":"+id,process));control=null;
                        }
                        finally {Release(control);}
                    }
                }
                finally {Release(sessions);Release(manager);Release(device);}
            }
            return result;
        }
        catch {foreach(var session in result)session.Dispose();throw;}
        finally {Release(devices);Release(root);}
    }
    private sealed class Session : IGameAudioSession
    {
        private object _control;
        public string Id {get;}
        public uint ProcessId {get;}
        public Session(object control,string id,uint process){_control=control;Id=id;ProcessId=process;}
        public bool Muted
        {
            get {Check(((ISimpleVolume)_control).GetMute(out bool muted));return muted;}
            set {var context=Context;Check(((ISimpleVolume)_control).SetMute(value,ref context));}
        }
        public void Dispose(){Release(_control);_control=null;}
    }
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow,uint state,out IDevices devices);
    }
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevices
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index,out IDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice
    {
        [PreserveSig] int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);
        [PreserveSig] int OpenPropertyStore(uint access,out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
    }
    [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionManager
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid session,uint flags,out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid session,uint flags,out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
    }
    [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object session);
    }
    [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group,ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
        [PreserveSig] int GetProcessId(out uint process);
    }
    [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleVolume
    {
        [PreserveSig] int SetMasterVolume(float volume,ref Guid context);
        [PreserveSig] int GetMasterVolume(out float volume);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)]bool muted,ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)]out bool muted);
    }
}
