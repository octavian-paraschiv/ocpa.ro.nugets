using MathNet.Numerics.LinearAlgebra.Single;
using System;
using System.IO;
using System.Text;

namespace ThorusCommon.IO;

public class MatrixFile
{
    protected readonly bool _binaryMode = true;

    protected DenseMatrix _matrix = null;
    protected string _fileName;

    public const uint MagicNumberBinary = 0xFEDCBA98;

    public bool IsBinaryFile { get; }

    public DenseMatrix Matrix
    {
        get { return _matrix.Clone() as DenseMatrix; }
        set { _matrix = value.Clone() as DenseMatrix; }
    }

    public void Save(string fmt = null, bool writeAsBinary = true)
    {
        string dir = Path.GetDirectoryName(_fileName);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        try
        {
            if (_binaryMode || writeAsBinary)
                InternalSaveBinary();
            else
                InternalSaveText(fmt);
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

                    using var ms = new MemoryStream(buffer);
                    using var br = new BinaryReader(ms);

                    if (MagicNumberBinary == br.ReadUInt32())
                    {
                        _binaryMode = true;
                        InternalLoadBinary(br);
                        return;
                    }
                }
                catch
                {
                    // not relevant
                }

                InternalLoadText();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        if (throwException)
            throw new FileNotFoundException("Matrix not initialized.", fileName);
    }

    private void InternalLoadText()
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
                _ = float.TryParse(elements[j], out float elemValue);
                _matrix.At(i, j, elemValue);
            }
        }
    }

    private void InternalLoadBinary(BinaryReader br)
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

    private void InternalSaveText(string fmt = null)
    {
        var sb = new StringBuilder();

        int lines = 0;

        for (int i = 0; i < _matrix.RowCount; i++)
        {
            var lineBuilder = new StringBuilder();

            for (int j = 0; j < _matrix.ColumnCount; j++)
            {
                float val = _matrix[i, j];

                if (string.IsNullOrEmpty(fmt))
                {
                    if (val >= 0)
                        lineBuilder.AppendFormat("{0:0000.00},", val);
                    else
                        lineBuilder.AppendFormat("{0:000.00},", val);
                }
                else
                {
                    lineBuilder.AppendFormat("{0},", val.ToString(fmt));
                }
            }

            sb.AppendLine(lineBuilder.ToString().Trim(','));
            lines++;
        }


        File.WriteAllText(_fileName, sb.ToString());
    }

    private void InternalSaveBinary()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

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

public static class DataReader
{
    public static float ReadFromFile(string filePath, int r, int c, float defaultValue = 0f)
    {
        using var ms = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(ms);

        if (MatrixFile.MagicNumberBinary == br.ReadUInt32())
        {
            int cols = br.ReadInt32();

            long offset = (r * cols + c) * sizeof(float);
            if (ms.Seek(offset, SeekOrigin.Current) > 0)
                return br.ReadSingle();
        }

        return defaultValue;
    }
}
