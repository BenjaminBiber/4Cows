using System.Net;

namespace Meadow.HorizonAgent.Sending;

/// <summary>
/// Der Server war erreichbar, hat die Anfrage aber abgewiesen (401, 403, 404, 409, 422).
/// Ein erneuter Versuch mit denselben Daten hilft nicht - deshalb kein Backoff.
/// </summary>
public sealed class MeadowRejectedException : Exception
{
    public MeadowRejectedException(HttpStatusCode statusCode, string? detail)
        : base(string.IsNullOrWhiteSpace(detail)
            ? $"HTTP {(int)statusCode}"
            : $"HTTP {(int)statusCode}: {detail}")
    {
        StatusCode = statusCode;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }

    public string? Detail { get; }
}
