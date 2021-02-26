using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DbfProcessor.Models
{
    public class ImpersonationDict
    {
        public Dictionary<string, TableInfo> Impersonations { get; set; }
    }

    public class Impersonation
    {
        private ImpersonationDict _impersonationDict;
        private static Impersonation _impersonation;
        private static Logging Log => Logging.GetLogging();

        private Impersonation()
        {
            try
            {
                Deserialize();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\impersonations.json";
            if (!File.Exists(path)) 
                throw new Exception("Can't find impersonations.json file");

             _impersonationDict = JsonSerializer.Deserialize<ImpersonationDict>(File.ReadAllText(path));
        }

        public static Impersonation GetInstance()
        {
            if (_impersonation is null) _impersonation = new Impersonation();
            return _impersonation;
        }

        public TableInfo GetImpersonateTable(string tableType)
        {
            if (!_impersonationDict.Impersonations.TryGetValue(tableType, out TableInfo table))
                throw new Exception($"Can't get table info by type: [{tableType}]");
            return table;
        }
    }
}
