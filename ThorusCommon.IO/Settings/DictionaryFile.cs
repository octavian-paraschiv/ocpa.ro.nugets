using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ThorusCommon.IO.Settings
{
    public delegate void DictionaryUpdatedHandler();

    public class DictionaryFile
    {
        private readonly object _lock = new object();
        private Dictionary<string, string> _nodes = new Dictionary<string, string>();
        private readonly string _filePath;

        public event DictionaryUpdatedHandler DictionaryUpdated;

        public Dictionary<string, string> Nodes
        {
            get
            {
                lock (_lock)
                {
                    return _nodes;
                }
            }
        }

        public string this[string key]
        {
            get
            {
                lock (_lock)
                {
                    if (_nodes.TryGetValue(key, out string val))
                        return val;

                    return null;
                }
            }

            set
            {
                lock (_lock)
                {
                    if (value != null)
                    {
                        if (_nodes.ContainsKey(key))
                            _nodes[key] = value;
                        else
                            _nodes.Add(key, value);
                    }
                    else if (_nodes.ContainsKey(key))
                    {
                        _nodes.Remove(key);
                    }
                }
            }
        }

        public DictionaryFile(string path)
        {
            FileInfo fi = new FileInfo(path);
            _filePath = fi.FullName;

            ReadFile();

            var fsw = new FileSystemWatcher(fi.DirectoryName, $"*{fi.Extension}"); // fi.Extension includes the leading dot character (.)
            fsw.Changed += OnFileChanged;
            fsw.Created += OnFileChanged;
            fsw.Deleted += OnFileChanged;
            fsw.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed:
                    {
                        if (string.Equals(e.FullPath, _filePath, StringComparison.OrdinalIgnoreCase))
                            ReadFile();
                    }
                    break;

                case WatcherChangeTypes.Deleted:
                    {
                        lock (_lock)
                        {
                            _nodes.Clear();
                        }

                        DictionaryUpdated?.Invoke();
                    }
                    break;
            }
        }

        private bool ReadFile()
        {
            try
            {
                var content = File.ReadAllText(_filePath);
                var nodes = JsonSerializer.Deserialize<Dictionary<string, string>>(content);

                if (nodes?.Count > 0)
                {
                    lock (_lock)
                    {
                        _nodes = new Dictionary<string, string>(nodes);
                    }

                    DictionaryUpdated?.Invoke();
                    return true;
                }

            }
            catch
            {
                // Not interested in actual exception
            }

            return false;
        }

        public bool SaveFile()
        {
            try
            {
                var content = JsonSerializer.Serialize(Nodes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, content);
            }
            catch
            {
                // Not interested in actual exception
            }

            return false;
        }
    }
}
