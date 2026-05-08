using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System.Net;

Console.WriteLine("EasyNet Unity Server");

long serverTick = 0;
Server server = new Server(true);
server.managed = true;

string playerList = "";

while (server.clients.Count == 0)
{
    Thread.Sleep(50);
}

Console.Clear();
_ = ClearCache();
Console.WriteLine("EasyNet Unity Server");

while (true)
{
    string uin = Console.ReadLine() ?? "";
    if (uin == "debug")
    {
        Console.Clear();
        Console.WriteLine($"Saved End point count: {server.clients.Values.Count}");
        foreach (EndPoint ep in server.clients.Values)
        {
            Console.WriteLine($"Endpoint: {ep}");
        }
    }
    else if (uin == "clear")
    {
        Console.Clear();
    }
    else if (uin == "ping")
    {
        foreach (EndPoint ep in server.clients.Values)
        {
            Console.WriteLine($"Pinging Client {server.clients.FirstOrDefault(x => x.Value == ep).Key}");
            byte[] bytes = Bytes.Get(server.clients.FirstOrDefault(x => x.Value == ep).Key);
            Packet updateID = new Packet(server, bytes, DataType.SetID);
            updateID.target = ep;
            server.notSentPackets.TryAdd(updateID.PacketID, updateID);
        }
    }
    else if (uin == "reset" || uin == "end")
    {
        server.clients.Clear();
        Console.Clear();
    }
    else
    {
        Packet msg = new Packet(server, Bytes.Get(uin), DataType.String);
        msg.Format();
        server.notSentPackets.TryAdd(msg.PacketID, msg);
    }
}

async Task ClearCache()
{
    while (true)
    {
        if (serverTick > 9000000000000000000)
        {
            serverTick = -9000000000000000000;
        }
        playerList = "";
        foreach(uint id in server.clients.Keys)
        {
            playerList += $"{id}-";
        }
       

        while (server.cache.TryDequeue(out var packet))
        {
            packet.Format();

            if (server.clients[packet.SenderID] == null)
            {
                Console.WriteLine("Packet Recieved from Disconected Client");
                continue;
            }



            if (packet.dataType == DataType.Custom || packet.dataType == DataType.String)
            {
                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("SERVER_TICK", serverTick.ToString()));
                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("PLAYER_LIST", playerList));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Echoing Packet of:\nType: {packet.dataType}\n Data: {Bytes.ToString(packet.Data)} ");
                packet.Data = Bytes.Get(Bytes.ToString(packet.Data));
                Console.ForegroundColor = ConsoleColor.White;
                packet.rawData = packet.GetBytes();
                packet.Format();
                server.notSentPackets.TryAdd(packet.PacketID, packet);
            }
            else if (packet.dataType == DataType.Ping)
            {
                Console.WriteLine($"Client [{packet.SenderID}] Pinged Back.");
            }
            else if(packet.dataType == DataType.Int)
            {
                Console.WriteLine($"Integer Recienved: {Bytes.ToInt32(packet.Data)}");
            }
            else
            {
                Console.WriteLine($"Echoing Packet of type: {packet.dataType} ");
                server.notSentPackets.TryAdd(packet.PacketID, packet);
            }


        }
        serverTick++;
        await Task.Delay(100);
    }
}
