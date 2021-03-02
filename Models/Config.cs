using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.IO;
using System.Text.Json;

namespace DbfProcessor.Models
{
    public class Config
    {
        public string ExchangeDirectory { get; set; }
        public string DbfLookUpDir { get; set; }
        public string DbfOdbcConn { get; set; }
        public string SqlServerConn { get; set; }
        public int BatchSize { get; set; }
    }

    public class ConfigInstance
    {
        private Config _config;
        private static ConfigInstance _instance;
        public Config Config => _config;
        private static Logging Log => Logging.GetLogging();

        private ConfigInstance()
        {
            try
            {
                Deserialize();
            } 
            catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\config.json";
            if (!File.Exists(path)) 
                throw new Exception("Can't find config.json file");
            _config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path));
        }

        public static ConfigInstance GetInstance()
        {
            if (_instance is null) _instance = new ConfigInstance();
            return _instance;
        }
    }
}
