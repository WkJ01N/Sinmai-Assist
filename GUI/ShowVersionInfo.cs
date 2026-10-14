using System;
using System.Runtime.CompilerServices;
using MAI2.Util;
using MAI2System;
using MelonLoader;
using SinmaiAssist.Utils;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class ShowVersionInfo
{
    private static readonly RuntimePropertyReader Keychip = new RuntimePropertyReader(
        () => Type.GetType("AMDaemon.System, AMDaemon.NET", false), "KeychipId");
    private static readonly InfoSnapshot Snapshot = new InfoSnapshot(new[]
    {
        new InfoSnapshot.Field { Label = "Client Version", Read = () => $"{SinmaiAssist.GameID} {SinmaiAssist.GameVersion}" },
        new InfoSnapshot.Field { Label = "Data Version", Read = ReadDataVersion },
        new InfoSnapshot.Field { Label = "Current Title Server", Read = Server.GetTitleServerUri },
        new InfoSnapshot.Field { Label = "Keychip", Read = Keychip.ReadString },
        new InfoSnapshot.Field { Label = "1P UserId", Read = () => ReadUserId(0) },
        new InfoSnapshot.Field { Label = "2P UserId", Read = () => ReadUserId(1) }
    }, message => MelonLogger.Warning(message));

    private static GUIStyle _textStyle;
    private static GUIStyle _shadowStyle;
    private static GUIContent _content;
    private static string _drawnText;
    private static float _width = -1;
    private static float _screenHeight = -1;
    private static float _height;

    // Called once per game update, including while the overlay is hidden, so enabling
    // it again always refreshes immediately. Unscaled time keeps this at 1 Hz in SpeedHack.
    public static void Update()
    {
        Snapshot.Update(SinmaiAssist.MainConfig.ModSetting.ShowInfo, Time.realtimeSinceStartup,
            $"{BuildInfo.Name} {BuildInfo.Version} ({BuildInfo.CommitHash})", SinmaiAssist.MainConfig.ModSetting.SafeMode);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadDataVersion()
    {
        var version = Singleton<SystemConfig>.Instance.config.dataVersionInfo.versionNo;
        return $"{version.versionString} {version.releaseNoAlphabet}";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadUserId(long index)
    {
        var user = User.GetUserData(index);
        if (user == null || !user.IsEntry) return "Not entered";
        return user.IsGuest() ? "Guest" : user.Detail.UserID.ToString();
    }

    // Rendering only consumes a string snapshot; it never calls a native information getter.
    public static void OnGUI()
    {
        if (_textStyle == null)
        {
            _textStyle = new GUIStyle { fontSize = 24, alignment = TextAnchor.UpperLeft,
                wordWrap = true, richText = false, normal = new GUIStyleState { textColor = Color.white } };
            _shadowStyle = new GUIStyle(_textStyle) { normal = new GUIStyleState { textColor = Color.black } };
        }
        float width = InfoPanelLayout.Width(Screen.width);
        if (_drawnText != Snapshot.Text || _width != width || _screenHeight != Screen.height)
        {
            _drawnText = Snapshot.Text;
            _width = width;
            _screenHeight = Screen.height;
            _content = new GUIContent(_drawnText);
            _height = width > 0 ? InfoPanelLayout.Height(Screen.height, _textStyle.CalcHeight(_content, width)) : 0;
        }
        if (_width <= 0 || _height <= 0) return;
        UnityEngine.GUI.Label(new Rect(12, 42, _width, _height), _content, _shadowStyle);
        UnityEngine.GUI.Label(new Rect(10, 40, _width, _height), _content, _textStyle);
    }
}
