using System;

namespace DbfProcessor.Core.Exceptions
{
    public class ExtractionException : Exception
    {
        public ExtractionException(string message) : base(message) { }
    }      
}
