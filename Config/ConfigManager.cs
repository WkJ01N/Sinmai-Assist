using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SinmaiAssist.Config
{
    public class ConfigManager<T> where T : new()
    {
        private readonly string _configPath;
        private T _config;
        private readonly IYamlTypeConverter _customConverter;
        
        public ConfigManager(string configPath = "config.yml", IYamlTypeConverter customConverter = null)
        {
            _configPath = Path.GetFullPath(configPath);
            _customConverter = customConverter;
            InitConfig();
        }

        private void InitConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _config = new T();
                    SaveConfig();
                    MelonLogger.Msg($"Create Default Config '{_configPath}' ");
                    return;
                }

                LoadConfig();
            }
            catch (YamlException ex)
            {
                MelonLogger.Error($"Load Config '{_configPath}' Failed: \n{ex.Message}");
                string backup = _configPath + $".broken.{DateTime.Now:yyyyMMddHHmmss}.{Guid.NewGuid():N}";
                File.Move(_configPath, backup);
                _config = new T();
                SaveConfig();
                MelonLogger.Warning($"Recovered this config only. Original saved to '{backup}'.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Init Config '{_configPath}' Failed: \n{ex.Message}");
                throw;
            }
        }

        public T GetConfig()
        {
            if (_config == null)
            {
                throw new InvalidOperationException("Configuration is not initialized.");
            }
            return _config;
        }
        
        private ISerializer CreateSerializer()
        {
            var builder = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance);

            if (_customConverter != null)
            {
                builder = builder.WithTypeConverter(_customConverter);
            }

            return builder.Build();
        }

        private IDeserializer CreateDeserializer()
        {
            var builder = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance);

            if (_customConverter != null)
            {
                builder = builder.WithTypeConverter(_customConverter);
            }

            return builder.Build();
        }

        public void SaveConfig()
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var serializer = CreateSerializer();
            var yaml = serializer.Serialize(_config);
            AtomicConfigFile.Write(_configPath, yaml);
        }

        
        private void LoadConfig()
        {
            var deserializer = CreateDeserializer();
            var yaml = File.ReadAllText(_configPath);
            _config = deserializer.Deserialize<T>(yaml);
            if (_config == null) _config = new T();
        }

        // Fill null nested nodes/keys from the corresponding default instance. Missing
        // properties already retain their initializers; this also handles explicit YAML null.
        public static void FillDefaults(object config, object defaults)
        {
            foreach (var property in config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0) continue;
                var fallback = property.GetValue(defaults);
                var value = property.GetValue(config);
                if (value == null && fallback != null) { property.SetValue(config, fallback); value = fallback; }
                if (value != null && fallback != null && property.PropertyType.IsClass &&
                    property.PropertyType.Namespace == typeof(T).Namespace)
                    FillDefaults(value, fallback);
            }
        }
    }
}
