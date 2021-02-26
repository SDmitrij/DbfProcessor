using System;

namespace DbfProcessor.Core.Exceptions
{
    public class InteractionException : Exception
    {
        public InteractionException(string message) : base(message) { }
    }
}
