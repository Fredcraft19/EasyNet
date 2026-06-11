using EasyNet;
using EasyNet.View;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using EasyNet_Debugging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Security.Principal;
using NetworkView = EasyNet.View.Network;
namespace EasyNet.Manager
{
    public static class NetworkManager
    {
        // Server to connect to info:
        private static IPAddress ip;
        private static int port;

        // Network Client Classes
        public static SystemClient client;

        public static long ID;
        /// <summary>
        /// If the client is connected to the server
        /// </summary>
        public static bool IsConnected;
        /// <summary>
        /// If the client is connected to a room
        /// </summary>

        public static bool InRoom;
        /// <summary>
        /// The room name that the client is connected to. Empty if not in room
        /// </summary>
        public static string ConnectedRoom;
        /// <summary>
        /// If client is the master client
        /// </summary>
        public static bool IsMaster = false;
        /// <summary>
        /// The ID of the master client
        /// </summary>
        public static uint MasterID = 0;

        public static bool connectOnAwake = false;

        public static long serverTick;
        public static int tickSpeed;
        /// <summary>
        /// Objects that can be spawned in by EasyNet
        /// </summary>
        /// <summary>
        /// Objects that have been spawned in by EasyNet
        /// </summary>
        public static DebugMode debugMode = DebugMode.None;


        private static List<long> PlayerList = new List<long>();

        private static bool SpawnedPlayer = false;
        public static void Initialize(IPAddress _ip, int _port, bool ConnectToServer)
        {
            ip = _ip; port = _port;
            client = new SystemClient(ip, port);
            NetworkView.Initialize();
          
            Awake();
            Start();

            if (ConnectToServer)
                Connect();

            Loop();


        }
        async static Task Loop()
        {
            if(!IsConnected)
                await Task.Delay(500);
            else
                await Task.Delay(2000);
            Update();
        }
        static void Awake()
        {
            if (port == 0)
                port = 8080;

            client.managed = true;
            if (connectOnAwake)
            {
                Packet connectPing = new Packet(client, Bytes.Get(1));
            }

        }
        static void Start()
        {
            client.UpdateRate = 50f;
            SlowUpdate();
            SlowerUpdate();
        }
        static void Update()
        {
            debug.debugmode = debugMode;
            if (client.ID != 0)
            {
                IsConnected = true;
            }
        }

        public static void Connect()
        {
            Packet ping = new Packet(client, Bytes.Get(0));
            client.SendPacket(ping);
        }
        public static void JoinRoom(string roomName)
        {
            Packet output = new Packet(client, Bytes.Get($"JOINROOM¬{roomName}"), DataType.Custom);
            client.SendPacket(output);
        }
        public static void LeaveRoom()
        {
            if (InRoom)
            {
                Packet leaveRequest = new Packet(client, Bytes.Get("¬LEAVEROOM"));
                client.SendPacket(leaveRequest);
            }
            else
            {
                if(debug.Warning())
                    Console.WriteLine("Cant leave a room when not in a room!");
            }
        }

        public static void SpawnPlayer()
        {
            if (!SpawnedPlayer)
            {
                Command Spawn = new Command(client, 0, "SPAWN", $"{0}");
                Spawn.Format();
                Packet output = new Packet(client, Spawn.GetBytes(), DataType.Custom);
                output.Format();
                client.SendPacket(output);
            }
        }
        public static void CheckIfMaster()
        {
            bool placeholder = true;
            uint placeholder2 = uint.MaxValue;
            foreach (long id in PlayerList)
            {
                if (id < placeholder2)
                {
                    placeholder2 = Convert.ToUInt32(id);
                }
                if (id < client.ID && id != client.ID)
                {
                    placeholder = false;
                    placeholder2 = Convert.ToUInt32(id);
                }
            }
            IsMaster = placeholder;
            MasterID = placeholder2;
        }
        public static void Instantiate(int index)
        {
            Command spawn = new Command(client, 0, "SPAWN", index.ToString());
            spawn.Format();
            Packet output = new Packet(client, spawn.GetBytes());
            client.SendPacket(output);
        }

