using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace ByPassProxy
{
    public class ConfigStore
    {
        Configuration _config;
        KeyValueConfigurationCollection _settings;

        public ConfigStore()
        {
            _config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            _settings = _config.AppSettings.Settings;
        }

        public void Save()
        {
            _config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(_config.AppSettings.SectionInformation.Name);
        }

        public int ListenPort
        {
            get
            {
                int value;
                return int.TryParse(ConfigurationManager.AppSettings["ListenPort"], out value) ? value : 8080;
            }
            set
            {
                Set("ListenPort", value.ToString());
            }
        }
        public string Target
        {
            get 
            {
                var value = ConfigurationManager.AppSettings["Target"];
                return string.IsNullOrEmpty(value) ? "localhost:80" : value;
            }
            set { Set("Target", value); }
        }
        public bool SingleConnection
        {
            get
            {
                bool value;
                return bool.TryParse(ConfigurationManager.AppSettings["SingleConnection"], out value) ? value : false;
            }
            set { Set("SingleConnection", value.ToString()); }
        }
        public int DelayMs
        {
            get
            {
                int value;
                return int.TryParse(ConfigurationManager.AppSettings["DelayMs"], out value) ? value : 0;
            }
            set { Set("DelayMs",value.ToString()); }
        }

        private void Set(string key, string value)
        {
            if (_settings.AllKeys.Contains(key))
                _settings[key].Value = value;
            else
                _settings.Add(key, value);
        }
    }
}
