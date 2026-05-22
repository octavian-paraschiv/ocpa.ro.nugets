using System;

namespace ThorusCommon.IO.Exceptions;

public class ThorusException(string s) : Exception(s)
{
    public ThorusException() : this(null)
    {
    }
}
