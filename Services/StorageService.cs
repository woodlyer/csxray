using System;
using System.IO;
using System.Text.Json;
using CsXray.Models;

namespace CsXray.Services
{
    public class StorageService
    {
        private readonly string _configDir;
        private readonly string _settingsFile;

        public StorageService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configDir = Path.Combine(baseDir, "config");
            _settingsFile = Path.Combine(_configDir, "settings.json");

            if (!Directory.Exists(_configDir))
            {
                Directory.CreateDirectory(_configDir);
            }
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load settings failed: {ex.Message}");
            }

            var defaultSettings = new AppSettings();

            // Try load default servers from default-servers.json if first run
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string defFile = Path.Combine(baseDir, "default-servers.json");
                if (File.Exists(defFile))
                {
                    string content = File.ReadAllText(defFile);
                    var imported = LinkParser.ImportFromText(content);
                    if (imported.Count > 0)
                    {
                        defaultSettings.Nodes.AddRange(imported);
                        defaultSettings.ActiveNodeId = imported[0].Id;
                    }
                }
            }
            catch { }

            return defaultSettings;
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save settings failed: {ex.Message}");
            }
        }
    }
}
