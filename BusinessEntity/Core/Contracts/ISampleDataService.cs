using System.Threading;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Contracts
{
    public interface ISampleDataService
    {
        Task InitializeSampleDataAsync(CancellationToken ct = default);
    }
} 