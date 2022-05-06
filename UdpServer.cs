using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace Multiplexer
{
    internal class UdpServer
    {
        private IEnumerable<int> ports;
        private ILogger<UdpServer> logger;

        public UdpServer(IEnumerable<int> ports, ILogger<UdpServer> logger)
        {
            this.ports = ports;
            this.logger = logger;
        }

        public IEnumerable<Task> StartListening(Func<UdpClient, UdpReceiveResult, CancellationToken, Task> onNewConnection, CancellationToken token)
        {
            var tasks = new List<Task>();

            foreach (var port in ports)
            {
                logger.LogInformation("Starting UDP listener on " + port);
                tasks.Add(StartListening(port, onNewConnection, token));
            }

            return tasks;
        }

        internal async Task StartListening(int port, Func<UdpClient, UdpReceiveResult, CancellationToken, Task> onNewConnection, CancellationToken token)
        {
            var listener = new UdpClient(port);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var attackerPayload = await listener.ReceiveAsync(token);
                    // logger.LogInformation("Got UDP connection from: "+attackerPayload.RemoteEndPoint.Address); // very noisy

                    await onNewConnection(listener, attackerPayload, token);
                }
            }
            catch (SocketException e)
            {
                logger.LogError(e, "UDP exception");
            }
            finally
            {
                listener.Close();
            }
        }
    }
}
