using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public interface IDisputeRepository
{
    Task<Dispute> CreateAsync(Dispute dispute);
    Task<Dispute?> GetByIdAsync(string id);
    Task<Dispute?> GetByOrderIdAsync(string orderId);
    Task<List<Dispute>> GetByUserIdAsync(string userId);
    Task<List<Dispute>> GetAllAsync(DisputeStatus? status = null, int page = 1, int pageSize = 20);
    Task<long> GetCountAsync(DisputeStatus? status = null);
    Task UpdateAsync(string id, Dispute dispute);
    Task AddMessageAsync(string disputeId, DisputeMessage message);
}
