using System;
using System.Reflection;

namespace OPMFileUploader
{
    public static class Authentication
    {
        static readonly Assembly _extAsm = load();

        private static Assembly load()
        {
            using (var stream = typeof(Authentication).Assembly.GetManifestResourceStream("FileUploader.ext.dll"))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
        }

        private static string ext(string method, params object[] args)
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

        public static string hash(string user, string pass) => ext("h", user, pass);
        public static string sendHash(string user, string pass) => ext("i", user, pass);
        public static string getSeed(string reqPass) => ext("j", reqPass);
        public static string calcHash(string userPassHash, string seed) => ext("k", userPassHash, seed);
    }
}
