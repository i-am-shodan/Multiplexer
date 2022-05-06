using CommandLine;

public class Options
{
    [Option('t', "tcp", Required = true, HelpText = "TCP Ports to forward.")]
    public IEnumerable<int> TCPPorts { get; set; } = new List<int>();

    [Option('u', "udp", Required = true, HelpText = "UDP Ports to forward.")]
    public IEnumerable<int> UDPPorts { get; set; } = new List<int>();

    [Option('c', "onconnect", Required = true, HelpText = "Command to run on a new connection.")]
    public string OnNewConnectionCmd { get; set; } = string.Empty;

    [Option('l', "oncleanup", Required = true, HelpText = "Command to run to cleanup a set of connections.")]
    public string OnCleanupCmd { get; set; } = string.Empty;
}