using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Models.Dtos;
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
        #endregion
        #region private properties
        private static Logging Log => Logging.GetLogging();
        private static Config Config => ConfigInstance.GetInstance().Config();
        private static Impersonation Impersonation => Impersonation.GetInstance();
        private ICollection<SharedParent> Parents => _parents;
        #endregion
        public Extract() => _parents = new List<SharedParent>();

        public void Process(ICollection<ExtractionDto> dtos)
        {
            foreach (ExtractionDto dto in dtos)
            {
                try
                {
                    FillShareds(dto);
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
            if (Parents.Count == 0)
            {
                Log.Accept(new Execution("There is nothing to sync", LoggingType.Info));
                return new List<SharedParent>();
            }
            return Parents;
        }

        public void Clear() => Parents.Clear();

        #region private
        private void FillShareds(ExtractionDto dto)
        {
            TableInfo tableInfo = Impersonation.GetImpersonateTable(dto.TableType);
            DataTable dbfData = ReceiveData(dto.TableName, dto.TableType);
            if (tableInfo.CustomColumns.Count > 0)
            {
                AddCustomColumns(dbfData, tableInfo);
                RetrieveShopNum(dbfData, dto.TableName);
            }
            if (!Parents.Any(t => t.TableType == dto.TableType))
            {
                Parents.Add(new SharedParent
                {
                    TableType = dto.TableType,
                    SharedChilds = new List<SharedChild>()
                });
            }
            SharedParent parent = Parents.Where(t => t.TableType.Equals(dto.TableType))
                .FirstOrDefault();
            if (parent is null) 
                throw new ExtractionException($"Can't find parent table with type, {dto.FullDescription}");
            SharedChild child = new SharedChild
            {
                FileName = dto.DbfName,
                PackageName = dto.Package,
                Rows = dbfData.Select()
            };
            parent.SharedChilds.Add(child);
        }

        private DataTable ReceiveData(string dbfTable, string type)
        {
            using OdbcConnection connection = new OdbcConnection(Config.DbfOdbcConn);
            connection.Open();
            OdbcCommand command = connection.CreateCommand();
            DataTable dataTable = new DataTable();
            TableInfo imperTable = Impersonation.GetImpersonateTable(type);
            string toSelect = string.Join(",", imperTable.SqlColumnTypes.Keys
                .Except(imperTable.CustomColumns).ToList());

            command.CommandText = $"SELECT {toSelect} FROM {dbfTable}";
            dataTable.Load(command.ExecuteReader());
            return dataTable;
        }
        #endregion
        #region static
        private static void AddCustomColumns(DataTable dataTable, TableInfo tableInfo)
        {
            foreach (string customCol in tableInfo.CustomColumns) 
                dataTable.Columns.Add(customCol, typeof(int));
        }

        private static void RetrieveShopNum(DataTable dataTable, string table)
        {
            if (!int.TryParse(table.Substring(0, table.IndexOf("_")), out int shopNum))
                throw new ExtractionException($"Can't parse shop number from dbf name: {table}");
            foreach (DataRow row in dataTable.Rows)
                row[dataTable.Columns.Count - 1] = shopNum;
        }
        #endregion
    }
}
