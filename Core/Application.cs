using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System.IO;

namespace DbfProcessor.Core
{
    public class Application
    {
        private readonly Logging _log;
        private readonly ConfigModel _config;
        private readonly Exchange _exchange;

        public Application(Logging logging, Config config, Exchange exchange)
        {
            _log = logging;
            _config = config.Get();
            _exchange = exchange;
        }

        public void Run()
        {
            try
            {
                CheckInfrastructure();
                _exchange.Begin();
            } 
            catch (InfrastructureException e)
            {
                _log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void CheckInfrastructure()
        {
            var exchangeDir = new DirectoryInfo(_config.ExchangeDirectory);
            if (!Directory.Exists(_config.ExchangeDirectory))
                throw new InfrastructureException("Directory that keeps dbfs from exchange is not exists");

            if (!Directory.Exists(_config.DbfLookUpDir))
                throw new InfrastructureException("Directory that handle dbfs is not exists");

            if (exchangeDir.GetFiles("*.zip").Length == 0
                && exchangeDir.GetFiles("*.dbf").Length == 0)
                throw new InfrastructureException("Exchange directory is empty");
        }
    }
}
