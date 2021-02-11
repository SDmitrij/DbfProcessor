using System.Collections.Generic;

namespace DbfProcessor.Models.Infrastructure
{
    public class SharedParent
    {
        public string TableType { get; set; }
        public ICollection<Query> SeedQueries { get; set; }
        public ICollection<SharedChild> SharedChilds { get; set; }
    }
}
