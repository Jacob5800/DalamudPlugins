using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BiSplanner.Services
{
    public class QueueService(ITypedLogger<QueueService> logger) : IQueueService
    {
        private readonly ITypedLogger<QueueService> logger = logger;

        private readonly CancellationTokenSource tokenSource = new();
        private readonly Channel<(string TaskName, Action Task)> taskChannel =
            Channel.CreateBounded<(string TaskName, Action Task)>(new BoundedChannelOptions(20)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
            });
        private Task? workerLoop;

        private static async Task doWorkerLoop(
            ChannelReader<(string TaskName, Action Task)> reader,
            ITypedLogger<QueueService> logger,
            CancellationToken token
            )
        {
            try
            {
                while (await reader.WaitToReadAsync(token))
                {
                    var (taskName, task) = await reader.ReadAsync(token);
                    token.ThrowIfCancellationRequested();
                    logger.Verbose($"[{taskName}] Executing task");
                    try
                    {
                        task();
                        logger.Verbose($"[{taskName}] Task complete");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"[{taskName}] Task execution failed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.Warning($"Worker loop cancelled");
            }
        }

        public bool Enqueue(string taskName, Action task)
        {
            if (tokenSource.IsCancellationRequested) return false;
            var count = taskChannel.Reader.CanCount
                ? $"{taskChannel.Reader.Count}"
                : "?";

            logger.Verbose($"[{taskName}] Enqueuing queue task ({count})");

            if (!taskChannel.Writer.TryWrite((taskName, task)))
            {
                logger.Warning($"[{taskName}] Failed to enqueue task");
                return false;
            }

            return true;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);
            workerLoop = Task.Run(
                () => doWorkerLoop(taskChannel.Reader, logger, tokenSource.Token),
                cancellationToken
                );

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Do not drain queued plugin actions after shutdown starts.
            taskChannel.Writer.TryComplete();
            tokenSource.Cancel();
            if (workerLoop != null) await workerLoop.WaitAsync(cancellationToken);
            logger.Verbose($"Queue service stopped; queued actions abandoned");
        }

    }

    public interface IQueueService : IHostedService
    {
        public bool Enqueue(string taskName, Action task);
    }
}
