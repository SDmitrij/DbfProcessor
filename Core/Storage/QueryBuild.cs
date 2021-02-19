using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.Text;

namespace DbfProcessor.Core.Storage
{
    public class QueryBuild
    {
        #region private fields
        private SharedParent _parent;
        private TableInfo _tableInfo;
        #endregion
        #region private properties
        private Logging Log => Logging.GetLogging();
        private Impersonation Impersonation => Impersonation.GetInstance();
        #endregion
        public void Build(SharedParent parent)
        {
            try
            {
                _parent = parent;
                _parent.SeedQueries = new List<Query>();
                _tableInfo = Impersonation.GetImpersonateTable(_parent.TableType);

                Create();
                if (_tableInfo.UniqueColumns.Count > 0) 
                    Index();
            } catch (Exception e)
            {
                Log.Accept(new Execution($"Failed to build query for {_parent.TableType} problem: {e.Message}"));
            }
        }
        #region private
        private void Create()
        {
            string[] schemas = { "stage", "dbo" };
            foreach (string schema in schemas)
            {
                StringBuilder query =
                new StringBuilder(
                    $"IF NOT EXISTS(SELECT * FROM information_schema.tables WHERE table_schema = '{schema}' " +
                    $"AND table_name = '{_tableInfo.TableName}')\n");
                query.Append($"\tCREATE TABLE [{schema}].[{_tableInfo.TableName}] (\n");

                foreach (var col in _tableInfo.SqlColumnTypes)
                    query.Append($"\t[{col.Key}] {col.Value},\n");
                query.Append(")\n");

                _parent.SeedQueries.Add(new Query
                {
                    QueryBody = ReplaceLastOccurrence(query.ToString(), ",", string.Empty),
                    QueryType = QueryType.Create
                });
            }
        }

        private void Index()
        {
            StringBuilder indexQuery = new StringBuilder($"IF NOT EXISTS(SELECT * FROM sys.indexes " +
                $"WHERE name = '{_tableInfo.TableName}_clustered' AND object_id " +
                $"= OBJECT_ID('[dbo].[{_tableInfo.TableName}]'))\nBEGIN\n");

            indexQuery.Append($"\tEXEC('CREATE UNIQUE CLUSTERED INDEX [{_tableInfo.TableName}_clustered] " +
                    $"ON [dbo].[{_tableInfo.TableName}] (\n");

            foreach (var constraint in _tableInfo.UniqueColumns)
                indexQuery.Append($"\t[{constraint}] ASC,\n");

            _parent.SeedQueries.Add(new Query
            {
                QueryBody = ReplaceLastOccurrence(indexQuery.ToString(), ",", string.Empty) +
                    ")WITH (ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)')\nEND\n",
                QueryType = QueryType.Index
            });
        }

        private static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);
            if (place == -1) return source;
            string result = source.Remove(place, find.Length).Insert(place, replace);
            return result;
        }
        #endregion
    }
}
