using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System.Net;
using EasyNet_Debugging;

Console.WriteLine("EasyNet Server");

long serverTick = 0;
Server server = new Server(8080, true);
server.managed = true;
uint MasterID = 2;

Dictionary<uint, string> clientsInRooms = new Dictionary<uint, string>();
string playerList = "";

uint NewPacketID()
{
    byte[] bytes = Guid.NewGuid().ToByteArray();

    uint part1 = BitConverter.ToUInt32(bytes, 0);
    uint part2 = BitConverter.ToUInt32(bytes, 4);
    uint part3 = BitConverter.ToUInt32(bytes, 8);
    uint part4 = BitConverter.ToUInt32(bytes, 12);

    return part1 ^ part2 ^ part3 ^ part4;
}
string GetPlayerList(string name)
{
    string list = "";
    foreach (uint id in clientsInRooms.Keys)
    {
        if (clientsInRooms[id] == name)
            list += $"¬{id}";

    }
    return list;
}

while (server.clients.Count == 0)
{
    Thread.Sleep(50);
}

Console.Clear();
_ = ClearCache();
Console.WriteLine("EasyNet Server");

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
    else if (uin == "clients")
    {
        Console.WriteLine("Clients list:");
        foreach (uint id in clientsInRooms.Keys)
        {
            Console.WriteLine($"ID: {id} In Room: {clientsInRooms[id]}");
        }
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
        //Console.ForegroundColor = ConsoleColor.Blue;
        //Console.WriteLine("Main Loop Intacked");
        //Console.ResetColor();

        MasterID = server.GetMasterClient();

        foreach (uint id in clientsInRooms.Keys.ToList())
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

            if (!server.clients.ContainsKey(packet.SenderID) && packet.SenderID > 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Packet Recieved from Disconected Client");
                Console.ResetColor();
                continue;
            }
            
            Console.ForegroundColor = ConsoleColor.Magenta; //1
            log++;
            Console.WriteLine($"LOG: {log}");
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
                    foreach (string s in splitted)
                    {
                        msg += s + " ";
                    }

                    playerList = GetPlayerList(clientsInRooms[packet.SenderID]);

                    msg = msg.Replace("PLAYER_LIST", playerList);
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
                try
                {
                    if (Bytes.ToString(packet.Data).Contains("JOINROOM"))
                    {
                        string roomToJoin = Bytes.ToString(packet.Data).Split('¬')[1];
                        clientsInRooms[packet.SenderID] = roomToJoin;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Packet joinRoomResponse = new Packet(server, Bytes.Get($"JOINED_ROOM¬{roomToJoin}¬{packet.SenderID}" + $"¬{packet.SenderID}"), DataType.Custom);
                        joinRoomResponse.SenderID = packet.SenderID;
                        joinRoomResponse.target = server.clients[packet.SenderID];
                        Console.WriteLine($"Added Client ID: {joinRoomResponse.SenderID} To Room: {clientsInRooms[joinRoomResponse.SenderID]}");
                        server.notSentPackets.TryAdd(joinRoomResponse.PacketID, packet);
                        Console.ResetColor();
                        continue; // make sure this packet isnt echoed to other clients
                    }
                    else
                    {
                        if (!clientsInRooms.ContainsKey(packet.SenderID))   // ROOM CHECK #1, to check if we should ignore this packet, because if the sender isnt in a room, they shouldnt be able to communicate.
                        {
                            Console.WriteLine("Packet Recieved from client not in room. Ignoring!");
                            continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error when managing the JOINROOM server command Error:\n{e}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                if (!clientsInRooms.ContainsKey(packet.SenderID))   // ROOM CHECK #2 (why not?!) (better to be safe then sorry!)
                {
                    Console.WriteLine("Packet Recieved from client not in room. Ignoring!");
                    continue;
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
                    continue; // dont want to echo this. (its a server command just like JOINROOM)
                }
                Console.ForegroundColor = ConsoleColor.Magenta;//6
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();

                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("SERVER_TICK", serverTick.ToString()));
                packet.Data = Bytes.Get(Bytes.ToString(packet.Data).Replace("PLAYER_LIST", playerList));

                packet.rawData = packet.GetBytes();
                packet.Format();
                Console.ForegroundColor = ConsoleColor.Magenta;//7
                log++;
                Console.WriteLine($"LOG: {log} ");
                Console.ResetColor();

                if (packet.target == null)
                {
                    if (clientsInRooms.TryGetValue(packet.SenderID, out var senderRoom))
                    {
                        foreach (var client in clientsInRooms.ToList())
                        {
                            if (client.Value == senderRoom)
                            {
                                Packet outbound = new Packet(server, packet.Data, packet.dataType);

                                outbound.target = server.clients[client.Key];
                                outbound.PacketID = NewPacketID();
                                outbound.SenderID = packet.SenderID;

                                if (Bytes.ToString(outbound.Data).Contains("JOINROOM"))
                                {
                                    outbound.Data = Bytes.Get(Bytes.ToString(outbound.Data) + $"¬{packet.SenderID}");
                                }

                                server.notSentPackets.TryAdd(outbound.PacketID, outbound);
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Sending Packet to: {client.Key} in: {senderRoom}");
                                Console.WriteLine($"Echoing Packet of:\nType: {packet.dataType}\n Data: {Bytes.ToString(packet.Data)}\nto room: {clientsInRooms[packet.SenderID]}");
                                Console.ForegroundColor = ConsoleColor.White;
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
                if (clientsInRooms.TryGetValue(packet.SenderID, out var senderRoom))
                {
                    foreach (var client in clientsInRooms.ToList())
                    {
                        if (client.Value == senderRoom)
                        {
                            Packet outbound = new Packet(server, packet.Data, packet.dataType);

                            outbound.target = server.clients[client.Key];
                            outbound.PacketID = NewPacketID();
                            outbound.SenderID = packet.SenderID;

                            server.notSentPackets.TryAdd(outbound.PacketID, outbound);
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine($"Sending Packet to: {client.Key} in: {senderRoom}\nData: {Bytes.ToString(outbound.Data)}");
                            Console.ResetColor();
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
        await Task.Delay(8);    // 125/Second (125 tickrate)
    }
}
