using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;

namespace Multiplexer
{
    public class UDPForwardSession : IDisposable
    {
        private readonly IPEndPoint attacker;
        private readonly IService service;
        private readonly Task BackgroundTask;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly UdpClient ourUDPservice;
        private readonly UdpClient listener;

        private readonly ILogger<UDPForwardSession> logger;
        private bool disposedValue;

        public UDPForwardSession(UdpClient listener, IPEndPoint attackerEndP, Resource resource, ILogger<UDPForwardSession> logger)
        {
            this.attacker = attackerEndP;
            this.logger = logger;
            this.listener = listener;

            service = resource.Request(attackerEndP.Address);
            ourUDPservice = new UdpClient(service.IPAddress.ToString(), service.Port);

            BackgroundTask = Task.Run(async () =>
            {
                try
                {
                    using (ourUDPservice)
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            var dataFromAttacker = await ourUDPservice.ReceiveAsync(cancellationTokenSource.Token);

                            await listener.SendAsync(dataFromAttacker.Buffer, dataFromAttacker.Buffer.Length, attackerEndP);
                        }
                    }
                }
                catch (TaskCanceledException)
                {

                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "UDP");
                }
            }, cancellationTokenSource.Token);
        }

        public async Task Start(UdpReceiveResult result, CancellationToken token)
        {
            service.LastRequestedTime = DateTime.UtcNow;
            await ourUDPservice.SendAsync(result.Buffer, token);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Cancel();
                    BackgroundTask.Wait();
                    cancellationTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
