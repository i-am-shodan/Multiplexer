using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace Multiplexer
{
    public class Resource : IDisposable
    {
        private readonly ConcurrentDictionary<IPAddress, IService> services = new();
        private readonly Task BackgroundCleanup;
        private readonly ILogger<Resource> logger;
        private bool disposedValue;

        public Resource(TimeSpan cleanupInterval, TimeSpan stateSessionTimespan, ILogger<Resource> logger, Action<IPAddress> onCleanup, CancellationToken token)
        {
            this.logger = logger;

            BackgroundCleanup = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(cleanupInterval);

                    logger?.LogInformation("Looking for resources to free");
                    var keysToRemove = services.Where(x => (DateTime.UtcNow - x.Value.LastRequestedTime) >= stateSessionTimespan).Select(x => x.Key).ToList();

                    foreach (var key in keysToRemove)
                    {
                        logger?.LogInformation("Cleaning up "+key);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        services.TryRemove(key, out IService s);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                        s.Dispose();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        onCleanup?.Invoke(key);
                    }
                }
            }, token);
        }

        public IService Request(IPAddress ip)
        {
            var service =  services.GetOrAdd(ip, (addr) => {
                logger.LogInformation("Created new service");
                return new ScriptService(addr, logger);
            });
            service.LastRequestedTime = DateTime.UtcNow;
            return service;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    logger?.LogInformation("Disposing of "+services.Count+" resources");
                    Parallel.ForEach(services.Values, (service) => service.Dispose());
                    services.Clear();
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

    internal class ScriptService : IService, IDisposable
    {
        internal static string Executable = string.Empty;
        internal static string OnNewConnectionCmd = string.Empty;
        internal static string OnDisconnectCmd = string.Empty;

        private readonly ILogger<Resource> logger;
        private bool disposedValue;

        private DateTime _firstRequestedTime = DateTime.UtcNow;
        private DateTime _lastRequestedTime = DateTime.UtcNow;
        private IPAddress _ip;
        private int _port;
        private IPAddress attackersIp;

        public DateTime LastRequestedTime { get => _lastRequestedTime; set => _lastRequestedTime = value; }
        public DateTime FirstRequestedTime => _firstRequestedTime;
        public IPAddress IPAddress => _ip;
        public int Port => _port;

        public ScriptService(IPAddress attackersIp, ILogger<Resource> logger)
        {
            this.logger = logger;

            try
            {
                var output = Run(Executable, OnNewConnectionCmd + " " + attackersIp.ToString());
                var scriptOutput = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Last().Split(":");

                var ipStr = scriptOutput[0].Trim();
                var portStr = scriptOutput[1].Trim();

                this.attackersIp = attackersIp;
                this._ip = IPAddress.Parse(ipStr);
                this._port = int.Parse(portStr);

                logger?.LogInformation("Resource acquired, will connect to: " + _ip + ":" + _port);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Cannot get resources!");
                throw;
            }
        }

        private string Run(string cmd, string arguments)
        {
            logger.LogInformation("Running script: "+cmd + " "+ arguments);

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo(cmd, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.Start();
            process.WaitForExit();

            return process.StandardOutput.ReadToEnd();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                try
                {
                    Run(Executable, OnDisconnectCmd + " " + attackersIp.ToString());
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error during dispose");
                }

                disposedValue = true;
            }
        }

        ~ScriptService()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
