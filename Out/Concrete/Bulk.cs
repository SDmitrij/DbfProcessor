using System;

namespace DbfProcessor.Out.Concrete
{
    public class Bulk : IResult
    {
        private readonly string _result;
        private readonly LoggingType _type;
        public Bulk(string result, LoggingType type = LoggingType.Info)
        {
            _type = type;
            _result = result;
        }

        public string GetFile()
        {
            if (_type == LoggingType.Info) return "bulk_success.txt";
            if (_type == LoggingType.Error) return "bulk_failed.txt";
            return string.Empty;
        }

        public string GetResult() => $"[{DateTime.Now}]: {_result}\n";
        public LoggingType Type() => _type;
    }
}
