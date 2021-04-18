using System;

namespace DbfProcessor.Core.Exceptions
{
    public class InfrastructureException : Exception
    {
        public InfrastructureException(string message) : base(message) { }
    }
}
