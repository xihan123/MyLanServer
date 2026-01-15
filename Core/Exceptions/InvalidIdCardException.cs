namespace MyLanServer.Core.Exceptions;

/// <summary>
///     身份证号验证失败异常
/// </summary>
public class InvalidIdCardException : DomainException
{
    public InvalidIdCardException(string message) : base("INVALID_ID_CARD", message)
    {
    }

    public InvalidIdCardException(string message, Exception innerException)
        : base("INVALID_ID_CARD", message, innerException)
    {
    }
}