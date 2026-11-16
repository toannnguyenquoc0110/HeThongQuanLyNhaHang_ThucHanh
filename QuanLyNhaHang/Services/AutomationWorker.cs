namespace QuanLyNhaHang.Services;

/// <summary>
/// Chạy nền: mỗi 5 giây đưa món chờ vào bếp. Không tự đặt hàng, không tự nhập kho (hai việc đó làm tay).
/// </summary>
public class AutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AutomationWorker> _log;

    public AutomationWorker(IServiceScopeFactory scopes, ILogger<AutomationWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var kitchen = scope.ServiceProvider.GetRequiredService<KitchenDispatcher>();
                await kitchen.DispatchAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Tự động hóa bếp gặp lỗi; sẽ thử lại ở lượt sau.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
