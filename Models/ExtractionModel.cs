namespace DbfProcessor.Models
{
    public class ExtractionModel
    {
        public string DbfName { get; set; }
        public string TableName { get; set; }
        public string Package { get; set; }
        public string TableType { get; set; }
        public string FullDescription
            => $"Dbf: {DbfName}, package: {Package}, type: {TableType}";
    }
}
