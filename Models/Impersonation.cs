using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DbfProcessor.Models
{
    public class Impersonation
    {
        class Impersonations
        {
            public IDictionary<string, TableInfo> TypeInfo { get; set; }
        }

        private readonly Logging _log;
        private Impersonations _impersonations;

        public Impersonation(Logging log)
        {
            _log = log;
            FillDictionary();
        }

        public TableInfo Get(string tableType)
        {
            try
            {
                return FindInDictionary(tableType);
            }
            catch (ImpersonationException e)
            {
                _log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void FillDictionary()
        {
            try
            {
                Deserialize();
            }
            catch (InfrastructureException e)
            {
                _log.Accept(new Execution(e.Message));
                throw;
            }
            catch (JsonException e)
            {
                _log.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\impersonations.json";
            if (!File.Exists(path))
                throw new InfrastructureException("Can't find impersonations.json file");

            _impersonations = JsonSerializer.Deserialize<Impersonations>(File.ReadAllText(path));
        }

        private TableInfo FindInDictionary(string tableType)
        {
            if (!_impersonations.TypeInfo.TryGetValue(tableType, out TableInfo table))
                throw new ImpersonationException($"Can't get table info by type: [{tableType}]");
            return table;
        }
    }
}
