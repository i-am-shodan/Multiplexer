# Connection multiplexer
Basically socat with state. When a new connection is received on the monitored port newconnection.sh is run, then a few minutes after the last connection is made endconnection.sh is ran. You provide the IP + Port to forward to in the newconnection.sh script.

Code is also a good example for MITMing UDP/TCP connections with C# which is what I originally wrote it for.

Tested on Windows and Linux

## Build
`dotnet build`

## Run
`dotnet run -t TCP_PORT -u UDP_PORT --onconnect newconnection.sh --oncleanup endconnection.sh`