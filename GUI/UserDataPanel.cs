using System;
using System.IO;
using System.Linq;
using MAI2.Util;
using Manager;
using MelonLoader;
using SinmaiAssist.Utils;
using UnityEngine;

namespace SinmaiAssist.GUI;

public class UserDataPanel
{
    private static UserData _player1 = null;
    private static UserData _player2 = null;
    private static bool _isNewItem = false;
    
    private enum CollectionType
    {
        Icon = UserData.Collection.Icon,
        Plate = UserData.Collection.Plate,
        Title = UserData.Collection.Title,
        Partner = UserData.Collection.Partner,
        Frame = UserData.Collection.Frame
    }
    
    private static string[] _userInputId = ["", "", "", "", "", "", ""];
    
    public static void OnGUI()
    {
        GUILayout.Label($"User Info", MainGUI.Style.Title);
        try
        {
            _player1 = Singleton<UserDataManager>.Instance.GetUserData(0);
            _player2 = Singleton<UserDataManager>.Instance.GetUserData(1);
        }
        catch (Exception)
        {
            _player1 = null;
            _player2 = null;
        }
        if (_player1?.Detail == null || _player2?.Detail == null)
        {
            GUILayout.Label("User data is not ready.", MainGUI.Style.Text);
            return;
        }
        GUILayout.Label($"1P: {_player1.Detail.UserName} ({_player1.Detail.UserID})", MainGUI.Style.Text);
        GUILayout.Label($"2P: {_player2.Detail.UserName} ({_player2.Detail.UserID})", MainGUI.Style.Text);
        
        GUILayout.Label("Add Collections", MainGUI.Style.Title);
        foreach (CollectionType type in Enum.GetValues(typeof(CollectionType)))
        {
            GUILayout.BeginHorizontal();
            int typeId = (int)type;
            GUILayout.Label(type.ToString(), new GUIStyle(MainGUI.Style.Text){fixedWidth = 50});
            _userInputId[typeId] = CommandUI.Text(_userInputId[typeId]);
            CommandUI.Button("user.add"+type,"Add",GUILayout.Width(50));
            GUILayout.EndHorizontal();
        }
        CommandUI.Toggle("user.new",_isNewItem,"Is New Item");
        
        GUILayout.Label("Unlock Music", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Music", new GUIStyle(MainGUI.Style.Text){fixedWidth = 50});
        _userInputId[0] = CommandUI.Text(_userInputId[0]);
        CommandUI.Button("user.addMusic","Add",GUILayout.Width(50));
        GUILayout.EndHorizontal();
        
        GUILayout.Label("MaiMile", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        GUILayout.Label("MaiMile", new GUIStyle(MainGUI.Style.Text){fixedWidth = 50});
        _userInputId[6] = CommandUI.Text(_userInputId[6]);
        CommandUI.Button("user.addMaiMile","Add",GUILayout.Width(50));
        GUILayout.EndHorizontal();
        
        GUILayout.Label("User Data Backup", MainGUI.Style.Title);
        GUILayout.BeginHorizontal();
        CommandUI.Button("user.export0","1P");
        CommandUI.Button("user.export1","2P");
        GUILayout.EndHorizontal();
        
    }

    internal static void ToggleNew() { _isNewItem=!_isNewItem; }
    internal static void ExecuteAdd(string item)
    {
        for(int p=0;p<2;p++)
        {
            if(!Singleton<UserDataManager>.Instance.GetUserData(p).IsEntry) continue;
            if(item=="Music") UnlockMusic(p,_userInputId[0]);
            else if(item=="MaiMile") AddMaiMile(p,_userInputId[6]);
            else {var type=(CollectionType)Enum.Parse(typeof(CollectionType),item);AddCollections(p,type,_userInputId[(int)type]);}
        }
    }
    private static void AddCollections(long index, CollectionType type, string input)
    {
        UserData userData = Singleton<UserDataManager>.Instance.GetUserData(index);
        if (userData.IsGuest())
        {
            GameMessageManager.SendMessage((int)index,"Guest Account\nUnable to add collections");
            return;
        }
        try
        {
            if (int.TryParse(input, out int id))
            {
                if (userData.AddCollections((UserData.Collection)type, id, _isNewItem))
                {
                    GameMessageManager.SendMessage((int)index,$"Add Collections \n{type} {id}" + (_isNewItem ? " (New Item)" : "") );
                }
                else
                {
                    GameMessageManager.SendMessage((int)index,$"Failed to add Collections or already added\n{type} {id}");
                }
            }
            else
            {
                GameMessageManager.SendMessage((int)index,$"Invalid ID\n {input}");
            }
        }
        catch (Exception e)
        {
            GameMessageManager.SendMessage((int)index,$"Unknown error");
            MelonLogger.Error(e);
        }
    }

    private static void UnlockMusic(long index, string input)
    {
        UserData userData = Singleton<UserDataManager>.Instance.GetUserData(index);
        if (userData.IsGuest())
        {
            GameMessageManager.SendMessage((int)index,"Guest Account\nUnable to unlock music");
            return;
        }
        try
        {
            if (int.TryParse(input, out int id))
            {
                if (!userData.IsUnlockMusic(UserData.MusicUnlock.Base, id))
                {
                    if (userData.AddUnlockMusic(UserData.MusicUnlock.Base, id))
                    {
                        GameMessageManager.SendMessage((int)index,$"Unlock Music \n{id}");
                    }
                    else
                    {
                        GameMessageManager.SendMessage((int)index,$"Failed to unlock music or already unlocked \n{id}");
                    }
                }
                else if(!userData.IsUnlockMusic(UserData.MusicUnlock.Master, id))
                {
                    userData.AddUnlockMusic(UserData.MusicUnlock.Master, id);
                    userData.AddUnlockMusic(UserData.MusicUnlock.ReMaster, id);
                    GameMessageManager.SendMessage((int)index,$"Unlock Master \n{id}");
                }
                else
                {
                    GameMessageManager.SendMessage((int)index,$"Failed to unlock Master or already unlocked\n{id}");
                }
            }
            else
            {
                GameMessageManager.SendMessage((int)index,$"Invalid ID\n {input}");
            }
        }
        catch (Exception e)
        {
            GameMessageManager.SendMessage((int)index,$"Unknown error");
            MelonLogger.Error(e);
        }
    }

    private static void AddMaiMile(long index, string input)
    {
        UserData userData = Singleton<UserDataManager>.Instance.GetUserData(index);
        if (SinmaiAssist.GameVersion < 25000)
        {
            GameMessageManager.SendMessage((int)index,"MaiMile is not supported in this version");
            return;
        }
        if (userData.IsGuest())
        {
            GameMessageManager.SendMessage((int)index,"Guest Account\nUnable to add MaiMile");
            return;
        }
        try
        {
            if (int.TryParse(input , out int addMile))
            {
                var haveMile = userData.Detail.Point;
                if (haveMile + addMile >= 99999)
                    addMile = 99999 - haveMile;
                var addMileBefore = haveMile + addMile;
                
                userData.AddPresentMile(addMile);
                GameMessageManager.SendMessage((int)index,$"Add {addMile} MaiMile\n ({haveMile} -> {addMileBefore})");
            }
            else
            {
                GameMessageManager.SendMessage((int)index,$"Invalid MaiMile\n {input}");
            }
        }
        catch (Exception e)
        {
            GameMessageManager.SendMessage((int)index,$"Unknown error");
            MelonLogger.Error(e);
        }
    }
}
