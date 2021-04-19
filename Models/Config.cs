using DbfProcessor.Core.Exceptions;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.IO;
using System.Text.Json;

namespace DbfProcessor.Models
{
    public class Config
    {
        private ConfigModel _configModel;

        public Config(Logging log)
        {
            try
            {
                Deserialize();
            }
            catch (InfrastructureException e)
            {
                log.Accept(new Execution(e.Message));
                throw;
            }
            catch (JsonException e)
            {
                log.Accept(new Execution(e.Message));
                throw;
            }
        }

        public ConfigModel Get() => _configModel;

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\config.json";
            if (!File.Exists(path)) 
                throw new InfrastructureException("Can't find config.json file");
            _configModel = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(path));
        }
    }
}
