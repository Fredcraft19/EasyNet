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
using Unity.VisualScripting;
using UnityEngine;
using NetworkView = EasyNet.View.NetworkView;
namespace EasyNet.Manager
{
    public class NetworkManager : MonoBehaviour
    {
        private IPAddress ip = IPAddress.Loopback;
        private int port = 8080;
        public Client client;

        [Header("Client Stats")]
        public long ID;
        /// <summary>
        /// If the client is connected to the server
        /// </summary>
        [Serialize]
        public bool IsConnected;
        /// <summary>
        /// If the client is connected to a room
        /// </summary>
        [Serialize]

        public bool InRoom;
        /// <summary>
        /// The room name that the client is connected to. Empty if not in room
        /// </summary>
        [Serialize]
        public string ConnectedRoom;
        /// <summary>
        /// If client is the master client
        /// </summary>
        public bool IsMaster = false;
        /// <summary>
        /// The ID of the master client
        /// </summary>
        public uint MasterID = 0;

        [Header("Settings")]
        public bool connectOnAwake = false;


        [HideInInspector]
        public long serverTick;
        public int tickSpeed;
        /// <summary>
        /// Objects that can be spawned in by EasyNet
        /// </summary>
        public List<GameObject> NetworkObjects = new List<GameObject>();
        /// <summary>
        /// Objects that have been spawned in by EasyNet
        /// </summary>
        [Header("Debug Choice")]
        public DebugMode debugMode = DebugMode.None;

        //[HideInInspector]
        public List<GameObject> NetworkObjectsSpawn = new List<GameObject>();

        // Used for Instantate("ObjectName")
        private Dictionary<string, GameObject> networkObjectsWithNames = new Dictionary<string, GameObject>();


        private List<long> PlayerList = new List<long>();


        public float lerpSpeed = 0.05f;
        /// <summary>
        /// Objects that are lerping, usually from NetworkTransform
        /// </summary>
        [Serialize]
        public List<LerpedObject> LerpingObjects = new List<LerpedObject>();

        private bool SpawnedPlayer = false;


        void Awake()
        {
            if (port == 0)
                port = 8080;

            client = new Client(ip, port);
            client.managed = true;

            FillNetworkObjectsName();

            if (connectOnAwake)
            {
                if (ID != 2 || ID != 0)
                {
                    Command objRequest = new Command(client, 2, "REQUEST", "");
                    objRequest.Format();
                    Packet output = new Packet(client, objRequest.GetBytes(), DataType.Custom);
                    client.SendPacket(output);
                }
            }

        }
        void Start()
        {
            client.UpdateRate = 50f;
            StartCoroutine(SlowUpdate());
            StartCoroutine(SlowerUpdate());
        }
        void Update()
        {
            debug.debugmode = debugMode;
            if (client.ID != 0)
            {
                IsConnected = true;
            }
            for (int i = LerpingObjects.Count - 1; i >= 0; i--)
            {
                LerpedObject obj = LerpingObjects[i];
                obj.smoothTime = lerpSpeed * 0.05f;

                if (!obj.done)
                {
                    obj.Lerp();
                }
                else
                {
                    LerpingObjects.RemoveAt(i);
                }
            }
        }

        public void Connect()
        {
            Packet ping = new Packet(client, Bytes.Get(0));
            client.SendPacket(ping);
        }
        public void JoinRoom(string roomName)
        {
            Packet output = new Packet(client, Bytes.Get($"JOINROOM¬{roomName}"), DataType.Custom);
            client.SendPacket(output);
        }
        public void LeaveRoom()
        {
            if (InRoom)
            {
                Packet leaveRequest = new Packet(client, Bytes.Get("¬LEAVEROOM"));
                client.SendPacket(leaveRequest);
            }
            else
            {
                Debug.LogWarning("Cant leave a room when nont in a room!");
            }
        }

