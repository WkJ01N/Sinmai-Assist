using Manager;
using Net.Packet;
using Net.Packet.Helper;
using SinmaiAssist.Common;
using SinmaiAssist.Utils;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class DummyLoginPanel
{
    public static string DummyLoginCode = "";
    public static string DummyUserId = "";
    public static bool CodeLoginFlag = false;
    public static bool UserIdLoginFlag = false;
    
    public static void OnGUI()
    {
        GUILayout.Label("Code:", MainGUI.Style.Text);
        DummyLoginCode = CommandUI.Area(DummyLoginCode, GUILayout.Height(100f));
        CommandUI.Button("login.code","Code Login");
        GUILayout.Label("UserID:", MainGUI.Style.Text);
        DummyUserId = CommandUI.Text(DummyUserId, GUILayout.Height(20f));
        CommandUI.Button("login.id","UserId Login");
        GUILayout.Label($"AMDaemon BootTime: {AMDaemon.Allnet.Auth.AuthTime}", MainGUI.Style.Text);
    }
    internal static void LoginCode()
    {
        CodeLoginFlag=true;
        if(SinmaiAssist.GameID!="SDGB") DummyAimeLogin.ReadCard();
    }
    internal static void LoginId()
    {
        UserIdLoginFlag=true;
        if(SinmaiAssist.GameID!="SDGB") DummyAimeLogin.ReadCard("12312312312312312312",DummyLoginCode);
    }
}
