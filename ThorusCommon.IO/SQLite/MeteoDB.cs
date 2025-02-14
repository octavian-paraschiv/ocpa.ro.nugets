using MathNet.Numerics.LinearAlgebra.Single;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ThorusCommon.SQLite
{
    public class MeteoDB : IDisposable
    {
        private SQLiteConnection _db = null;
        private string _origPath = null;
        private static readonly string _templatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Template.db3");
        private bool _write = false;

        public static MeteoDB OpenOrCreate(string path, bool write)
        {
            if (File.Exists(path))
            {
                // Open existing
                return new MeteoDB(path, write);
            }

            if (File.Exists(_templatePath))
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

            if (!File.Exists(_origPath) && File.Exists(_templatePath))
                _origPath = _templatePath;

            SQLiteOpenFlags flags =
                write ? SQLiteOpenFlags.ReadWrite : SQLiteOpenFlags.ReadOnly;

            _write = write;

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

        private bool disposedValue;

        public IEnumerable<Data> GetData(string regionCode = "RO",
            GridCoordinates gc = null,
            int skip = 0,
            int take = -1,
            int precision = 2)
        {
            float mul = (float)Math.Pow(10, -precision);

            if (gc == null)
                gc = new GridCoordinates { R = 0, C = 0 };

            return _db.Table<Data>()
                .Where(d => d.RegionCode == regionCode && d.R == gc.R && d.C == gc.C)
                .OrderBy(d => d.Timestamp)
                .Skip(skip)
                .Take(take)
                .AsEnumerable()
                .Select(d => DataBuilder(d, f => mul * f));
        }

        private static Data DataBuilder(Data input, Func<float, float> func)
        {
            var data = new Data();

            typeof(Data)
               .GetProperties(BindingFlags.Instance | BindingFlags.Public)
               .ToList()
               .ForEach(pi =>
               {
                   var inVal = pi.GetValue(input);

                   if (pi.PropertyType == typeof(float))
                       pi.SetValue(data, func((float)inVal));
                   else
                       pi.SetValue(data, inVal);
               });

            return data;
        }


        private readonly Dictionary<string, Data> _dataToSave = new Dictionary<string, Data>();

        public void AddMatrix(string regionCode, string timestamp, string type, DenseMatrix m, int precision = 2)
        {
            int mul = (int)Math.Pow(10, precision);

            for (int r = 0; r < m.RowCount; r++)
            {
                for (int c = 0; c < m.ColumnCount; c++)
                {
                    string key = $"{regionCode}_{timestamp}_{r}_{c}";
                    Data d = null;

                    if (!_dataToSave.ContainsKey(key))
                    {
                        _dataToSave.Add(key, new Data
                        {
                            C = c,
                            R = r,
                            RegionCode = regionCode,
                            Timestamp = timestamp
                        });
                    }

                    d = _dataToSave[key];

                    var pi = d.GetType().GetProperty(type);
                    if (pi != null)
                        pi.SetValue(d, (int)(mul * m[r, c]));
                }
            }
        }

        public void SaveAndClose()
        {
            if (_write)
            {
                if (_dataToSave?.Count > 0)
                    _db.InsertAll(_dataToSave.Values);

                _db.Execute("VACUUM"); // Shrink DB
            }

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    SaveAndClose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