        public void SpawnPlayer()
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
        public void CheckIfMaster()
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
        public void Instantiate(int index)
        {
            Command spawn = new Command(client, 0, "SPAWN", index.ToString());
            spawn.Format();
            Packet output = new Packet(client, spawn.GetBytes());
            client.SendPacket(output);
        }
        public void Instantiate(string name)
        {
            int index = NetworkObjects.IndexOf(networkObjectsWithNames[name]);
            Command spawn = new Command(client, 0, "SPAWN", index.ToString());
            spawn.Format();
            Packet output = new Packet(client, spawn.GetBytes());
            client.SendPacket(output);
        }
        private void FillNetworkObjectsName()
        {
            networkObjectsWithNames.Clear();
            for (int i = 0; i < NetworkObjects.Count; i++)
            {
                networkObjectsWithNames[NetworkObjects[i].name] = NetworkObjects[i];
            }
        }

        public void RequestPlayerList()
        {
            Command command = new Command(client, -1, "ServerCommand", "PLAYER_LIST");
            command.Format();
            Packet output = new Packet(client, command.GetBytes(), DataType.Custom);
            client.notSentPackets.TryAdd(output.PacketID, output);
        }
        IEnumerator SlowerUpdate()
        {
            Debug.Log("Started 'Slower Update'");
            while (true)
            {
                if (IsConnected && InRoom)
                {
                    RequestPlayerList();
                    yield return new WaitForSeconds(2.5f);
                    CheckIfMaster();
                    yield return new WaitForSeconds(2.5f);
                }
                else
                {
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }
            }
        }
        IEnumerator SlowUpdate()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f / (float)tickSpeed);
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

