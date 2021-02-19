using System;
using System.IO;
using System.Linq;

namespace DbfProcessor.Core
{
    public class Partition
    {
        private readonly FileInfo[] _packages;
        private readonly int _total;
        private int _iteration = 1;
        public bool HasNext => _iteration <= _total;

        public Partition(FileInfo[] packages)
        {
            _packages = packages;
            _total = (int)Math.Ceiling(decimal.Divide(_packages.Length, 10));
        }

        public FileInfo[] Get()
        {
            var res = _packages.Skip((_iteration - 1) * 10).Take(10).ToArray();
            _iteration++;
            return res;
        }
    }
}
