using System;
using System.IO;

namespace DbfProcessor.Out
{
    public class Logging
    {
        private static Logging _logging;
        private Logging()
        {
            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results"));
        }

        public void Accept(IResult result)
            =>
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results",
                result.GetFile()), result.GetResult());

        public static Logging GetLogging()
        {
            if (_logging is null) _logging = new Logging();
            return _logging;
        }
    }
}
