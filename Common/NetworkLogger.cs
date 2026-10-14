using HarmonyLib;
using MAI2.Util;
using Manager;
using MelonLoader;
using Net.Packet;
using Net.VO;
using System;
using System.IO;
using System.Collections.Generic;

namespace SinmaiAssist.Common;

public class NetworkLogger
{
    private static readonly object logLock = new object();
    private enum HttpMessageType
    {
        Request,
        Response,
        Null
    }
    [ThreadStatic]
    private static Stack<Packet> _packetStack;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Packet), "set_State")]
    public static void RequestListener(Packet __instance)
    {
        if (__instance.State == PacketState.Ready)
        {
            string content = __instance.Query.GetRequest();
            // string baseUrl = (string)AccessTools.Field(typeof(Packet), "BaseUrl").GetValue(__instance);
            var baseUrl = Singleton<OperationManager>.Instance.GetBaseUri();
            PrintNetworkLog(HttpMessageType.Request, baseUrl + __instance.Query.Api, content);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NetQuery<VOSerializer, VOSerializer>), "SetResponse")]
    public static void ResponseListener(ref string str)
    {
        if (_packetStack != null && _packetStack.Count != 0)
        {
            string ccontent = str;
            var packet = _packetStack.Peek();
            PrintNetworkLog(HttpMessageType.Response, Singleton<OperationManager>.Instance.GetBaseUri() + packet.Query.Api, ccontent);
        }
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Packet), "ProcImpl")]
    public static void ProcImplPrefix(Packet __instance)
    {
        if (_packetStack == null) _packetStack = new Stack<Packet>();
        _packetStack.Push(__instance);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Packet), "ProcImpl")]
    public static void ProcImplFinalizer()
    {
        if (_packetStack != null && _packetStack.Count != 0) _packetStack.Pop();
    }
    private static void PrintNetworkLog(HttpMessageType httpMessageType, string api, string content)
    {
        if (httpMessageType == HttpMessageType.Null) return;
        try
        {
            DateTime now = DateTime.Now;
            string logText = now.ToString("[HH:mm:ss.fff] ");
            logText += $"[{httpMessageType}] [{api}] -> {content}\n";

            if (SinmaiAssist.MainConfig.Common.NetworkLogger.PrintToConsole)
                MelonLogger.Msg($"[NetworkLogger] [{httpMessageType}] [{api}] -> {content}");

            lock (logLock)
            {
                string logDir = Path.Combine($"{BuildInfo.Name}/NetworkLogs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                File.AppendAllText(Path.Combine(logDir, now.ToString("yyyy-MM-dd") + ".log"), logText);
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error(e);
        }
    }
}
