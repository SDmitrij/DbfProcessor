using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Linq;

namespace DbfProcessor.Core
{
    public class Extract
    {
        private readonly ICollection<SharedParent> _parents;
        private readonly Logging _log;
        private readonly ConfigModel _configModel;
        private readonly Impersonation _impersonation;
        

        public Extract(Logging log, Config config, Impersonation impersonation)
        {
            _log = log;
            _configModel = config.Get();
            _impersonation = impersonation;
            _parents = new List<SharedParent>();
        }

        public void Process(ICollection<ExtractionModel> extractionModels)
        {
            if (extractionModels.Count == 0) return;
            foreach (ExtractionModel extractionModel in extractionModels)
            {
                TableInfo info = _impersonation.Get(extractionModel.TableType);
                try
                {
                    FillShareds(extractionModel, info);
                }
                catch (ExtractionException e)
                {
                    _log.Accept(new Execution($"{e.Message}"));
                    throw;
                }
            }
        }

        public ICollection<SharedParent> GetParents()
        {
            if (_parents.Count == 0)
            {
                _log.Accept(new Execution("There is nothing to sync", LoggingType.Info));
                return new List<SharedParent>();
            }
            return _parents;
        }

        public void ClearParents() => _parents.Clear();

        #region private
        private void FillShareds(ExtractionModel model, TableInfo info)
        {
            if (!File.Exists(Path.Combine(_configModel.DbfLookUpDir, model.DbfName)))
                throw new ExtractionException($"Can't find dbf file to receive data on: {model.FullDescription}");

            DataTable data = ReceiveData(model, info);

            if (info.CustomColumns.Count > 0) Custom(model, data, info);

            if (!_parents.Any(t => t.TableType.Equals(model.TableType)))
            {
                _parents.Add(new SharedParent
                {
                    TableType = model.TableType,
                    SharedChilds = new List<SharedChild>()
                });
            }
            SharedParent parent = _parents
                .Where(t => t.TableType.Equals(model.TableType))
                .FirstOrDefault();
            if (parent is null) 
                throw new ExtractionException($"Can't find parent table with type, {model.FullDescription}");

            SharedChild child = new SharedChild
            {
                FileName = model.DbfName,
                PackageName = model.Package,
                Rows = data.Select()
            };
            parent.SharedChilds.Add(child);
        }

        private DataTable ReceiveData(ExtractionModel model, TableInfo info)
        {
            using OdbcConnection connection = new OdbcConnection(_configModel.DbfOdbcConn);
            connection.Open();
            OdbcCommand command = connection.CreateCommand();
            DataTable dataTable = new DataTable();
           
            string toSelect = string.Join(",", info.SqlColumnTypes.Keys
                .Except(info.CustomColumns).ToList());

            command.CommandText = $"SELECT {toSelect} FROM {model.TableName}";
            dataTable.Load(command.ExecuteReader());
            return dataTable;
        }

        private void Custom(ExtractionModel model, DataTable data, TableInfo info)
        {
            foreach (string customCol in info.CustomColumns)
                data.Columns.Add(customCol, typeof(int));
            if (!int.TryParse(model.TableName.Substring(0, model.TableName.IndexOf("_")), out int shopNum))
                throw new ExtractionException($"Can't parse shop number: {model.FullDescription}");
            foreach (DataRow row in data.Rows)
                row[data.Columns.Count - 1] = shopNum;
        }
        #endregion
    }
}
