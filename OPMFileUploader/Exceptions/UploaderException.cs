using System;

namespace FileUploader.Exceptions;

public class UploaderException(string s) : Exception(s)
{
    public UploaderException() : this(null)
    {
    }
}
