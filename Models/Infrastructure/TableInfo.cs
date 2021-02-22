using System.Collections.Generic;

namespace DbfProcessor.Models.Infrastructure
{
    public class TableInfo
    {
        public string TableName { get; set; }
        public bool Ignore { get; set; }
        public ICollection<string> UniqueColumns { get; set; }
        public ICollection<string> CustomColumns { get; set; }
        public IDictionary<string, string> SqlColumnTypes { get; set; }
    }
}
