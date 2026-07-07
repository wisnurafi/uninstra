namespace Uninstra.Core.Models;

using Uninstra.Core.Enums;

public sealed record ElevatedRequest
{
    public required string RequestId { get; init; }
    public required string SessionId { get; init; }
    public required string Nonce { get; init; }
    public ElevatedOperationType OperationType { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed record ElevatedResponse
{
    public required string RequestId { get; init; }
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TechnicalDetails { get; init; } = string.Empty;
    public string ResultPayload { get; init; } = string.Empty;
}
