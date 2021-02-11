using System;
using System.Collections.Generic;
using System.IO;

namespace DbfProcessor.Out
{
    public class Logging
    {
        private static Logging _logging;
        private readonly ICollection<IResult> _results = new List<IResult>();

        public void Accept(IResult result) => _results.Add(result);

        public void Write()
        {
            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results"));
                
            foreach (IResult result in _results)
                ResultToFile(result);
        }

        private void ResultToFile(IResult result) =>
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results", 
                result.GetFile()), result.GetResult());

        public static Logging GetLogging()
        {
            if (_logging is null) _logging = new Logging();
            return _logging;
        }
    }
}
