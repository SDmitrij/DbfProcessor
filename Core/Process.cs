using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System.IO;

namespace DbfProcessor.Core
{
    public static class Process
    {
        private static Config Config => ConfigInstance.GetInstance().Config();
        private static Logging Log => Logging.GetLogging();

        public static void Run()
        {
            try
            {
                CheckInfrastructure();
                Exchange exchange = new Exchange();
                exchange.Run();
            } 
            catch (ExchangeException e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private static void CheckInfrastructure()
        {
            var exchangeDir = new DirectoryInfo(Config.ExchangeDirectory);
            if (!Directory.Exists(Config.ExchangeDirectory))
                throw new ExchangeException("Directory that keeps dbfs from exchange is not exists");

            if (!Directory.Exists(Config.DbfLookUpDir))
                throw new ExchangeException("Directory that handle dbfs is not exists");

            if (exchangeDir.GetFiles("*.zip").Length == 0
                && exchangeDir.GetFiles("*.dbf").Length == 0)
                throw new ExchangeException("Exchange directory is empty");
        }
    }
}
