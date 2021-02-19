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
        private SharedParent _parent;
        private TableInfo _tableInfo;
        private ICollection<SharedParent> _parents;
        private readonly QueryBuild _queryBuild;
        #endregion
        #region private properties
        private Impersonation Impersonation => Impersonation.GetInstance();
        private Config Config => ConfigInstance.GetInstance().Config();
        private Logging Log => Logging.GetLogging();
        private QueryBuild QueryBuild => _queryBuild;
        #endregion
      
        public Interaction() => _queryBuild = new QueryBuild();
       
        public void Take(ICollection<SharedParent> parents)
        {
            if (parents.Count == 0) return;
            try
            {
                _parents = parents;
                Loop();
            }  catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
            }
        }

        public void ApplyBase()
        {
            try
            {
                BaseSeed();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
            }
        }

        public bool GetSyncInfo(string dbfName)
        {
            string query = $"SELECT [dbo].[fn_NotBulkedDbfs]('{dbfName}')";
            using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
            connection.Open();
            SqlCommand command = new SqlCommand(query, connection);
            SqlDataReader reader = command.ExecuteReader();
           
            if (!reader.HasRows)
            {
                Log.Accept(new Execution("Sync table is empty...", LoggingType.Info));
                return false;
            }
            reader.Read();
            if (reader.IsDBNull(0)) return false;
            if (reader.GetInt32(0) == 0) return false;
            else return true;
        }

        public void Stage()
        {
            string sqlStageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Stage");
            if (!Directory.Exists(sqlStageDir))
                throw new Exception("Directory with stage sql does not exists!");
            DirectoryInfo sqlStageDirInf = new DirectoryInfo(sqlStageDir);
            FileInfo stageFile = sqlStageDirInf.GetFiles("*.sql")
                .Where(f => f.Name.Replace(".sql", string.Empty).Equals("Stage")).FirstOrDefault();
            if (stageFile is null)
                throw new Exception("Can't find stage sql file");
            string sqlStageQuery = File.ReadAllText(stageFile.FullName);
            if (sqlStageQuery.Equals(string.Empty))
                throw new Exception($"Stage sql file {stageFile.Name} does not have any content");
            ExecuteOnly(sqlStageQuery);
        }

        public void CreateProcedures()
        {
            string procsSqlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Procs");
            if (!Directory.Exists(procsSqlDir))
                throw new Exception("Directory with sql procedures does not exists!");
            DirectoryInfo procsSqlDirInf = new DirectoryInfo(procsSqlDir);
            FileInfo[] sqlProcedureFiles = procsSqlDirInf.GetFiles("*.sql");
            if (sqlProcedureFiles.Length == 0)
                throw new Exception("Sql procs dir does not conatains any sql file");

            foreach (FileInfo sqlFile in sqlProcedureFiles)
            {
                string sqlQuery = File.ReadAllText(sqlFile.FullName);
                if (sqlQuery.Equals(string.Empty))
                    throw new Exception($"Sql file's: {sqlFile.Name} content is empty");
                ExecuteOnly(sqlQuery);
            }
        }

        #region private
        private void Loop()
        {
            foreach (SharedParent parent in _parents)
            {
                _parent = parent;
                _tableInfo = Impersonation.GetImpersonateTable(_parent.TableType);

                QueryBuild.Build(_parent);
                TableSeed();

                if (_tableInfo.UniqueColumns.Count > 0) 
                    ApplyIndex();

                BulkCopy();
            }
        }

        private void BaseSeed()
        {
            string baseSqlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Base");
            if (!Directory.Exists(baseSqlDir))
                throw new Exception("Directory with base sql does not exists!");

            DirectoryInfo sqlDirInfo = new DirectoryInfo(baseSqlDir);
            FileInfo baseSqlFile = sqlDirInfo
                .GetFiles("*.sql")
                .Where(f => f.Name.Replace(".sql", string.Empty)
                .Equals("Seed"))
                .FirstOrDefault();

            if (baseSqlFile is null)
                throw new Exception("Can't find sql file with base migration");
            string baseSql = File.ReadAllText(baseSqlFile.FullName);

            if (baseSql.Equals(string.Empty))
                throw new Exception($"Base sql file {baseSqlFile.FullName} " +
                    "does not have any content");
            ExecuteOnly(baseSql);
        }

        private void BulkCopy()
        {
            static string execQuery(SharedChild child, bool bulked)
                => $"EXEC [dbo].[sp_InsertSyncInfo] '{child.PackageName.Trim()}', " +
                $"'{child.FileName.Trim()}', {(bulked ? 1 : 0)};";

            foreach (SharedChild child in _parent.SharedChilds)
            {
                try
                {
                    BulkExecute(child);
                    ExecuteOnly(execQuery(child, true));
                    Log.Accept(new Bulk($"Bulk successed for table in file: {child.FileName}"));
                } catch (Exception e)
                {
                    ExecuteOnly(execQuery(child, false));
                    Log.Accept(new Bulk($"Bulk failed for table in file: {child.FileName}", LoggingType.Error));
                    Log.Accept(new Execution(e.Message));
                }
            }
        }

        private void TableSeed()
        {
            foreach (Query query in _parent.SeedQueries.Where(q => q.QueryType == QueryType.Create))
                ExecuteOnly(query.QueryBody);
        }

        private void ApplyIndex()
        {
            Query indexQuery = _parent.SeedQueries
                .Where(q => q.QueryType == QueryType.Index).FirstOrDefault();
            if (indexQuery is null) 
                throw new Exception($"Can't get index query for table {_tableInfo.TableName}");
            ExecuteOnly(indexQuery.QueryBody);
        }

        private int ExecuteOnly(string sql)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection)
                {
                    CommandTimeout = 0
                };
                return command.ExecuteNonQuery();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
                Log.Accept(new Sql(new Query { QueryBody = sql }));
            }
            return 0;
        }

        private void BulkExecute(SharedChild child)
        {
            using SqlConnection connection = new SqlConnection(Config.SqlServerConn);
            connection.Open();
            SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = $"[stage].[{_tableInfo.TableName}]",
                BatchSize = Config.BatchSize
            };
            BuildColumnMappings(sqlBulkCopy);
            sqlBulkCopy.WriteToServer(child.Rows);
        }

        private void BuildColumnMappings(SqlBulkCopy sqlBulkCopy)
        {
            foreach (string column in _tableInfo.SqlColumnTypes.Keys)
                sqlBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(column, column));
        }
        #endregion
    }
}
