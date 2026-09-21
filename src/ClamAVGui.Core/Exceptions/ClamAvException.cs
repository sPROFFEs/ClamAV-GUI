namespace ClamAVGui.Core.Exceptions;

public enum ClamAvErrorCode
{
    ClamAvNotFound,
    ClamAvExecutionFailed,
    DatabaseMissing,
    DatabaseMismatch,
    DatabaseUpdateFailed,
    DaemonUnavailable,
    DaemonProtocolError,
    DaemonOwnershipViolation,
    PermissionDenied,
    InvalidConfiguration,
    ConfigurationReadOnly,
    ScanCancelled,
    UnsupportedFeature,
    QuarantineFailed
}

public class ClamAvException : Exception
{
    public ClamAvErrorCode ErrorCode { get; }

    public ClamAvException(ClamAvErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ClamAvException(ClamAvErrorCode errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
