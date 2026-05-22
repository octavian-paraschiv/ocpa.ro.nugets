using System;
using System.Reflection;

namespace FileUploader;

public static class Authentication
{
    static readonly Assembly _extAsm = Load();

    private static Assembly Load()
    {
        using var stream = typeof(Authentication).Assembly.GetManifestResourceStream("FileUploader.ext.dll");
        var bytes = new byte[stream.Length];

        _ = stream.Read(bytes, 0, bytes.Length);

        return Assembly.Load(bytes);
    }

    private static string Ext(string method, params object[] args)
    {
        try
        {
            var field = _extAsm?.GetType("ext")?.GetField(method, BindingFlags.Static | BindingFlags.Public);
            if (field != null)
            {
                switch ((args?.Length).GetValueOrDefault())
                {
                    case 0:
                        return (field.GetValue(null) as Func<string>)();
                    case 1:
                        return (field.GetValue(null) as Func<string, string>)(args[0] as string);
                    case 2:
                        return (field.GetValue(null) as Func<string, string, string>)(args[0] as string, args[1] as string);
                }
            }
        }
        catch
        {
            // not relevant
        }

        return null;
    }

    public static string Hash(string user, string pass) => Ext("h", user, pass);
    public static string SendHash(string user, string pass) => Ext("i", user, pass);
    public static string GetSeed(string reqPass) => Ext("j", reqPass);
    public static string CalcHash(string userPassHash, string seed) => Ext("k", userPassHash, seed);
}
