using System;
using System.Collections.Generic;

namespace ThorusCommon.SQLite
{
    public class GeographyDB : IDisposable
    {
        private SQLiteConnection _db = null;
        private readonly string _path = null;

        public GeographyDB(string path, bool write)
        {
            _path = path;
            ReOpen(write);
        }

        private void ReOpen(bool write)
        {
            Close();

            SQLiteOpenFlags flags =
                write ? SQLiteOpenFlags.ReadWrite : SQLiteOpenFlags.ReadOnly;

            _db = new SQLiteConnection(_path, flags);
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

        public TableQuery<Region> Regions
        {
            get
            {
                return _db.Table<Region>();
            }
        }

        public TableQuery<City> Cities
        {
            get
            {
                return _db.Table<City>();
            }
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
                    Close();

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
