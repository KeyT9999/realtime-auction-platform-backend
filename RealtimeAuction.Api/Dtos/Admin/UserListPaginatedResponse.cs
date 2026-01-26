namespace RealtimeAuction.Api.Dtos.Admin;

public class UserListPaginatedResponse
{
    public List<UserListResponse> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
