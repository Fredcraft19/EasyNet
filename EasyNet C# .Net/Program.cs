// Developer Front-End
using EasyNet.Manager;
using EasyNet.View;
using System.Net;
using EasyNet_Debugging;

debug.debugmode = DebugMode.All;
NetworkManager.Initialize(IPAddress.Loopback, 8081, true);
Network.BindRPC("bob", bob);
void bob()
{
    Console.WriteLine("Bob has spoken..");
}

Network.RPC("bob", EasyNet.RPCTarget.Others);