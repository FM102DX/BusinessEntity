using System.Threading;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Contracts
{
    /// <summary>
    /// Provides text lines from a data fill source for populating sample documents.
    /// Each call returns the next line; wraps around when reaching the end.
    /// </summary>
    public interface IDataFillLineProvider
    {
        Task<string> GetNextLineAsync(CancellationToken ct = default);
    }
}
