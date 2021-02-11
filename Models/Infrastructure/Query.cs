namespace DbfProcessor.Models.Infrastructure
{
    public enum QueryType : byte
    {
        Create,
        Index
    }
    public class Query
    {
        public QueryType QueryType { get; set; }
        public string QueryBody { get; set; }
    }
}
