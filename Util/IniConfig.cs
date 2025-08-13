using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HacknetChineseSupport.Util
{
    public class IniConfig
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void Load(string filePath)
        {
            string currentSection = "";
            foreach (string line in File.ReadLines(filePath))
            {
                string trimmed = line.Trim();

                // 跳过空行和注释
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";"))
                    continue;

                // 处理节
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    continue;
                }

                // 处理键值对
                int separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex > 0)
                {
                    string key = trimmed.Substring(0, separatorIndex).Trim();
                    string value = trimmed.Substring(separatorIndex + 1).Trim();

                    if (!_sections.ContainsKey(currentSection))
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    _sections[currentSection][key] = value;
                }
            }
        }

        public string GetValue(string section, string key, string defaultValue = "")
            => _sections.TryGetValue(section, out var sectionData) &&
               sectionData.TryGetValue(key, out var value)
                ? value : defaultValue;

        public int GetInt(string section, string key, int defaultValue = 0)
            => int.TryParse(GetValue(section, key), out int result) ? result : defaultValue;

        public bool GetBool(string section, string key, bool defaultValue = false)
            => bool.TryParse(GetValue(section, key), out bool result) ? result : defaultValue;

        public void SetValue(string section, string key, string value)
        {
            if (!_sections.ContainsKey(section))
                _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _sections[section][key] = value;
        }

        public void Save(string filePath)
        {
            using(var writer = new StreamWriter(filePath))
            {
                foreach (var section in _sections)
                {
                    writer.WriteLine($"[{section.Key}]");
                    foreach (var kvp in section.Value)
                    {
                        writer.WriteLine($"{kvp.Key}={kvp.Value}");
                    }
                    writer.WriteLine();
                }
            }
        }
    }
}
