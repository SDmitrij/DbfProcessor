using DbfProcessor.Models.Infrastructure;
using System;

namespace DbfProcessor.Out.Concrete
{
    public class Sql : IResult
    {
        private readonly string _sql;
        private readonly LoggingType _type;
        public Sql(Query query, LoggingType type = LoggingType.Error)
        {
            _sql = query.QueryBody;
            _type = type;
        }
        public string GetFile()
        {
            if (_type == LoggingType.Error) return "error.sql";
            if (_type == LoggingType.Info) return "info.sql";
            return string.Empty;
        }

        public string GetResult() => $"/*[{DateTime.Now}]:*/\n {_sql}\n";

        public LoggingType Type() => _type;
    }
}
