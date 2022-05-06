using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Multiplexer
{
    public class TCPForwardSession
    {
        private readonly TcpClient attackerTcpClient;
        private readonly IPAddress attackersIp;
        private readonly Resource resource;
        private readonly ILogger<TCPForwardSession> logger;

        public TCPForwardSession(TcpClient client, Resource resource, ILogger<TCPForwardSession> logger)
        {
            this.logger = logger;

            this.attackerTcpClient = client;
            this.resource = resource;

            var endPoint = client.Client.RemoteEndPoint as IPEndPoint;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            attackersIp = endPoint.Address;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        public async Task Start(CancellationToken token)
        {
            // connect to machine & copy streams

            // we need to check the client actually wants to the talk to the server
            // port scans are a waste of everyones time
            byte firstByte;
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                using (var jointToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token))
                {
                    byte[] buffer = new byte[1];
                    int read = await attackerTcpClient.GetStream().ReadAsync(buffer, 0, 1, jointToken.Token);
                    if (read == -1)
                    {
                        throw new Exception();
                    }
                    firstByte = buffer[0];
                }
            }
            catch (Exception)
            {
                // this is expected
                logger.LogInformation("Port scan detected, not forwarding...");
                return;
            }

            try
            {
                var resource = this.resource.Request(attackersIp);
                
                var forwardToAddress = resource.IPAddress;
                var forwardToPort = resource.Port;

                using (var ourTCPService = new TcpClient())
                using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    await ourTCPService.ConnectAsync(forwardToAddress, forwardToPort);
                    logger.LogInformation("Connected to server " + forwardToAddress + ":" + forwardToPort);

                    // write first byte back
                    await ourTCPService.GetStream().WriteAsync(new byte[] { firstByte }, 0, 1);

                    // copy data from attacker to server
                    var clientCopyTask = attackerTcpClient.GetStream().CopyToAsync(ourTCPService.GetStream(), 81920, linkedToken.Token);

                    // copy data from our server to attacker
                    var serverCopyTask = ourTCPService.GetStream().CopyToAsync(attackerTcpClient.GetStream(), 81920, linkedToken.Token);

                    while (!linkedToken.IsCancellationRequested)
                    {
                        resource.LastRequestedTime = DateTime.UtcNow;

                        await Task.Delay(1000, token);
                        if (!ourTCPService.Connected ||
                            !attackerTcpClient.Connected ||
                            clientCopyTask.IsCompleted ||
                            serverCopyTask.IsCompleted ||
                            clientCopyTask.IsFaulted ||
                            serverCopyTask.IsFaulted)
                        {
                            logger.LogInformation("Shutting down TCP connection");
                            linkedToken.Cancel();
                        }
                    }

                    await Task.WhenAll(clientCopyTask, serverCopyTask);               
                }
                
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occured");
            }
        }
    }
}
