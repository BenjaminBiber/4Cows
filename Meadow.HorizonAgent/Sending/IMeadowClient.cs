using Meadow.Shared.Models.Horizon;

namespace Meadow.HorizonAgent.Sending;

public interface IMeadowClient
{
    /// <summary>
    /// Wirft <see cref="MeadowRejectedException"/> bei 401/403/404/409/422 (Server erreichbar, Anfrage
    /// abgewiesen), <see cref="HttpRequestException"/> bzw. <see cref="TaskCanceledException"/>, wenn der
    /// Server nicht erreichbar ist.
    /// </summary>
    Task<AgentHeartbeatResponse> SendHeartbeatAsync(AgentHeartbeat heartbeat, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendHeartbeatAsync"/>
    Task<HorizonSyncResult> SendSnapshotAsync(HorizonSnapshot snapshot, CancellationToken cancellationToken);
}
