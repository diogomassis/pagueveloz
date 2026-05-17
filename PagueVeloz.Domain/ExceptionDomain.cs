namespace PagueVeloz.Domain;

public sealed class ExceptionDomain : Exception
{
    public ExceptionDomain(string message)
        : base(message)
    {
    }
}
