using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System.Drawing;
using System.Net;

Console.WriteLine("EasyNet Unity Server");

long serverTick = 0;
Server server = new Server(true);
server.managed = true;
uint MasterID = 2;

Dictionary<uint, string> clientsInRooms = new Dictionary<uint, string>();

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
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("Main Loop Intacked");
        Console.ResetColor();
        if (serverTick > 9000000000000000000)
        {
            serverTick = -9000000000000000000;
        }
        playerList = "";

        foreach (uint id in server.clients.Keys)
        {
            playerList += $"-{id}";
        }
        MasterID = server.GetMasterClient();

        foreach (uint id in clientsInRooms.Keys)
        {
            if (!server.clients.ContainsKey(id))
            {
                clientsInRooms.Remove(id);
            }
        }

        while (server.cache.TryDequeue(out var packet))
        {
            packet.Format();

            int log = 0;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Processing Packet: {packet.PacketID}");
            Console.ResetColor();

            if (server.clients[packet.SenderID] == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Packet Recieved from Disconected Client");
                Console.ResetColor();
                continue;
            }
            Console.ForegroundColor = ConsoleColor.Magenta; //1
            log++;
            Console.WriteLine($"LOG: {log} ");
            Console.ResetColor();

            if (packet.dataType == DataType.Custom || packet.dataType == DataType.String)
            {
                string msg = Bytes.ToString(packet.Data);
                Console.ForegroundColor = ConsoleColor.Magenta;//2
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();
                if (msg.Contains("PLAYER_LIST"))
                {
                    Console.WriteLine($"Sending PLAYER_LIST to {packet.SenderID}");
                    string[] splitted = msg.Split(' ');
                    splitted[2] = packet.SenderID.ToString();
                    msg = "";
                    foreach (string s in splitted) {
                        msg += s + " ";
                    }
                    msg.Replace("PLAYER_LIST", playerList);
                    packet.Data = Bytes.Get(msg);
                }
                Console.ForegroundColor = ConsoleColor.Magenta;//3
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();
                if (msg.Contains("MASTER_CLIENT"))
                {
                    packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("MASTER_CLIENT", MasterID.ToString()));
                    packet.target = server.clients[MasterID];
                }
                Console.ForegroundColor = ConsoleColor.Magenta;//4
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();
                try {
                    if (Bytes.ToString(packet.Data).Contains("JOINROOM"))
                    {
                        string roomToJoin = Bytes.ToString(packet.Data).Split('¬')[1];
                        clientsInRooms[packet.SenderID] = roomToJoin;
                        Console.ForegroundColor = ConsoleColor.Green;
                        uint senderID = packet.SenderID;
                        packet = new Packet(server, Bytes.Get($"JOINED_ROOM¬{roomToJoin}¬{packet.SenderID}"), DataType.Custom);
                        packet.SenderID = senderID;
                        packet.target = server.clients[packet.SenderID];
                        Console.WriteLine($"Added Client ID: {packet.SenderID} To Room: {clientsInRooms[packet.SenderID]}");
                        server.notSentPackets.TryAdd(packet.PacketID, packet);
                        Console.ResetColor();
                    }
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error when managing the JOINROOM server command Error:\n{e}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                Console.ForegroundColor = ConsoleColor.Magenta;//5
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();
                if (msg.Contains("LEAVEROOM"))
                {
                    clientsInRooms.Remove(packet.SenderID);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Removed Client ID: {packet.SenderID} from their room");
                    Console.ResetColor();
                }
                Console.ForegroundColor = ConsoleColor.Magenta;//6
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();

                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("SERVER_TICK", serverTick.ToString()));
                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("PLAYER_LIST", playerList));
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.WriteLine($"Echoing Packet of:\nType: {packet.dataType}\n Data: {Bytes.ToString(packet.Data)}\nto room: {clientsInRooms[packet.SenderID]}");
                Console.ForegroundColor = ConsoleColor.White;
                packet.rawData = packet.GetBytes();
                packet.Format();
                Console.ForegroundColor = ConsoleColor.Magenta;//7
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();

                if(packet.target == null)
                {
                    if (clientsInRooms.TryGetValue(packet.SenderID, out var senderRoom))
                    {
                        foreach (var client in clientsInRooms.ToList())
                        {
                            if (client.Value == senderRoom)
                            {
                                packet.target = server.clients[client.Key];
                                server.notSentPackets.TryAdd(packet.PacketID, packet);
                                Console.WriteLine($"Sending Packet to: {client.Key} in: {senderRoom}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Warning: ID {packet.SenderID} sent packet before joining a room.");
                    }
                }
                else
                {
                    server.notSentPackets.TryAdd(packet.PacketID, packet);
                }
                
                Console.ForegroundColor = ConsoleColor.Magenta;//8
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();
            }
            else if (packet.dataType == DataType.Ping)
            {
                Console.WriteLine($"Client [{packet.SenderID}] Pinged Back.");
            }
            else if (packet.dataType == DataType.Int)
            {
                Console.WriteLine($"Integer Recienved: {Bytes.ToInt32(packet.Data)}");
            }
            else
            {
                Console.WriteLine($"Echoing Packet of type: {packet.dataType} ");
                if (clientsInRooms.TryGetValue(packet.SenderID, out string senderRoom))
                {
                    foreach (var client in clientsInRooms.ToList())
                    {
                        if (client.Value == senderRoom)
                        {
                            server.notSentPackets.TryAdd(packet.PacketID, packet);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: ID {packet.SenderID} sent packet before joining a room.");
                }
            }
            Console.ForegroundColor = ConsoleColor.Magenta;//9
            log++;
            Console.WriteLine($"LOG: {log} ");
            Console.ResetColor();


        }
        serverTick++;
        await Task.Delay(100);
    }
}

