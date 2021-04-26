using DbfProcessor.Models.Infrastructure;
using System.Collections.Generic;

namespace DbfProcessor.Models
{
    public class ImpersonationModel
    {
        public Dictionary<string, TableInfo> TypeInfo { get; set; }
    }
}
