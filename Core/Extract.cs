using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;

namespace DbfProcessor.Core
{
    public class Extract
    {
        #region private fields
        private readonly ICollection<SharedParent> _parents;
        private Extraction _extractionModel;
        #endregion
        #region private properties
        private static Logging Log => Logging.GetLogging();
        private static Config Config => ConfigInstance.GetInstance().Config();
        private static Impersonation Impersonation => Impersonation.GetInstance();
        #endregion
        public Extract() => _parents = new List<SharedParent>();

        public void Process(ICollection<Extraction> extractionModels)
        {
            if (extractionModels.Count == 0)
                throw new ExchangeException("Empty extraction models, problem with exchange");
            foreach (Extraction model in extractionModels)
            {
                try
                {
                    _extractionModel = model;
                    FillShareds();
                }
                catch (ExtractionException e)
                {
                    Log.Accept(new Execution($"{e.Message}"));
                    throw;
                }
            }
        }

        public ICollection<SharedParent> GetParents()
        {
            if (_parents.Count == 0)
            {
                Log.Accept(new Execution("There is nothing to sync", LoggingType.Info));
                return new List<SharedParent>();
            }
            return _parents;
        }

        public void ClearParents() => _parents.Clear();

        #region private
        private void FillShareds()
        {
            TableInfo tableInfo = Impersonation.GetImpersonateTable(_extractionModel.TableType);
            DataTable dbfData = ReceiveData();
            if (tableInfo.CustomColumns.Count > 0)
            {
                AddCustomColumns(dbfData);
                RetrieveShopNum(dbfData);
            }
            if (!_parents.Any(t => t.TableType == _extractionModel.TableType))
            {
                _parents.Add(new SharedParent
                {
                    TableType = _extractionModel.TableType,
                    SharedChilds = new List<SharedChild>()
                });
            }
            SharedParent parent = _parents.Where(t => t.TableType.Equals(_extractionModel.TableType))
                .FirstOrDefault();
            if (parent is null) 
                throw new ExtractionException($"Can't find parent table with type, {_extractionModel.FullDescription}");
            SharedChild child = new SharedChild
            {
                FileName = _extractionModel.DbfName,
                PackageName = _extractionModel.Package,
                Rows = dbfData.Select()
            };
            parent.SharedChilds.Add(child);
        }

        private DataTable ReceiveData()
        {
            using OdbcConnection connection = new OdbcConnection(Config.DbfOdbcConn);
            connection.Open();
            OdbcCommand command = connection.CreateCommand();
            DataTable dataTable = new DataTable();
            TableInfo imperTable = Impersonation.GetImpersonateTable(_extractionModel.TableType);
            string toSelect = string.Join(",", imperTable.SqlColumnTypes.Keys
                .Except(imperTable.CustomColumns).ToList());

            command.CommandText = $"SELECT {toSelect} FROM {_extractionModel.TableName}";
            dataTable.Load(command.ExecuteReader());
            return dataTable;
        }

        private void AddCustomColumns(DataTable dataTable)
        {
            TableInfo tableInfo = Impersonation.GetImpersonateTable(_extractionModel.TableType);
            foreach (string customCol in tableInfo.CustomColumns)
                dataTable.Columns.Add(customCol, typeof(int));
        }

        private void RetrieveShopNum(DataTable dataTable)
        {
            if (!int.TryParse(_extractionModel.TableName.Substring(0, _extractionModel.TableName.IndexOf("_")), out int shopNum))
                throw new ExtractionException($"Can't parse shop number: {_extractionModel.FullDescription}");
            foreach (DataRow row in dataTable.Rows)
                row[dataTable.Columns.Count - 1] = shopNum;
        }
        #endregion
    }
}
