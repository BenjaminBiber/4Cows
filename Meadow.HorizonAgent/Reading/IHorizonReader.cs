using Meadow.Shared.Models.Horizon;

namespace Meadow.HorizonAgent.Reading;

public interface IHorizonReader
{
    Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken);
}
