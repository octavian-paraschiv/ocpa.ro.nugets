using System;
using System.Text;
using System.IO;
using MathNet.Numerics.LinearAlgebra.Single;

namespace ThorusCommon.IO
{
    public class MatrixFile
    {
        protected static bool _saveAsBinaryFiles = true;

        protected DenseMatrix _matrix = null;
        protected string _fileName = string.Empty;

        public const uint MagicNumberBinary = 0xFEDCBA98;

        public bool IsBinaryFile { get; private set; }

        public DenseMatrix Matrix
        {
            get { return _matrix.Clone() as DenseMatrix; }
            set { _matrix = value.Clone() as DenseMatrix; }
        }

        public void Save(string fmt = null)
        {
            string dir = Path.GetDirectoryName(_fileName);
            if (Directory.Exists(dir) == false)
                Directory.CreateDirectory(dir);

            try
            {
                if (_saveAsBinaryFiles)
                    InternalSave_BIN(fmt);
                else
                    InternalSave_TXT(fmt);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public MatrixFile(string fileName, bool throwException)
        {
            _fileName = fileName;

            if (File.Exists(fileName))
            {
                try
                {
                    try
                    {
                        byte[] buffer = File.ReadAllBytes(_fileName);

                        using (MemoryStream ms = new MemoryStream(buffer))
                        using (BinaryReader br = new BinaryReader(ms))
                        {
                            if (MagicNumberBinary == br.ReadUInt32())
                            {
                                InternalLoad_BIN(br);
                                return;
                            }
                        }
                    }
                    catch
                    {
                    }

                    InternalLoad_TXT();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            if (throwException)
                throw new Exception("Matrix not initialized.");
        }

        protected virtual void InternalLoad_TXT()
        {
            string[] lines = File.ReadAllLines(_fileName);
            string[] elements = lines[0].Split(" ,".ToCharArray());

            _matrix = new DenseMatrix(lines.Length, elements.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                elements = line.Split(" ,".ToCharArray());

                for (int j = 0; j < elements.Length; j++)
                {
                    float elemValue = 0;
                    float.TryParse(elements[j], out elemValue);
                    _matrix.At(i, j, elemValue);
                }
            }
        }

        protected virtual void InternalLoad_BIN(BinaryReader br)
        {
            int rows = br.ReadInt32();
            int cols = br.ReadInt32();

            _matrix = new DenseMatrix(rows, cols);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    float elemValue = br.ReadSingle();
                    _matrix.At(i, j, elemValue);
                }
            }
        }

        protected virtual void InternalSave_TXT(string fmt = null)
        {
            StringBuilder sb = new StringBuilder();

            int lines = 0;

            for (int i = 0; i < _matrix.RowCount; i++)
            {
                string line = "";

                for (int j = 0; j < _matrix.ColumnCount; j++)
                {
                    float val = _matrix[i, j];

                    if (string.IsNullOrEmpty(fmt))
                    {
                        if (val >= 0)
                            line += string.Format("{0:0000.00},", val);
                        else
                            line += string.Format("{0:000.00},", val);
                    }
                    else
                    {
                        line += string.Format("{0},", val.ToString(fmt));
                    }
                }

                sb.AppendLine(line.Trim(','));
                lines++;
            }

            
            File.WriteAllText(_fileName, sb.ToString());
        }

        protected virtual void InternalSave_BIN(string fmt = null)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(MagicNumberBinary);
                bw.Write(_matrix.RowCount);
                bw.Write(_matrix.ColumnCount);

                for (int i = 0; i < _matrix.RowCount; i++)
                {
                    for (int j = 0; j < _matrix.ColumnCount; j++)
                    {
                        float val = _matrix[i, j];
                        bw.Write(val);
                    }
                }

                File.WriteAllBytes(_fileName, ms.GetBuffer());
            }
        }
    }

    public static class DataReader
    {
        public static float ReadFromFile(string filePath, int r, int c, float defaultValue = 0f)
        {
            float val = defaultValue;

            using (FileStream ms = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (MatrixFile.MagicNumberBinary == br.ReadUInt32())
                {
                    int rows = br.ReadInt32();
                    int cols = br.ReadInt32();

                    long offset = (r * cols + c) * sizeof(float);
                    if (ms.Seek(offset, SeekOrigin.Current) > 0)
                        return br.ReadSingle();
                }
            }

            return defaultValue;
        }
    }
}
