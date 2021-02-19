using System;

namespace DbfProcessor.Out.Concrete
{
    public class Execution : IResult
    {
        #region private
        private readonly string _result;
        private readonly LoggingType _type;
        #endregion
        public Execution(string result, LoggingType type = LoggingType.Error)
        {
            _result = result;
            _type = type;
        }

        public string GetFile()
        {
            if (_type == LoggingType.Info) 
                return "info.txt";
            if (_type == LoggingType.Error) 
                return "error.txt";
            return string.Empty;
        }
        public string GetResult() => $"[{DateTime.Now}]: {_result}\n";
        public LoggingType Type() => _type;
    }
}
