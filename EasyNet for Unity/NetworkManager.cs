using EasyNet;
using EasyNet.Behaviour;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Newtonsoft.Json;
using Unity.VisualScripting;

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
        public bool LoginOnAwake = false;



        public long serverTick;
        public int tickSpeed;
        /// <summary>
        /// Objects that can be spawned in by EasyNet
        /// </summary>
        public List<GameObject> NetworkObjects = new List<GameObject>();
        /// <summary>
        /// Objects that have been spawned in by EasyNet
        /// </summary>
        [Header("Debugging Lists")]
        public List<GameObject> NetworkObjectsSpawn = new List<GameObject>();

        // Used for Instantate("ObjectName")
        private Dictionary<string, GameObject> networkObjectsWithNames = new Dictionary<string, GameObject>();


        public List<long> PlayerList = new List<long>();

        private readonly Dictionary<string, Delegate> bindedRpcs = new Dictionary<string, Delegate>();

        /// <summary>
        /// Objects that are lerping, usually from NetworkTransform
        /// </summary>
        public List<LerpedObject> LerpingObjects = new List<LerpedObject>();

        /// <summary>
        /// Network Variables that have been created
        /// </summary>
        public Dictionary<string, NetworkVariableBase> NetworkVariable = new Dictionary<string, NetworkVariableBase>();
        public bool showNetVar = false;

        private bool SpawnedPlayer = false;


        void Awake()
        {
            if (port == 0)
                port = 8080;

            client = new Client(ip, port);
            client.managed = true;

            FillNetworkObjectsName();

            if (LoginOnAwake)
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
            if (client.ID != 0)
            {
                IsConnected = true;
            }
            if (showNetVar)
            {
                string str = "";
                foreach (var name in NetworkVariable.Keys)
                {
                    str += $"Name: {name} Value {GetVariable<int>("name")}\n";
                }
                Debug.Log("All Network Variables:\n" + str);
            }
            List<LerpedObject> finished = new List<LerpedObject>();
            foreach (LerpedObject obj in LerpingObjects)
            {
                if (!obj.done)
                    obj.Lerp();
                else
                    finished.Add(obj);
            }
            foreach (LerpedObject obj in finished)
            {
                LerpingObjects.Remove(obj);
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
        public T GetVariable<T>(string key)
        {
            if (NetworkVariable.ContainsKey(key))
                return ((NetworkVariable<T>)NetworkVariable[key]).Value;
            else
            {
                Debug.Log($"Cant find referenced Variable at '{key}'. Creating new one.");
                NetworkVariable.Add(key, new NetworkVariable<T>(this, key));
                return ((NetworkVariable<T>)NetworkVariable[key]).Value;
            }
        }
        public void RegisterVariable(NetworkVariableBase netVar)
        {
            NetworkVariable[netVar.name] = netVar;
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


        /// <summary>
        /// Calls an RPC
        /// </summary>
        /// <param name="RPC_name">Name of RPC you want to call (name set in BindRPC).</param>
        /// <param name="target">Who you want to send RPC to.</param>
        /// <param name="args">Parameters for RPC if needed. They have to be serializable.</param>
        public void RPC(string rpcName, RPCTarget target, params object[] args)
        {
            string jsonParams = JsonConvert.SerializeObject(args);
            Command CallRPC = new Command(client, 0, "RPC", $"¬{rpcName}¬{(int)target}¬{jsonParams}");
            CallRPC.Format();
            Packet output = new Packet(client, CallRPC.GetBytes(), DataType.Custom);
            client.SendPacket(output);

            if (RPCTarget.All == target)
                RunRPC(rpcName, args);
        }

        /// <summary>
        /// When binding an RPC, the method must be return type void.
        /// </summary>
        /// <param name="RPC_name">Set the name of the RPC.</param>
        /// <param name="Method">The method you want to be able to call by the RPC name.</param>
        public void BindRPC(string rpcName, Delegate method)
        {
            if (rpcName == null || rpcName == " ")
            {
                Debug.LogError("Actual RPC name needed!");
                return;
            }
            if (!bindedRpcs.ContainsKey(rpcName))
            {
                bindedRpcs[rpcName] = method;
            }
            else
            {
                Debug.LogError($"RPC already exists with name {rpcName}!");
            }
        }
        public void BindRPC(string rpcName, Action m) => BindRPC(rpcName, (Delegate)m);
        public void BindRPC<T>(string rpcName, Action<T> m) => BindRPC(rpcName, (Delegate)m);
        public void BindRPC<T1, T2>(string rpcName, Action<T1, T2> m) => BindRPC(rpcName, (Delegate)m);
        public void BindRPC<T1, T2, T3>(string rpcName, Action<T1, T2, T3> m) => BindRPC(rpcName, (Delegate)m);
        public void BindRPC<T1, T2, T3, T4>(string rpcName, Action<T1, T2, T3, T4> m) => BindRPC(rpcName, (Delegate)m);
        public void BindRPC<T1, T2, T3, T4, T5>(string rpcName, System.Action<T1, T2, T3, T4, T5> m) => BindRPC(rpcName, (Delegate)m);

        private void RunRPC(string rpcName, params object[] args)
        {
            if (bindedRpcs.TryGetValue(rpcName, out Delegate method))
            {
                try
                {
                    var methodParams = method.Method.GetParameters();
                    object[] convertedArgs = new object[args.Length];

                    for (int i = 0; i < args.Length; i++)
                    {
                        convertedArgs[i] = System.Convert.ChangeType(args[i], methodParams[i].ParameterType);
                    }
                    method.DynamicInvoke(convertedArgs);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error trying to execute RPC: '{rpcName}'. \nError: {e}");
                }
            }
            else
            {
                Debug.LogWarning($"RPC Command '{rpcName}' recieved but no RPC was bound, use BindRPC('{rpcName}', Delegate) to do so");
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

                                foreach (GameObject OBJ in NetworkObjectsSpawn)
                                {
                                    var obj = OBJ.GetComponent<NetworkBehaviour>();
                                    if (obj.ID == targetID && !obj.isPlayers)
                                    {
                                        bool canAdd = true;
                                        foreach (LerpedObject l in LerpingObjects)
                                        {
                                            if (l.obj == OBJ)
                                            {
                                                canAdd = false;
                                                l.target = newPos;
                                                break;
                                            }
                                        }
                                        if (canAdd)
                                            LerpingObjects.Add(new LerpedObject(OBJ, newPos));
                                    }
                                }
                            }
                            else if (splitted[0] == "R")
                            {
                                int senderID = Convert.ToInt32(splitted[1]);
                                int targetID = Convert.ToInt32(splitted[2]);
                                Quaternion r = new Quaternion(Convert.ToSingle(splitted[3]), Convert.ToSingle(splitted[4]), Convert.ToSingle(splitted[5]), Convert.ToSingle(splitted[6]));
                                Debug.Log($"{senderID} to {targetID}: {splitted[0]} {r.x}, {r.y}, {r.z}, {r.w}");
                                NetworkBehaviour[] networked = FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None);
                                foreach (GameObject OBJ in NetworkObjectsSpawn)
                                {
                                    var obj = OBJ.GetComponent<NetworkBehaviour>();
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
                                    var obj = OBJ.GetComponent<NetworkBehaviour>();
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
                                    if (client.ID == MasterID)  // original, first player
                                    {
                                        Debug.Log($"Sending out all Object Information to Client that requested.");
                                        for (int i = 0; i < NetworkObjectsSpawn.Count; i++)
                                        {
                                            for (int x = 0; x < NetworkObjects.Count; x++)
                                            {
                                                if (NetworkObjectsSpawn[i] == NetworkObjects[x])
                                                {
                                                    Command Spawn = new Command(client, Convert.ToInt64(SenderID), "SPAWN", $"{x}");
                                                    Spawn.Format();
                                                    Packet output = new Packet(client, Spawn.GetBytes(), DataType.Custom);
                                                    client.SendPacket(output);
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (splitted[0] == "JOINED_ROOM")
                                {
                                    InRoom = true;
                                    ConnectedRoom = msg.Split('¬')[1];
                                }
                                else if (splitted[0] == "KICK")
                                {
                                    uint idToKick = Convert.ToUInt32(splitted[splitted.Length - 1]);
                                    foreach (GameObject go in NetworkObjectsSpawn)
                                    {
                                        NetworkBehaviour identity = go.GetComponent<NetworkBehaviour>();
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
                                        go.GetComponent<NetworkBehaviour>().ID = newID;
                                    }
                                }
                                else if (splitted[0] == "SPAWN")
                                {
                                    string sender = splitted[1];
                                    string obj = splitted[2];
                                    Debug.Log($"{sender}: Spawn Object [{obj}]");

                                    GameObject go = Instantiate(NetworkObjects[Convert.ToInt32(obj)]);
                                    NetworkBehaviour identity = go.GetComponent<NetworkBehaviour>();
                                    NetworkObjectsSpawn.Add(go);

                                    identity.ID = (pack.PacketID * 7);
                                    identity.OwnerID = pack.SenderID;

                                    if (client.ID == Convert.ToInt32(sender))
                                    {

                                        identity.isPlayers = true;
                                        identity.root = this;
                                        Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: True");
                                    }
                                    else
                                    {
                                        Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: False");
                                    }
                                }
                                else if (splitted[0] == "ServerCommand")
                                {
                                    string[] playerList = msg.Split('-');
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
                                        try
                                        {
                                            string json = rpc[3];
                                            object[] args = JsonConvert.DeserializeObject<object[]>(json);
                                            bool canRun = false;
                                            if (RPCTarget.Master == target && IsMaster)
                                                canRun = true;
                                            if (Convert.ToInt64(sender) == client.ID)
                                            {
                                                if (RPCTarget.AllByServer == target)
                                                    canRun = true;
                                            }
                                            else
                                            {
                                                if (RPCTarget.All == target)
                                                    canRun = true;
                                                if (RPCTarget.Others == target)
                                                    canRun = true;
                                            }
                                            if (canRun)
                                                RunRPC(name, args);
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogError($"Error with running RPC: {e}");
                                        }
                                    }
                                }
                                else if (splitted[0] == "UPDATEVAR")
                                {
                                    Debug.Log("UPDATEVAR Recieved");
                                    string sender = splitted[1];
                                    //string target = splitted[2];
                                    long latestTick = -1;
                                    if (!long.TryParse(splitted[3], out latestTick))
                                    {
                                        Debug.LogError($"Failed to parse tick. Raw string value was: '{splitted[3]}'");
                                    }

                                    string name = splitted[4];
                                    string value = splitted[5];
                                    if ((latestTick < 0 && serverTick > 0) || (latestTick > serverTick))
                                    {
                                        serverTick = latestTick;
                                        if (!NetworkVariable.ContainsKey(name))
                                        {
                                            Debug.Log($"Network Variable: '{name}' does not exist. Creating one now");
                                            NetworkVariable.Add(name, new NetworkVariable<int>(this, name));
                                        }
                                        if (splitted.Length > 6 && NetworkVariable[name].GetType() == typeof(string))
                                        {
                                            for (int i = 6; i < splitted.Length; i++)
                                            {
                                                value += splitted[i] + " ";
                                            }
                                        }

                                        NetworkVariable[name].SetLocalValue(value);

                                        Debug.Log($"NETWORK MANAGER: Updated Variable: '{name}' to '{value}'");
                                    }
                                }
                                else
                                {
                                    Debug.Log($"Unrecognised Custom Packet sent:\nMessage/Commmand: {msg}");
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
                                Debug.Log($"Ignoring recieved Command Reason:\nTargetID: {command.TargetID} MyID: {client.ID}");

                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Error when turning recieved command/string into a command format!\n\nError: {e}");

                            Debug.Log($"Unrecognised Custom Packet sent:\nMessage/Commmand: {Bytes.ToString(pack.Data)}");
                            string unknown = Bytes.ToString(pack.Data);
                            if (unknown.Contains("JOINED_ROOM"))
                            {
                                try
                                {
                                    if (Convert.ToInt64(unknown.Split('¬')[2]) == ID)
                                    {
                                        ConnectedRoom = unknown.Split('¬')[1];
                                        InRoom = true;
                                    }
                                }
                                catch (Exception b)
                                {
                                    Debug.LogError(b);
                                }

                            }
                        }
                        Debug.Log($"Packet Recieved -> {pack.dataType}: {Bytes.ToString(pack.Data)}");

                    }
                    else if (DataType.SetID == pack.dataType)
                    {
                        long newID = Bytes.ToUInt32(pack.Data);
                        if (newID != 0)
                        {
                            Debug.Log($"Set Client ID: {newID}");
                            ID = newID;
                            client.ID = (uint)newID;
                        }
                        else
                        {
                            Debug.LogWarning("SETID Refused: Not setting ID to 0");
                        }
                    }
                    else if (DataType.String == pack.dataType)
                    {
                        Debug.Log($"[Server] {Bytes.ToString(pack.Data)}");
                    }
                    else if (DataType.Ping == pack.dataType)
                    {
                        Debug.Log("Sending Ping Back");
                        Packet pingBack = new Packet(client, Bytes.Get("Pingback"), DataType.Ping);
                        _ = client.client.Send(pingBack.GetBytes());
                    }
                }

            }
        }

    }
}
