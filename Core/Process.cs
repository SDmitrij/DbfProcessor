using DbfProcessor.Out;

namespace DbfProcessor.Core
{
    public static class Process
    {
        private static Logging Log => Logging.GetLogging();

        public static void Run()
        {
            Exchange exchange = new Exchange();
            exchange.Run();
            Log.Write();
        }
    }
}
