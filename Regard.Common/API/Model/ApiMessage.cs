namespace Regard.Common.API.Model
{
    /// <summary>
    /// Notice and above are stored in the DB
    /// </summary>
    enum ApiMessageSeverity
    {
        Info,
        Notice,
        Warning,
        Error
    }

    public class ApiMessage
    {
        public string Message { get; set; }

        ApiMessageSeverity Severity { get; set; }
    }
}
