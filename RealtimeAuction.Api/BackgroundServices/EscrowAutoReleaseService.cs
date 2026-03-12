using Microsoft.Extensions.Hosting;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Chạy định kỳ để tự động giải phóng Escrow cho các đơn hàng
/// đã shipped quá 7 ngày mà buyer chưa xác nhận.
/// </summary>
public class EscrowAutoReleaseService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscrowAutoReleaseService> _logger;
    
    // Chạy mỗi 1 giờ
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public EscrowAutoReleaseService(
        IServiceProvider serviceProvider,
        ILogger<EscrowAutoReleaseService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EscrowAutoReleaseService started. Checking every {Interval}.", _checkInterval);

        // Chờ 30 giây sau khi start để các services khác khởi tạo xong
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("EscrowAutoReleaseService: Running auto-release check...");

                using var scope = _serviceProvider.CreateScope();
                var escrowService = scope.ServiceProvider.GetRequiredService<IEscrowService>();
                
                await escrowService.ProcessAutoReleaseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EscrowAutoReleaseService: Error during auto-release check");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("EscrowAutoReleaseService stopped.");
    }
}
