using SinmaiAssist.Common;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class GraphicPanel
{
    private static string screenWidth = $"{Graphic.GetResolutionWidth()}";
    private static string screenHeight = $"{Graphic.GetResolutionHeight()}";
    private static string frameRate = $"{Graphic.GetMaxFrameRate()}";
    
    
    public static void OnGUI()
    {
        CommandUI.Button("graphic.fullscreen",null,GUILayout.Height(50));
        GUILayout.Label($"Custom Graphic Settings", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label($"Width:", MainGUI.Style.Text);
        screenWidth = CommandUI.Text(screenWidth);
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        GUILayout.Label($"Height:", MainGUI.Style.Text);
        screenHeight = CommandUI.Text(screenHeight);
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        GUILayout.Label($"Max FPS (Unlimited is -1):", MainGUI.Style.Text);
        frameRate = CommandUI.Text(frameRate);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        CommandUI.Button("graphic.apply","Apply");
    }
    internal static void Apply()
    {
        if (int.TryParse(screenWidth, out int widthValue) && int.TryParse(screenHeight, out int heightValue) && int.TryParse(frameRate, out int fpsValue))
        {
            if (widthValue >= 360f && heightValue >= 360f)
            {
                Graphic.SetResolution(widthValue, heightValue);
                Graphic.SetMaxFrameRate(fpsValue);
            }
            else throw new System.ArgumentException("Resolution must be at least 360 x 360.");
        }
        else throw new System.ArgumentException("Enter integer width, height and FPS values.");
    }
}
