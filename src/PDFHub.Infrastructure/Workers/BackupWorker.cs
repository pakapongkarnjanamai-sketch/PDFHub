using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Services;

namespace PDFHub.Infrastructure.Workers;

/// <summary>In-memory queue of manual backups; the worker runs one at a time.</summary>
public sealed class BackupQueue : IBackupQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<int> Reader => _channel.Reader;

    public void Enqueue(int backupRunId) => _channel.Writer.TryWrite(backupRunId);
}

/// <summary>Executes queued manual backups, each in a fresh DI scope (backend-aspnet-background-workers).</summary>
public sealed class BackupWorker(IServiceProvider serviceProvider, BackupQueue queue, ILogger<BackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Worker} started.", nameof(BackupWorker));
        try
        {
            // The queue lives in memory, so a run that was in progress when the app stopped can never finish.
            using (var scope = serviceProvider.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IBackupService>().FailInterruptedAsync(stoppingToken);

            await foreach (var runId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = serviceProvider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IBackupService>().ExecuteAsync(runId, stoppingToken);
                    logger.LogInformation("Manual backup {RunId} finished.", runId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Manual backup {RunId} failed.", runId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        logger.LogInformation("{Worker} stopped.", nameof(BackupWorker));
    }
}
