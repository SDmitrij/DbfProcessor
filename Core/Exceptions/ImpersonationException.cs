using System;

namespace DbfProcessor.Core.Exceptions
{
    public class ImpersonationException : Exception
    {
        public ImpersonationException(string message) : base(message) { }
    }
}
