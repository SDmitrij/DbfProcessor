namespace DbfProcessor.Models
{
    public class ConfigModel
    {
        public string ExchangeDirectory { get; set; }
        public string DbfLookUpDir { get; set; }
        public string DbfOdbcConn { get; set; }
        public string SqlServerConn { get; set; }
        public int BatchSize { get; set; }
    }
}
