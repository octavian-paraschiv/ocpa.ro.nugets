using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace ThorusCommon.IO.Settings
{
    public delegate void DictionaryUpdatedHandler();

    public class DictionaryFile
    {
        object _lock = new object();
        Dictionary<string, string> _nodes = new Dictionary<string, string>();
        FileSystemWatcher _fsw;
        string _filePath;

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

            _fsw = new FileSystemWatcher(fi.DirectoryName, $"*{fi.Extension}"); // fi.Extension includes the leading dot character (.)
            _fsw.Changed += OnFileChanged;
            _fsw.Created += OnFileChanged;
            _fsw.Deleted += OnFileChanged;
            _fsw.EnableRaisingEvents = true;
        }

        private void _fsw_Deleted(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void _fsw_Created(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
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
                var nodes = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

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
            }
            return false;
        }

        public bool SaveFile()
        {
            try
            {
                var content = JsonConvert.SerializeObject(Nodes, Formatting.Indented);
                File.WriteAllText(_filePath, content);
            }
            catch
            {
            }
            return false;
        }
    }
}
