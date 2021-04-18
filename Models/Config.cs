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
        private ConfigModel _config;

        public Config(Logging logging)
        {
            try
            {
                Deserialize();
            } 
            catch (JsonException e)
            {
                logging.Accept(new Execution(e.Message));
                throw;
            }
        }

        public ConfigModel Get() => _config;

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\config.json";
            if (!File.Exists(path)) 
                throw new InfrastructureException("Can't find config.json file");
            _config = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(path));
        }
    }
}
