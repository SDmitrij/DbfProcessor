using DbfProcessor.Core.Exceptions;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace DbfProcessor.Core.Storage
{
    public class Interaction
    {
        #region private fields
        private string _currentPackage;
        private SharedParent _parent;
        private TableInfo _tableInfo;
        private ICollection<SharedParent> _parents;
        private readonly QueryBuild _queryBuild;
        #endregion
        #region private properties
        private static Impersonation Impersonation => Impersonation.GetInstance();
        private static Config Config => ConfigInstance.GetInstance().Config;
        private static Logging Log => Logging.GetLogging();
        #endregion
        public Interaction() => _queryBuild = new QueryBuild();
       
        public void Process(ICollection<SharedParent> parents)
        {
            if (parents.Count == 0) return;
            _parents = parents;
            try
            {
                Loop();
            }  
            catch (InteractionException e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        public void ApplyBase()
        {
            try
            {
                BaseSeed();
            }
            catch (InteractionException e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        public void ApplyStage()
        {
            try
            {
                CreateProcedures();
                Stage();
            }
            catch (InteractionException e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }

        public bool GetSyncInfo(string dbfName, string package)
        {
            string query = "SELECT [dbo].[fn_CheckBulked](@dbf, @pack)";
            using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
            connection.Open();
            SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@dbf", dbfName);
            command.Parameters.AddWithValue("@pack", package);
            SqlDataReader reader = command.ExecuteReader();
           
            if (!reader.HasRows)
            {
                Log.Accept(new Execution("Sync table is empty...", LoggingType.Info));
                return false;
            }
            reader.Read();
            if (reader.GetInt32(0) == 0) return false;
            else return true;
        }
        #region private
        private void Stage()
        {
            string sqlStageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Stage");
            if (!Directory.Exists(sqlStageDir))
                throw new InteractionException("Directory with stage sql does not exists!");
            DirectoryInfo sqlStageDirInf = new DirectoryInfo(sqlStageDir);
            FileInfo stageFile = sqlStageDirInf.GetFiles("*.sql")
                .Where(f => f.Name.Replace(".sql", string.Empty).Equals("Stage")).FirstOrDefault();
            if (stageFile is null)
                throw new InteractionException("Can't find stage sql file");
            string sqlStageQuery = File.ReadAllText(stageFile.FullName);
            if (sqlStageQuery.Equals(string.Empty))
                throw new InteractionException($"Stage sql file {stageFile.Name} does not have any content");
            Log.Accept(new Execution("Staging...", LoggingType.Info));
            ExecuteNonParameterized(sqlStageQuery);
        }

        private void CreateProcedures()
        {
            string procsSqlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Procs");
            if (!Directory.Exists(procsSqlDir))
                throw new InteractionException("Directory with sql procedures does not exists!");
            DirectoryInfo procsSqlDirInf = new DirectoryInfo(procsSqlDir);
            FileInfo[] sqlProcedureFiles = procsSqlDirInf.GetFiles("*.sql");
            if (sqlProcedureFiles.Length == 0)
                throw new InteractionException("Sql procs dir does not conatains any sql file");

            foreach (FileInfo sqlFile in sqlProcedureFiles)
            {
                string sqlQuery = File.ReadAllText(sqlFile.FullName);
                if (sqlQuery.Equals(string.Empty))
                    throw new InteractionException($"Sql file's: {sqlFile.Name} content is empty");
                ExecuteNonParameterized(sqlQuery);
            }
        }

        private void Loop()
        {
            foreach (SharedParent parent in _parents)
            {
                _parent = parent;
                _tableInfo = Impersonation.Get(_parent.TableType);
                _queryBuild.Build(_parent);
                TableSeed();
                if (_tableInfo.UniqueColumns.Count > 0) ApplyIndex();
                BulkCopy();
            }
        }

        private void BaseSeed()
        {
            string baseSqlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Base");
            if (!Directory.Exists(baseSqlDir))
                throw new InteractionException("Directory with base sql does not exists!");

            DirectoryInfo sqlDirInfo = new DirectoryInfo(baseSqlDir);
            FileInfo baseSqlFile = sqlDirInfo
                .GetFiles("*.sql")
                .Where(f => f.Name.Replace(".sql", string.Empty)
                .Equals("Seed"))
                .FirstOrDefault();

            if (baseSqlFile is null)
                throw new InteractionException("Can't find sql file with base migration");
            string baseSql = File.ReadAllText(baseSqlFile.FullName);

            if (baseSql.Equals(string.Empty))
                throw new InteractionException($"Base sql file {baseSqlFile.FullName} " +
                    "does not have any content");
            ExecuteNonParameterized(baseSql);
        }

        private void BulkCopy()
        {
            string sql = "EXEC [dbo].[sp_InsertSyncInfo] @packName, @fileName, @bulked, @timeNow;";
            static IDictionary<string, object> initParams(string pack, string dbf, byte bulked)
            {
                return new Dictionary<string, object>
                {
                    { "@packName", pack },
                    { "@fileName", dbf },
                    { "@bulked", bulked },
                    { "@timeNow", DateTime.Now }
                };
            }

            foreach (SharedChild child in _parent.SharedChilds)
            {
                try
                {
                    _currentPackage = child.PackageName;
                    BulkExecute(child);
                    ExecuteParameterized(sql, initParams(child.PackageName, child.FileName, 1));
                } 
                catch (Exception e)
                {
                    ExecuteParameterized(sql, initParams(child.PackageName, child.FileName, 0));
                    Log.Accept(new Execution(e.Message));
                    throw;
                }
            }
        }

        private void TableSeed()
        {
            foreach (Query query in _parent.SeedQueries.Where(q => q.QueryType == QueryType.Create))
                ExecuteNonParameterized(query.QueryBody);
        }

        private void ApplyIndex()
        {
            Query indexQuery = _parent.SeedQueries
                .Where(q => q.QueryType == QueryType.Index).FirstOrDefault();
            if (indexQuery is null) 
                throw new InteractionException($"Can't get index query for table {_tableInfo.TableName}");
            ExecuteNonParameterized(indexQuery.QueryBody);
        }

        private void ExecuteNonParameterized(string sql)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection)
                {
                    CommandTimeout = 0
                };
                command.ExecuteNonQuery();
            } 
            catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
                Log.Accept(new Sql(new Query { QueryBody = sql }));
                throw;
            }
        }

        private void ExecuteParameterized(string sql, IDictionary<string, object> parameters)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection)
                {
                    CommandTimeout = 0
                };
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value);
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
                Log.Accept(new Sql(new Query { QueryBody = sql }));
                throw;
            }
        }

        private void BulkExecute(SharedChild child)
        {
            using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
            connection.Open();
            using SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = $"[stage].[{_tableInfo.TableName}]",
                BatchSize = Config.BatchSize
            };
            sqlBulkCopy.SqlRowsCopied += new SqlRowsCopiedEventHandler(OnSqlRowsCopied);
            sqlBulkCopy.NotifyAfter = 50;
            BuildColumnMappings(sqlBulkCopy);
            sqlBulkCopy.WriteToServer(child.Rows);
        }

        private void OnSqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
            =>
            Log.Accept(new Execution(string.Format("\tTable: [{0}], pack: [{1}] copied: {2}",
                _tableInfo.TableName, _currentPackage, e.RowsCopied), LoggingType.Info));

        private void BuildColumnMappings(SqlBulkCopy sqlBulkCopy)
        {
            foreach (string column in _tableInfo.SqlColumnTypes.Keys)
                sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(column, column));
        }
        #endregion
    }
}
