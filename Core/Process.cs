using DbfProcessor.Core.Storage;
using DbfProcessor.Out;

namespace DbfProcessor.Core
{
    public static class Process
    {
        private static Logging Log => Logging.GetLogging();

        public static void Run()
        {
            Interaction interaction = new Interaction();
            Exchange exchange = new Exchange();

            interaction.ApplyBase();
            exchange.Run();
            interaction.Take(exchange.GetResults());
            
            Log.Write();
        }
    }
}
