using System.Net;

namespace Multiplexer
{
    public interface IService : IDisposable
    {
        public DateTime FirstRequestedTime { get; }
        public DateTime LastRequestedTime { get; set; }
        public IPAddress IPAddress { get; }
        public int Port { get; }
    }
}
