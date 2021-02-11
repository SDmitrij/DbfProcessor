using DbfProcessor.Core.Storage;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DbfProcessor.Core
{
    public class Extract
    {
        #region private fields
        private string _currPack;
        private readonly ICollection<SharedParent> _tables = new List<SharedParent>();
        #endregion
        #region private properties
        private Logging Log => Logging.GetLogging();
        private Config Config => ConfigInstance.GetInstance().Config();
        private Impersonation Impersonation => Impersonation.GetInstance();
        private Interaction Interaction => new Interaction();
        #endregion

        public ICollection<SharedParent> GetTables() => _tables;

        public void ProcessDbf(string path)
        {
            try
            {
                _currPack = Path.GetDirectoryName(path);
                InitializeTables();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
            }
        }

        #region private
        private void FillSharedTable(string tableName, string dbfName)
        {
            if (Interaction.GetSyncInfo(dbfName)) return;
            string tableType = RetrieveTypeFromName(tableName);
            TableInfo tableInfo = Impersonation.GetImpersonateTable(tableType);
            DataTable dbfData = ReceiveData(tableName, tableType);

            if (tableInfo.CustomColumns.Count > 0)
            {
                AddCustomColumnsToDataTable(dbfData, tableInfo);
                RetrieveAndFillShopNum(dbfData, tableName);
            }
            if (!_tables.Any(t => t.TableType == tableType))
            {
                _tables.Add(new SharedParent
                {
                    TableType = tableType,
                    SharedChilds = new List<SharedChild>()
                });
            }
            SharedParent parent = _tables.Where(t => t.TableType == tableType)
                .FirstOrDefault();
            if (parent is null) 
                throw new Exception($"Can't find table with type: [{tableType}]");
            SharedChild child = new SharedChild
            {
                FileName = dbfName,
                PackageName = _currPack,
                Rows = dbfData.Select()
            };
            parent.SharedChilds.Add(child);
        }

        private void InitializeTables()
        {
            using OdbcConnection connection = new OdbcConnection(Config.DbfOdbcConn);
            connection.Open();
            foreach (FileInfo table in CollectDbfs(connection.Database))
            {
                string currentTableName = table.Name.Replace(".dbf", string.Empty);
                if (currentTableName.Length > 8)
                {
                    string trunTableName = currentTableName.Substring(0, currentTableName.LastIndexOf("_"));
                    string newFilePath = Path.Combine(connection.Database, $"{trunTableName}.dbf");
                    File.Move(table.FullName, newFilePath);
                    FillSharedTable(trunTableName, table.Name);
                    File.Move(newFilePath, Path.Combine(connection.Database, $"{currentTableName}.dbf"));
                }
                else
                {
                    FillSharedTable(currentTableName, table.Name);
                }
            }
        }

        private DataTable ReceiveData(string dbfTable, string type)
        {
            using OdbcConnection connection = new OdbcConnection(Config.DbfOdbcConn);
            connection.Open();
            OdbcCommand cmd = connection.CreateCommand();
            DataTable dataTable = new DataTable();
            string toSelect = string.Join(",", Impersonation.GetImpersonateTable(type)
                .SqlColumnTypes.Keys.Except(Impersonation.GetImpersonateTable(type).CustomColumns).ToList());

            cmd.CommandText = $"SELECT {toSelect} FROM {dbfTable}";
            dataTable.Load(cmd.ExecuteReader());
            return dataTable;
        }
        #endregion
        #region static
        private static FileInfo[] CollectDbfs(string databaseLoc)
        {
            DirectoryInfo dbfDir = new DirectoryInfo(databaseLoc);
            FileInfo[] dbfDirFiles = dbfDir.GetFiles("*.dbf");

            if (dbfDirFiles.Length == 0)
                throw new Exception($"There are no any table in DBF directory: " +
                    $"[{databaseLoc}], check user's DSN config");

            return dbfDirFiles;
        }

        private static string RetrieveTypeFromName(string table)
        {
            if (Regex.IsMatch(table, @"^\d*_"))
            {
                int typeLen = table.Length - table.IndexOf("_");
                return table.Substring(table.IndexOf("_") + 1, typeLen - 1);
            }
            if (Regex.IsMatch(table, @"^\S*_")) return table.Substring(0, table.IndexOf("_"));
            throw new Exception($"Empty table type on [{table}]");
        }

        private static void AddCustomColumnsToDataTable(DataTable dataTable, TableInfo tableInfo)
        {
            foreach (string customCol in tableInfo.CustomColumns) 
                dataTable.Columns.Add(customCol, typeof(int));
        }

        private static void RetrieveAndFillShopNum(DataTable dataTable, string table)
        {
            if (!int.TryParse(table.Substring(0, table.IndexOf("_")), out int shopNum))
                throw new Exception($"Can't parse shop number from dbf name: {table}");
            foreach (DataRow row in dataTable.Rows)
                row[dataTable.Columns.Count - 1] = shopNum;
        }
        #endregion
    }
}
