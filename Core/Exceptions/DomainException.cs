namespace MyLanServer.Core.Exceptions;

/// <summary>
///     领域异常基类
///     用于表示业务逻辑错误
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string errorCode, string message, int statusCode = 400) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    protected DomainException(string errorCode, string message, Exception innerException, int statusCode = 400)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    ///     错误代码
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    ///     HTTP 状态码
    /// </summary>
    public int StatusCode { get; protected set; }
}