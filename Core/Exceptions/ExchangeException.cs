using System;

namespace DbfProcessor.Core.Exceptions
{
    public class ExchangeException : Exception
    {
        public ExchangeException(string message) : base(message) { }
    }
}
