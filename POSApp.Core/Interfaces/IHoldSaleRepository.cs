using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IHoldSaleRepository
    {
        Task<IEnumerable<HoldSale>> GetAllAsync(CancellationToken ct = default);
        Task<HoldSale?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<HoldSale> AddAsync(HoldSale holdSale, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
