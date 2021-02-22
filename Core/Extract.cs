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
        private string _currPackage;
        private readonly Interaction _interaction;
        private readonly ICollection<SharedParent> _parents;
        #endregion
        #region private properties
        private Logging Log => Logging.GetLogging();
        private Config Config => ConfigInstance.GetInstance().Config();
        private Impersonation Impersonation => Impersonation.GetInstance();
        private Interaction Interaction => _interaction;
        private ICollection<SharedParent> Parents => _parents;
        #endregion

        public Extract()
        {
            _interaction = new Interaction();
            _parents = new List<SharedParent>();
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

        public void ProcessPack(string package)
        {
            _currPackage = package;
            try
            {
                InitializeTables();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
            }
        }

        #region private
        private void FillShareds(string table, string dbf)
        {
            string tableType = RetrieveTypeFromName(table);
            TableInfo tableInfo = Impersonation.GetImpersonateTable(tableType);
            if (!NeedSync(dbf, tableInfo))
            {
                Log.Accept(new Execution($"No need to sync {dbf}, it has already synced or ignored", 
                    LoggingType.Info));
                return;
            }
            DataTable dbfData = ReceiveData(table, tableType);
            if (tableInfo.CustomColumns.Count > 0)
            {
                AddCustomColumns(dbfData, tableInfo);
                RetrieveShopNum(dbfData, table);
            }
            if (!Parents.Any(t => t.TableType == tableType))
            {
                Parents.Add(new SharedParent
                {
                    TableType = tableType,
                    SharedChilds = new List<SharedChild>()
                });
            }
            SharedParent parent = Parents.Where(t => t.TableType.Equals(tableType))
                .FirstOrDefault();
            if (parent is null) 
                throw new Exception($"Can't find table with type: [{tableType}]");
            SharedChild child = new SharedChild
            {
                FileName = dbf,
                PackageName = _currPackage,
                Rows = dbfData.Select()
            };
            parent.SharedChilds.Add(child);
        }

        private bool NeedSync(string dbf, TableInfo info)
        {
            if (info.Ignore) return false;
            if (Interaction.GetSyncInfo(dbf))
                return false;
             else
                return true;
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
                    FillShareds(trunTableName, table.Name);
                    File.Move(newFilePath, Path.Combine(connection.Database, $"{currentTableName}.dbf"));
                }
                else
                {
                    FillShareds(currentTableName, table.Name);
                }
            }
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

        private static void AddCustomColumns(DataTable dataTable, TableInfo tableInfo)
        {
            foreach (string customCol in tableInfo.CustomColumns) 
                dataTable.Columns.Add(customCol, typeof(int));
        }

        private static void RetrieveShopNum(DataTable dataTable, string table)
        {
            if (!int.TryParse(table.Substring(0, table.IndexOf("_")), out int shopNum))
                throw new Exception($"Can't parse shop number from dbf name: {table}");
            foreach (DataRow row in dataTable.Rows)
                row[dataTable.Columns.Count - 1] = shopNum;
        }
        #endregion
    }
}
