using System;
using System.IO;

namespace DbfProcessor.Out
{
    public class Logging
    {
        public Logging()
        {
            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results"));
        }

        public void Accept(IResult result)
        {
            if (!result.GetFile().Equals(string.Empty))
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results",
                    result.GetFile()), result.GetResult());
            ToConsole(result);
        }

        private void ToConsole(IResult result)
        {
            if (result.Type() == LoggingType.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("{0}", result.GetResult());
                Console.ResetColor();
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
                return;
            }
            if (result.Type() == LoggingType.Info)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("{0}", result.GetResult());
                Console.ResetColor();
                return;
            }
        }
    }
}
