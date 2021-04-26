using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.IO;
using System.Text.Json;

namespace DbfProcessor
{
    public class Impersonation
    {
        private readonly Logging _log;
        private ImpersonationModel _impersonationModel;

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
            string path = $"{AppDomain.CurrentDomain.BaseDirectory}impersonations.json";
            if (!File.Exists(path))
                throw new InfrastructureException("Can't find impersonations.json file");
            _impersonationModel = JsonSerializer.Deserialize<ImpersonationModel>(File.ReadAllText(path));
        }

        private TableInfo FindInDictionary(string tableType)
        {
            if (!_impersonationModel.TypeInfo.TryGetValue(tableType, out TableInfo table))
                throw new ImpersonationException($"Can't get table info by type: [{tableType}]");
            return table;
        }
    }
}
