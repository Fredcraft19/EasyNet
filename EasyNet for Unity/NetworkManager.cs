using EasyNet;
using EasyNet.Behaviour;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.VisualScripting;
using UnityEngine;

namespace EasyNet.Manager
{
    class NetworkManager : MonoBehaviour
    {
        public IPAddress ip = IPAddress.Loopback;
        public int port;
        public Client client;
        public long ID;

        public long serverTick;
        public int tickSpeed;
        public List<GameObject> NetworkObjects = new List<GameObject>();
        public List<GameObject> NetworkObjectsSpawn = new List<GameObject>();

        public List<LerpedObject> LerpingObjects = new List<LerpedObject>();

        [Serialize]
        public Dictionary<string, NetworkVariableBase> NetworkVariable = new Dictionary<string, NetworkVariableBase>();
        public bool showNetVar=false;

        private bool SpawnedPlayer = false;

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

            void Awake()
            {
                if (port == 0)
                    port = 8080;

                client = new Client(ip, port);
                client.managed = true;

                if(ID != 2 || ID != 0)
            {
                Command objRequest = new Command(client, 2, "REQUEST", "");
                objRequest.Format();
                Packet output = new Packet(client, objRequest.GetBytes(), DataType.Custom);
                client.SendPacket(output);
            }
                

            
            }
        void Start()
        {
            client.UpdateRate = 100f;
            StartCoroutine(SlowUpdate());
        }
        void Update()
        {
            if (showNetVar)
            {
                string str="";
                foreach(var name in NetworkVariable.Keys)
                {
                    str += $"Name: {name} Value {GetVariable<int>("name")}\n";
                }
                Debug.Log("All Network Variables:\n"+str);
            }
            List<LerpedObject> finished = new List<LerpedObject>();
            foreach(LerpedObject obj in LerpingObjects)
            {
                if (!obj.done)
                    obj.Lerp();
                else
                    finished.Add(obj);
            }
            foreach(LerpedObject obj in finished)
            {
                LerpingObjects.Remove(obj);
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
                    Debug.Log("Decoding Packet: " + pack.dataType);
                    if (DataType.Custom == pack.dataType)
                    {
                        Debug.Log($"-> {Bytes.ToString(pack.Data)}");
                        string msg = Bytes.ToString(pack.Data);
                        string[] splitted = msg.Split(' ');

                        if (splitted[0] == "REQUEST")
                        {
                            string SenderID = splitted[1];
                            if (client.ID == 2)  // original, first player
                            {
                                Debug.Log($"Sending out all Object Information to Client that requested.");
                                string Objects = "";
                                for (int i = 0; i < NetworkObjectsSpawn.Count; i++)
                                {
                                    string index = "-1";
                                    for (int x = 0; x < NetworkObjects.Count; x++)
                                    {
                                        if (NetworkObjectsSpawn[i] == NetworkObjects[x])
                                        {
                                            index = $"{x}";
                                        }
                                    }
                                    Objects += $"{i}-{index}" + " ";
                                }
                                Command CurrentObjects = new Command(client, Convert.ToInt32(SenderID), "OBJECTS", Objects);
                                CurrentObjects.Format();
                                Packet output = new Packet(client, CurrentObjects.GetBytes(), DataType.Custom);
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
                            NetworkObjectsSpawn.Add(go);
                            yield return new WaitForSeconds(1f);

                            go.GetComponent<NetworkBehaviour>().ID = (pack.PacketID * 7);

                            if (client.ID == Convert.ToInt32(sender))
                            {
                                NetworkBehaviour net = go.GetComponent<NetworkBehaviour>();
                                net.isPlayers = true;
                                net.root = this;
                                Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: True");
                            }
                            else
                            {
                                Debug.Log($"OBJ Spawned:\nID: {(pack.PacketID * 7)}\nIsPlayers: False");
                            }
                        }
                        else if (splitted[0] == "P")
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
                                    foreach(LerpedObject l in LerpingObjects)
                                    {
                                        if(l.obj == OBJ)
                                        {
                                            canAdd = false;
                                            l.target = newPos;
                                            break;
                                        }
                                    }
                                    if(canAdd)
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
                        else if (splitted[0] == "UPDATEVAR")
                        {
                            Debug.Log("UPDATEVAR Recieved");
                            string sender =splitted[1];
                            string target = splitted[2];
                            long latestTick = Convert.ToInt64(splitted[3]);
                            string name = splitted[4];
                            string value = splitted[5];
                            if((latestTick < 0 && serverTick > 0) || (latestTick > serverTick))
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

                                NetworkVariable[name].SetValue(value);

                                Debug.Log($"Updated Variable: '{name}' to '{value}'");
                            }                            
                        }
                        else
                        {
                            Debug.Log($"Unrecognised Custom Packet sent:\nMessage/Commmand: {msg}");
                        }
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
