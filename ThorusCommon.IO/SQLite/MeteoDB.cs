using MathNet.Numerics.LinearAlgebra.Single;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ThorusCommon.SQLite
{
    public partial class MeteoDB
    {
        private SQLiteConnection _db = null;
        private string _origPath = null;

        static readonly string _templatePath;

        const int RequiredSchemaVersion = 2;

        static MeteoDB()
        {
            _templatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Template.db3");
        }

        public static MeteoDB OpenOrCreate(string path, bool write)
        {
            if (File.Exists(path))
            {
                // Open existing
                return new MeteoDB(path, write);
            }

            if (write && File.Exists(_templatePath))
            {
                // Create new DB from DB template
                File.Copy(_templatePath, path, true);
                return new MeteoDB(path, write);
            }

            return null;
        }

        private MeteoDB(string path, bool write)
        {
            _origPath = path;
            ReOpen(write);
        }

        private void ReOpen(bool write)
        {
            Close();

            if (!File.Exists(_origPath))
            {
                if (File.Exists(_templatePath))
                    _origPath = _templatePath;
            }

            SQLiteOpenFlags flags =
                write ? SQLiteOpenFlags.ReadWrite : SQLiteOpenFlags.ReadOnly;

            _db = new SQLiteConnection(_origPath, flags);
        }

        public void Close()
        {
            if (_db != null)
            {
                _db.Close();
                _db.Dispose();
                _db = null;
            }
        }

        private List<Region> _regions = null;
        public List<Region> Regions
        {
            get
            {
                if (_regions == null)
                    _regions = _db.Table<Region>().ToList();

                return _regions;
            }
        }

        public TableQuery<Data> Data
        {
            get
            {
                return _db.Table<Data>();
            }
        }

        private Dictionary<string, Data> _dataToSave = new Dictionary<string, Data>();

        public void AddMatrix(int regionId, string timestamp, string type, DenseMatrix m)
        {
            for (int r = 0; r < m.RowCount; r++)
            {
                for (int c = 0; c < m.ColumnCount; c++)
                {
                    string key = $"{regionId}_{timestamp}_{r}_{c}";
                    Data d = null;

                    if (!_dataToSave.ContainsKey(key))
                    {
                        _dataToSave.Add(key, new Data
                        {
                            C = c,
                            R = r,
                            RegionId = regionId,
                            Timestamp = timestamp
                        });
                    };

                    d = _dataToSave[key];

                    var pi = d.GetType().GetProperty(type);
                    if (pi != null)
                        pi.SetValue(d, m[r, c]);
                }
            }


        }

        public void SaveAndClose()
        {
            _db.InsertAll(_dataToSave.Values);
            _db.Execute("VACUUM"); // Shrink DB
            Close();
        }

        public void PurgeAll<T>()
        {
            _db.Execute($"DELETE FROM {typeof(T).Name.ToUpperInvariant()}");
        }

        public void InsertAll<T>(IEnumerable<T> values)
        {
            _db.InsertAll(values);
            _db.Execute("VACUUM"); // Shrink DB
        }
    }
}
