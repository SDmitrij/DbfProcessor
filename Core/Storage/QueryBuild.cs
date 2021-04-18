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
        private readonly Logging _log;
        private readonly Impersonation _impersonation;
        #endregion

        public QueryBuild(Logging log, Impersonation impersonation)
        {
            _log = log;
            _impersonation = impersonation;
        }

        public void Build(SharedParent parent)
        {
            try
            {
                parent.SeedQueries = new List<Query>();
                TableInfo info = _impersonation.Get(parent.TableType);

                Create(info, parent);
                if (info.UniqueColumns.Count > 0) 
                    Index(info, parent);
            } catch (Exception e)
            {
                _log.Accept(new Execution($"Failed to build query for {parent.TableType} " +
                    $"problem: {e.Message}"));
                throw;
            }
        }
        #region private
        private void Create(TableInfo info, SharedParent parent)
        {
            string[] schemas = { "stage", "dbo" };
            foreach (string schema in schemas)
            {
                StringBuilder query =
                new StringBuilder(
                    $"IF NOT EXISTS(SELECT * FROM information_schema.tables WHERE table_schema = '{schema}' " +
                    $"AND table_name = '{info.TableName}')\n");
                query.Append($"\tCREATE TABLE [{schema}].[{info.TableName}] (\n");

                foreach (var col in info.SqlColumnTypes)
                    query.Append($"\t[{col.Key}] {col.Value},\n");
                query.Append(")\n");

                parent.SeedQueries.Add(new Query
                {
                    QueryBody = ReplaceLastOccurrence(query.ToString(), ",", string.Empty),
                    QueryType = QueryType.Create
                });
            }
        }

        private void Index(TableInfo info, SharedParent parent)
        {
            StringBuilder indexQuery = new StringBuilder($"IF NOT EXISTS(SELECT * FROM sys.indexes " +
                $"WHERE name = '{info.TableName}_clustered' AND object_id " +
                $"= OBJECT_ID('[dbo].[{info.TableName}]'))\nBEGIN\n");

            indexQuery.Append($"\tEXEC('CREATE UNIQUE CLUSTERED INDEX [{info.TableName}_clustered] " +
                    $"ON [dbo].[{info.TableName}] (\n");

            foreach (var constraint in info.UniqueColumns)
                indexQuery.Append($"\t[{constraint}] ASC,\n");

            parent.SeedQueries.Add(new Query
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