        public static void RequestPlayerList()
        {
            Command command = new Command(client, -1, "ServerCommand", "PLAYER_LIST");
            command.Format();
            Packet output = new Packet(client, command.GetBytes(), DataType.Custom);
            client.notSentPackets.TryAdd(output.PacketID, output);
        }
        async static Task SlowerUpdate()
        {
            if(debug.Log())
                Console.WriteLine("Started 'Slower Update'");
            while (true)
            {
                if (IsConnected && InRoom)
                {
                    RequestPlayerList();
                    await Task.Delay(2500);
                    CheckIfMaster();
                    await Task.Delay(2500);
                }
                else
                {
                    await Task.Delay(100);
                    continue;
                }
            }
        }
        async static Task SlowUpdate()
        {
            while (true)
            {
                await Task.Delay((int)(1f / (float)tickSpeed));
                client.managed = true;
                while (client.cache.TryDequeue(out Packet pack))
                {
                    pack.Format();
                    if (DataType.Custom == pack.dataType)
                    {
                        Command command = null;
                        try
                        {
                            command = new Command(pack, client);

                            string msg = Bytes.ToString(pack.Data);
                            string[] splitted = msg.Split(' ');

                            if (command.TargetID == client.ID || command.TargetID == 0)
                            {
                                if (splitted[0] == "JOINED_ROOM")
                                {
                                    if(debug.Log())
                                        Console.WriteLine("JOINED ROOM from 'JOINED_ROOM'");
                                    InRoom = true;
                                    ConnectedRoom = msg.Split('¬')[1];
                                }
                                else if (splitted[0] == "ServerCommand")
                                {
                                    string[] playerList = msg.Split('¬');
                                    playerList[0] = uint.MaxValue.ToString();
                                    string output = "";
                                    foreach (string s in playerList)
                                    {
                                        output += s + " ";
                                    }
                                    if (debug.Log())
                                        Console.WriteLine($"Recieved Player List from Server! Stats:\nmsg: {msg}\nsplitted: {output}");

                                    PlayerList.Clear();
                                    foreach (string player in playerList)
                                    {

                                        try
                                        {
                                            if (player != uint.MaxValue.ToString())
                                            {
                                                Console.WriteLine($"Added player: {player} to player list");
                                                PlayerList.Add(Convert.ToInt64(player));
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            if(debug.Error())
                                                Console.WriteLine($"ERROR from NETWORK MANAGER, 'ServerCommand': Failed Int Convert: '{player}' \nError:{e}");
                                        }
                                    }
                                }
                                else if (splitted[0] == "RPC")
                                {
                                    if (debug.Log())
                                        Console.WriteLine("RPC Recieved");
                                    Command RecievedRPC = new Command(pack, client);
                                    string sender = splitted[1];
                                    string m = RecievedRPC.data;
                                    string[] rpc = m.Split('¬');
                                    string name = rpc[1];
                                    RPCTarget target = (RPCTarget)Convert.ToInt32(rpc[2]);
                                    if (rpc.Length > 3)
                                    {
                                        if (debug.Log())
                                            Console.WriteLine("RPC length longer than 3");
                                        try
                                        {
                                            string json = rpc[3];
                                            object[] args = JsonConvert.DeserializeObject<object[]>(json);
                                            bool canRun = false;
                                            if (RPCTarget.Master == target && IsMaster)
                                                canRun = true;
                                            else
                                            {
                                                if (RPCTarget.All == target)
                                                    canRun = true;
                                                if (RPCTarget.AllByServer == target)
                                                    canRun = true;
                                                if (RPCTarget.Others == target)
                                                    canRun = true;
                                            }
                                            Console.WriteLine("RPC Parameters Decoded");
                                            if (canRun)
                                            {
                                                Console.WriteLine("Looking for elegable RPC runner");
                                                // Make a constant identity!
                                                if (Network.bindedRpcs.ContainsKey(name))
                                                {
                                                    Console.WriteLine($"RPC '{name}' ran");
                                                    Network.RunRPC(name, args);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                if (debug.Warning())
                                                    Console.WriteLine("RPC recieved. Not for me though.");
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            if (debug.Error())
                                                Console.WriteLine($"Error with running RPC: {e}");
                                        }
                                    }
                                }
                                else if (splitted[0] == "UPDATEVAR")
                                {
                                    Console.WriteLine("UPDATEVAR Recieved");
                                    string sender = splitted[1];
                                    //string target = splitted[2];
                                    long latestTick = -1;
                                    if (!long.TryParse(splitted[3], out latestTick))
                                    {
                                        if (debug.Error())
                                            Console.WriteLine($"Failed to parse tick. Raw string value was: '{splitted[3]}'");
                                    }

                                    string name = splitted[4];
                                    string value = splitted[5];

                                    serverTick = latestTick;
                                    // MAKE IDENTITY JUST 1 AS IN NETWORK VIEW
                                    if (NetworkView.NetworkVariable[name].tick < latestTick)
                                    {
                                        NetworkView.NetworkVariable[name].tick = latestTick;
                                        if (debug.Log())
                                            Console.WriteLine($"NETWORK MANAGER: Updated Variable: '{name}' to '{value}'");
                                        NetworkView.NetworkVariable[name].SetLocalValue(value);
                                        break;
                                    }


                                }
                                else
                                {
                                    if (debug.Warning())
                                        Console.WriteLine($"Unrecognised Custom Packet sent:\nMessage/Commmand: {msg}");
                                    string unknown = Bytes.ToString(pack.Data);
                                    if (unknown.Contains("JOINROOM"))
                                    {
                                        ConnectedRoom = unknown.Split('¬')[1];
                                        InRoom = true;
                                    }
                                }
                            }
                            else
                            {
                                if (debug.Warning())
                                    Console.WriteLine($"Ignoring recieved Command Reason:\nTargetID: {command.TargetID} MyID: {client.ID}");
                            }
                        }
                        catch (Exception e)
                        {
                            if (debug.Error())
                                Console.WriteLine($"Error when turning recieved command/string into a command format!\n\nError: {e}");

                            string unknown = Bytes.ToString(pack.Data);
                            if (unknown.Contains("JOINROOM"))
                            {
                                try
                                {
                                    if (debug.Log())
                                        Console.WriteLine("Room Joining Stage 1/3");
                                    if (Convert.ToInt64(unknown.Split('¬')[2]) == ID)
                                    {
                                        if (debug.Log())
                                            Console.WriteLine("Room Joining Stage 2/3");
                                        ConnectedRoom = unknown.Split('¬')[1];
                                        if (debug.Log())
                                            Console.WriteLine("Room Joining Stage 3/3 Complete");
                                        InRoom = true;
                                        if (client.ID != MasterID)
                                        {
                                            Command request = new Command(client, MasterID, "REQUEST", "");
                                            request.Format();
                                            Packet p = new Packet(client, request.GetBytes(), DataType.Custom);
                                            client.SendPacket(p);
                                        }
                                    }
                                }
                                catch (Exception b)
                                {
                                    if (debug.Error())
                                        Console.WriteLine($"Error in getting Room Data:\nError: {b}");
                                }

                            }
                        }
                        if (debug.Log())
                            Console.WriteLine($"Packet Recieved -> {pack.dataType}: {Bytes.ToString(pack.Data)}");
                        if (Bytes.ToString(pack.Data).Contains("REQUEST"))
                        {
                            string[] splitted = Bytes.ToString(pack.Data).Split(' ');


                            string SenderID = splitted[1];
                            // request late joiner data of what though?
                        }

                    }
                    else if (DataType.SetID == pack.dataType)
                    {
                        long newID = Bytes.ToUInt32(pack.Data);
                        if (newID != 0)
                        {
                            if (debug.Log())
                                Console.WriteLine($"Set Client ID: {newID}");
                            ID = newID;
                            client.ID = (uint)newID;
                        }
                        else
                        {
                            if (debug.Warning())
                                Console.WriteLine("SETID Refused: Not setting ID to 0");
                        }
                    }
                    else if (DataType.String == pack.dataType)
                    {
                        Console.WriteLine($"[Server] {Bytes.ToString(pack.Data)}");
                    }
                    else if (DataType.Ping == pack.dataType)
                    {
                        if (debug.Log())
                            Console.WriteLine("Sending Ping Back");
                        Packet pingBack = new Packet(client, Bytes.Get("Pingback"), DataType.Ping);
                        _ = client.client.Send(pingBack.GetBytes());
                    }
                }

            }
        }
        public static uint GetObjectID(uint senderID, uint objectID)
        {

            return (senderID << 16) | (objectID & 0xFFFF);
        }

    }
}