using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace Multiplexer
{
    internal class TcpServer
    {
        private IEnumerable<int> ports;
        private ILogger<TcpServer> logger;

        public TcpServer(IEnumerable<int> ports, ILogger<TcpServer> logger)
        {
            this.logger = logger;
            this.ports = ports;
        }

        public IEnumerable<Task> StartListening(Func<TcpClient, CancellationToken, Task> onNewConnection, CancellationToken token)
        {
            var tasks = new List<Task>();

            foreach (var port in ports)
            {
                logger.LogInformation("Starting TCP listener on " + port);
                tasks.Add(StartListening(port, onNewConnection, token));
            }

            return tasks;
        }

        internal async Task StartListening(int port, Func<TcpClient, CancellationToken, Task> onNewConnection, CancellationToken token)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start(); // Start your listener

            while (!token.IsCancellationRequested) // Permanent loop, it may not be the best solution
            {
                var client = await listener.AcceptTcpClientAsync(token);
                logger.LogInformation("Got TCP connection from: "+(client?.Client?.RemoteEndPoint as IPEndPoint)?.Address);

#pragma warning disable CS8604 // Possible null reference argument.
                _ = onNewConnection(client, token);
#pragma warning restore CS8604 // Possible null reference argument.
            }
        }
    }
}
