// Developer Front-End
using EasyNet.Manager;
using EasyNet.View;
using System.Net;
using EasyNet_Debugging;
using EasyNet;

NetworkManager.DebugMode = DebugMode.None;
NetworkManager.Initialize(IPAddress.Loopback, 8080, true);
NetworkManager.Connect();
NetworkManager.JoinRoom("12345");

void Message(string sender, string msg)
{
    Console.WriteLine($"[{sender}] {msg}");
}
Network.BindRPC<string, string>("message", Message);
Network.RegisterVariable<int>("number");

string uin;
while (true)
{
    uin = Console.ReadLine() ?? "";
    try
    {
        string[] parts = uin.Split(' ');
        if (parts[0] == "msg")
        {
            if (parts.Length > 2)
            {
                Network.RPC("message", RPCTarget.Others, parts[1], parts[2]);
            }
        }
        else if (uin == "connect")
        {
            NetworkManager.Connect();
        }
        else if (uin == "room")
        {
            NetworkManager.JoinRoom("12345");
        }
        else if (uin == "id")
        {
            Console.WriteLine("-- CLIENT IDS --");
            Console.WriteLine($"Backend: {NetworkManager.client.ID}\nManager: {NetworkManager.ID}\nView: {Network.ID}");
        }
        else if (uin == "clear" || uin == "clr")
            Console.Clear();
        else if (uin == "ping")
        {
            NetworkManager.client.SendPacket(new EasyNet_BackEnd.Data.Packet(NetworkManager.client, EasyNet_BackEnd.Data.Bytes.Get("PINGING SERVER :3"), EasyNet_BackEnd.Data.DataType.String));
        }
        else if (uin == "leave")
            NetworkManager.LeaveRoom();
        else if (uin == "read")
        {
            Console.WriteLine($"Number: '{Network.GetVariable<int>("number")}'");
        }
        else if (uin == "add")
        {
            Network.SetVariable("number", Network.GetVariable<int>("number") + 1);
        }
        else
            Console.WriteLine("Command not recognised.");
    }
    catch (Exception e) { }
    
}
