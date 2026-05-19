namespace TigerTradeMcp.TigerLink;

public sealed class TigerLinkException : Exception
{
    public TigerLinkException(string message) : base(message) { }
    public TigerLinkException(string message, Exception inner) : base(message, inner) { }
}
