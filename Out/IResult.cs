namespace DbfProcessor.Out
{
    public interface IResult
    {
        public string GetResult();
        public string GetFile();
        public LoggingType Type();
    }
}
