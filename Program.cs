using CommandLine;
using Microsoft.Extensions.Logging;
using Multiplexer;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;

Options commandLineOptions = new();
Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o => commandLineOptions = o);

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "hh:mm:ss ";
    }));
ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

ConcurrentDictionary<IPAddress, UDPForwardSession> udpServers = new();

using (var cts = new CancellationTokenSource())
using (var unmanagedResources = new Resource(
    TimeSpan.FromSeconds(30), 
    TimeSpan.FromMinutes(2), 
    loggerFactory.CreateLogger<Resource>(), 
    (ip) => { 
        if (udpServers.TryRemove(ip, out var session))
        {
            session.Dispose();
        }
    }, 
    cts.Token))
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        ScriptService.Executable = @"C:\windows\system32\windowspowershell\v1.0\powershell.exe";
    }
    else
    {
        ScriptService.Executable = "bash";
    }

    ScriptService.OnNewConnectionCmd = commandLineOptions.OnNewConnectionCmd;
    ScriptService.OnDisconnectCmd = commandLineOptions.OnCleanupCmd;

    new TcpServer(commandLineOptions.TCPPorts, loggerFactory.CreateLogger<TcpServer>()).StartListening(async (client, token) => 
    {  
        logger.LogInformation("Got TCP connection");

        var forward = new TCPForwardSession(client, unmanagedResources, loggerFactory.CreateLogger<TCPForwardSession>());
        await forward.Start(token);

    }, cts.Token);

    new UdpServer(commandLineOptions.UDPPorts, loggerFactory.CreateLogger<UdpServer>()).StartListening(async (listener, result, token) =>
    {
        var ip = result.RemoteEndPoint.Address;

        var forward = udpServers.GetOrAdd(ip, (addr) =>
        {
            logger.LogInformation("Got UDP connection");
            return new UDPForwardSession(listener, result.RemoteEndPoint, unmanagedResources, loggerFactory.CreateLogger<UDPForwardSession>());
        });
        await forward.Start(result, token);

    }, cts.Token);

    Console.WriteLine("Press enter to quit");
    Console.ReadLine();
    cts.Cancel();

    logger?.LogInformation("Terminating, disposing of acquired resources");
    foreach (var server in udpServers.Values)
    {
        logger?.LogInformation("Disposing of UDP background server");
        server.Dispose();
    }
}