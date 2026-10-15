using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace SinmaiAssist.Config;

public class KeyBindConfig
{
    public Dictionary<string, Key> Actions { get; set; } = new Dictionary<string, Key>();
    public AutoPlayConfig AutoPlay { get; set; } = new AutoPlayConfig();
    public ChartControllerConfig ChartController { get; set; } = new ChartControllerConfig();
    public SpeedHackConfig SpeedHack { get; set; } = new SpeedHackConfig();
    public SinmaiAssistConfig SinmaiAssist { get; set; } = new SinmaiAssistConfig();

    public class AutoPlayConfig
    {
        public Key None { get; set; } = "None";
        public Key Critical { get; set; } = "None";
        public Key Perfect { get; set; } = "None";
        public Key Great { get; set; } = "None";
        public Key Good { get; set; } = "None";
        public Key Random { get; set; } = "None";
        public Key RandomAllPerfect { get; set; } = "None";
        public Key RandomFullComboPlus { get; set; } = "None";
        public Key RandomFullCombo { get; set; } = "None";
    }
    
    public class ChartControllerConfig
    {
        public Key Pause { get; set; } = "None";
        public Key Forward { get; set; } = "None";
        public Key Backward { get; set; } = "None";
        public Key SetRecord { get; set; } = "None";
        public Key ReturnRecord { get; set; } = "None";
    }

    public class SpeedHackConfig
    {
        public Key Increase { get; set; } = "None";
        public Key Decrease { get; set; } = "None";
        public Key Reset { get; set; } = "None";
    }

    public class SinmaiAssistConfig
    {
        public Key ShowUserPanel { get; set; } = "None";
    }
    
    public class Key
    {
        private string _keyCode;

        public Key(string key)
        {
            _keyCode = key;
        }

        public static implicit operator Key(string key) => new Key(key);
        public static implicit operator string(Key key) => key._keyCode;
        public new string ToString() => _keyCode;
        
        public KeyCode KeyCode
        {
            get
            {
                if (_keyCode == "None")
                    return UnityEngine.KeyCode.None;
                if (Enum.TryParse<KeyCode>(_keyCode, out var code) && Enum.IsDefined(typeof(KeyCode),code) && code>=KeyCode.None && code<KeyCode.Mouse0)
                    return code;
                
                return KeyCode.None;
            }
        }
    }

    public class Converter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type == typeof(Key);
        }
        
        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var value = parser.Consume<Scalar>().Value;
            return new Key(value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            if (value is Key key)
            {
                emitter.Emit(new Scalar(key.ToString()));
            }
            else
            {
                emitter.Emit(new Scalar("None"));
            }
        }
    }
}