                            if (splitted[0] == "P")
                            {
                                int senderID = Convert.ToInt32(splitted[1]);
                                long targetID = Convert.ToInt64(splitted[2]);
                                UnityEngine.Vector3 newPos = new UnityEngine.Vector3(Convert.ToSingle(splitted[3]), Convert.ToSingle(splitted[4]), Convert.ToSingle(splitted[5]));

                                Debug.Log($"{senderID} to {targetID}: {splitted[0]} {newPos.x}, {newPos.y}, {newPos.z}");

                                Debug.Log("FIND OBJ TO LERP");
                                foreach (GameObject OBJ in NetworkObjectsSpawn)
                                {
                                    var obj = OBJ.GetComponent<NetworkView>();
                                    if (obj.ID == targetID && !obj.IsPlayers)
                                    {
                                        bool canAdd = true;
                                        foreach (LerpedObject l in LerpingObjects)
                                        {
                                            if (l.obj == OBJ)
                                            {
                                                canAdd = false;
                                                l.target = newPos;
                                                Debug.Log("FIND OBJ TO LERP");
                                                break;
                                            }
                                        }
                                        if (canAdd)
                                        {
                                            Debug.Log("FOUND OBJ. LERPING!");

                                            LerpingObjects.Add(new LerpedObject(OBJ, newPos));
                                        }
                                    }
                                }
                                Debug.Log("LOOP FINISHED, MOVED?");


                            }
                            else if (splitted[0] == "R")
                            {
                                int senderID = Convert.ToInt32(splitted[1]);
                                int targetID = Convert.ToInt32(splitted[2]);
                                Quaternion r = new Quaternion(Convert.ToSingle(splitted[3]), Convert.ToSingle(splitted[4]), Convert.ToSingle(splitted[5]), Convert.ToSingle(splitted[6]));
                                Debug.Log($"{senderID} to {targetID}: {splitted[0]} {r.x}, {r.y}, {r.z}, {r.w}");
                                NetworkView[] networked = FindObjectsByType<NetworkView>(FindObjectsSortMode.None);
                                foreach (GameObject OBJ in NetworkObjectsSpawn)
                                {
                                    var obj = OBJ.GetComponent<NetworkView>();
                                    if (obj.ID == targetID)
                                    {
                                        obj.transform.rotation = r;
                                    }
                                }
                            }
                            else if (splitted[0] == "S")
                            {
                                int senderID = Convert.ToInt32(splitted[1]);
                                int targetID = Convert.ToInt32(splitted[2]);
                                UnityEngine.Vector3 s = new UnityEngine.Vector3(Convert.ToSingle(splitted[3]), Convert.ToSingle(splitted[4]), Convert.ToSingle(splitted[5]));
                                Debug.Log($"{senderID} to {targetID}: {splitted[0]} {s.x}, {s.y}, {s.z}");
                                foreach (GameObject OBJ in NetworkObjectsSpawn)
                                {
                                    var obj = OBJ.GetComponent<NetworkView>();
                                    if (obj.ID == targetID)
                                    {
                                        obj.transform.localScale = s;
                                    }
                                }
                            }
                            if (command.TargetID == client.ID || command.TargetID == 0)
                            {
                                if (splitted[0] == "REQUEST")
                                {
                                    string SenderID = splitted[1];
                                    Debug.LogError($"Request recieved: Is Master: {IsMaster}");

                                    Debug.LogError($"Sending out all Object Information to Client that requested.");

                                    Debug.LogError($"DEBUG: NetworkObjectsSpawn Count: {NetworkObjectsSpawn.Count}");
                                    Debug.LogError($"DEBUG: NetworkObjects (Prefabs) Count: {NetworkObjects.Count}");
                                    foreach (GameObject spawnReq in NetworkObjectsSpawn)
                                    {
                                        NetworkView identity = spawnReq.GetComponent<NetworkView>();
                                        int prefabIndex = identity.spawnID;
                                        if (!identity.NetworkSync)
                                        {
                                            continue;
                                        }
                                        else if (prefabIndex > -1 && identity.IsPlayers)
                                        {
                                            Debug.LogWarning($"Sending SPAWN OBJ Index: {prefabIndex} to Client: {SenderID}");

                                            Command spawnCmd = new Command(client, Convert.ToInt64(SenderID), "SPAWN", prefabIndex.ToString());
                                            spawnCmd.Format();

                                            Packet output = new Packet(client, spawnCmd.GetBytes(), DataType.Custom);
                                            client.SendPacket(output);
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"Object {spawnReq.name} not found in NetworkObjects list!");
                                        }
                                    }
                                }
                                else if (splitted[0] == "JOINED_ROOM")
                                {
                                    Debug.LogError("JOINED ROOM from 'JOINED_ROOM'");
                                    InRoom = true;
                                    ConnectedRoom = msg.Split('¬')[1];
                                }
                                else if (splitted[0] == "KICK")
                                {
                                    uint idToKick = Convert.ToUInt32(splitted[splitted.Length - 1]);
                                    foreach (GameObject go in NetworkObjectsSpawn)
                                    {
                                        NetworkView identity = go.GetComponent<NetworkView>();
                                        if (identity.OwnerID == idToKick)
                                            Destroy(go);
                                    }
                                }
                                else if (splitted[0] == "OBJECTS")
                                {
                                    string SenderID = splitted[1];
                                    long ID = pack.PacketID;
                                    for (int i = 3; i < splitted.Length; i++)
                                    {
                                        long newID = ID + (i * 7);
                                        string[] parts = splitted[i].Split('-');
                                        Debug.Log($"OBJECTS: Parts: {parts}");
                                        string ObjID = parts[0];
                                        string ObjIndex = parts[1];
                                        GameObject go = Instantiate(NetworkObjects[Convert.ToInt32(ObjIndex)]);
                                        go.GetComponent<NetworkView>().ID = newID;
                                    }
                                }
                                else if (splitted[0] == "SPAWN")
                                {
                                    string sender = splitted[1];
                                    string obj = splitted[2];
                                    if (Convert.ToUInt32(obj) == ID)
                                        obj = splitted[3];
                                    Debug.Log($"{sender}: Spawn Object [{obj}]");

                                    GameObject go = Instantiate(NetworkObjects[Convert.ToInt32(obj)]);
                                    NetworkView identity = go.GetComponent<NetworkView>();
                                    NetworkObjectsSpawn.Add(go);

                                    identity.spawnID = Convert.ToInt32(obj);
                                    identity.ID = GetObjectID(Convert.ToUInt32(sender), Convert.ToUInt32(obj));
                                    identity.OwnerID = pack.SenderID;

                                    if (client.ID == Convert.ToInt32(sender))
                                    {

                                        identity.IsPlayers = true;
                                        identity.Network = this;
                                        Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: True");
                                    }
                                    else
                                    {
                                        Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: False");
                                    }
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
                                    Debug.Log($"Recieved Player List from Server! Stats:\nmsg: {msg}\nsplitted: {output}");

                                    PlayerList.Clear();
                                    foreach (string player in playerList)
                                    {

                                        try
                                        {
                                            if (player != uint.MaxValue.ToString())
                                            {
                                                Debug.Log($"Added player: {player} to player list");
                                                PlayerList.Add(Convert.ToInt64(player));
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogWarning($"ERROR from NETWORK MANAGER, 'ServerCommand': Failed Int Convert: '{player}' \nError:{e}");
                                        }
                                    }
                                }
                                else if (splitted[0] == "RPC")
                                {
                                    Debug.Log("RPC Recieved");
                                    Command RecievedRPC = new Command(pack, client);
                                    string sender = splitted[1];
                                    string m = RecievedRPC.data;
                                    string[] rpc = m.Split('¬');
                                    string name = rpc[1];
                                    RPCTarget target = (RPCTarget)Convert.ToInt32(rpc[2]);
                                    if (rpc.Length > 3)
                                    {
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
                                                if (identity.bindedRpcs.ContainsKey(name))
                                                {
                                                    Console.WriteLine($"RPC '{name}' ran");
                                                    identity.RunRPC(name, args);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("RPC recieved. Not for me though.");
                                            }
                                        }
                                        catch (Exception e)
                                        {
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
                                        Console.WriteLine($"Failed to parse tick. Raw string value was: '{splitted[3]}'");
                                    }

                                    string name = splitted[4];
                                    string value = splitted[5];

                                    serverTick = latestTick;
                                    // MAKE IDENTITY JUST 1 AS IN NETWORK VIEW
                                    if (identity.NetworkVariable[name].tick < latestTick)
                                    {
                                        identity.NetworkVariable[name].tick = latestTick;
                                        Debug.Log($"NETWORK MANAGER: Updated Variable: '{name}' to '{value}'");
                                        identity.NetworkVariable[name].SetLocalValue(value);
                                        break;
                                    }


                                }
                                else
                                {
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
                                Console.WriteLine($"Ignoring recieved Command Reason:\nTargetID: {command.TargetID} MyID: {client.ID}");


                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error when turning recieved command/string into a command format!\n\nError: {e}");

                            string unknown = Bytes.ToString(pack.Data);
                            if (unknown.Contains("JOINROOM"))
                            {
                                try
                                {
                                    Console.WriteLine("Room Joining Stage 1/3");
                                    if (Convert.ToInt64(unknown.Split('¬')[2]) == ID)
                                    {
                                        DeConsole.WriteLinebug.Log("Room Joining Stage 2/3");
                                        ConnectedRoom = unknown.Split('¬')[1];
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
                                    Console.WriteLine($"Error in getting Room Data:\nError: {b}");
                                }

                            }
                        }
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
                            Console.WriteLine($"Set Client ID: {newID}");
                            ID = newID;
                            client.ID = (uint)newID;
                        }
                        else
                        {
                            Console.WriteLine("SETID Refused: Not setting ID to 0");
                        }
                    }
                    else if (DataType.String == pack.dataType)
                    {
                        Console.WriteLine($"[Server] {Bytes.ToString(pack.Data)}");
                    }
                    else if (DataType.Ping == pack.dataType)
                    {
                        Console.WriteLine("Sending Ping Back");
                        Packet pingBack = new Packet(client, Bytes.Get("Pingback"), DataType.Ping);
                        _ = client.client.Send(pingBack.GetBytes());
                    }
                }

            }
        }
        public uint GetObjectID(uint senderID, uint objectID)
        {

            return (senderID << 16) | (objectID & 0xFFFF);
        }

    }
}
