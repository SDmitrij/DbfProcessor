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

        private readonly Logging _logging;
        private Impersonations _typeInfo;

        public Impersonation(Logging logging)
        {
            _logging = logging;
            try
            {
                Deserialize();
            }
            catch (JsonException e)
            {
                _logging.Accept(new Execution(e.Message));
                throw;
            }
        }

        public TableInfo Get(string tableType)
        {
            try
            {
                return FindInDictionary(tableType);
            }
            catch (ImpersonationException e)
            {
                _logging.Accept(new Execution(e.Message));
                throw;
            }
        }

        private void Deserialize()
        {
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}\\impersonations.json";
            if (!File.Exists(path))
                throw new InfrastructureException("Can't find impersonations.json file");

            _typeInfo = JsonSerializer.Deserialize<Impersonations>(File.ReadAllText(path));
        }

        private TableInfo FindInDictionary(string tableType)
        {
            if (!_typeInfo.TypeInfo.TryGetValue(tableType, out TableInfo table))
                throw new ImpersonationException($"Can't get table info by type: [{tableType}]");
            return table;
        }
    }
}
