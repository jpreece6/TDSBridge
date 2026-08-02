using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TDSBridge;

public class WriteBackService : BackgroundService
{
    readonly ILogger<WriteBackService> _logger;

    public WriteBackService(ILogger<WriteBackService> logger)
    {
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting WriteBackService");

            while (!stoppingToken.IsCancellationRequested)
            {
                // TODO check for data
                // TODO check if server is online
                // TODO write data to server
                
                
                await Task.Delay(500);
            }


        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception: {Ex}", ex.ToString());
        }
        finally
        {
            _logger.LogInformation("Stopping WriteBackService");
        }

    }
}