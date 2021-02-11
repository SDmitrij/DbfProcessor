using System.Data;

namespace DbfProcessor.Models.Infrastructure
{
    public class SharedChild
    {
        public string PackageName { get; set; }
        public string FileName { get; set; }
        public DataRow[] Rows { get; set; }
    }
}
