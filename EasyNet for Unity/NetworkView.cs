using EasyNet.Manager;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace EasyNet.View
{
    class NetworkView : MonoBehaviour
    {
        public NetworkManager Network;
        /// <summary>
        /// The Unique ID of the network object
        /// </summary>
        public long ID;
        /// <summary>
        /// The Client ID of the person who Instantiated the object (who owns it)
        /// </summary>
        public uint OwnerID;
        /// <summary>
        /// The Object Reference from NetworkObjects list in NetworkManager
        /// </summary>
        [HideInInspector]
        public int spawnID = -1;

        /// <summary>
        /// Is Connected to the EasyNet Unity Server
        /// </summary>
        public bool Connected;
        /// <summary>
        /// Is owned by the client
        /// </summary>
        public bool IsPlayers = false;
        public bool NetworkSync = true;

        public readonly Dictionary<string, Delegate> bindedRpcs = new Dictionary<string, Delegate>();
        public Dictionary<string, NetworkVariableBase> NetworkVariable = new Dictionary<string, NetworkVariableBase>();


        [Header("Debug")]
        public bool showEndpoint = false;
        public bool doAge = true;
        public int age;
        public int netVarDisplay=-1;
        private TextMeshProUGUI ageDisplay;


        private void Awake()
        {
            ageDisplay = GameObject.Find("Age").GetComponent<TextMeshProUGUI>();
            Network = FindFirstObjectByType<NetworkManager>();
            try
            {
                if (doAge)
                    RegisterVariable(new NetworkVariable<int>(Network, "age"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Already made varaible, age:\nError {e}");
            }
            if (IsPlayers)
            {

                Network.NetworkObjectsSpawn.Add(gameObject);        // Maybe!? or should networkedObjs be for foreign objects?
            }
            if (doAge)
                StartCoroutine(Age());
        }


        private void Update()
        {
            if (!IsPlayers)
                return;
            if (!NetworkSync)
                spawnID = -1;   
            if (doAge)
            {
                netVarDisplay = GetVariable<int>("age");
            }
            if (showEndpoint)
            {
                showEndpoint = false;
                Debug.LogWarning($"Endpoint: {(IPEndPoint)Network.client.client._socket.LocalEndPoint}");
            }
            if (Network.client != null && IsPlayers)
            {
                Connected = Network.client.isConnected();


            }
        }
        public T GetVariable<T>(string key)
        {
            if (NetworkVariable.ContainsKey(key))
                return ((NetworkVariable<T>)NetworkVariable[key]).Value;
            else
            {
                Debug.Log($"Cant find referenced Variable at '{key}'. Creating new one.");
                NetworkVariable.Add(key, new NetworkVariable<T>(Network, key));
                return ((NetworkVariable<T>)NetworkVariable[key]).Value;
            }
        }
        public void RegisterVariable(NetworkVariableBase netVar)
        {
            NetworkVariable[netVar.name] = netVar;
        }
        IEnumerator Age()
        {
            yield return new WaitForSeconds(2f);

            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (IsPlayers)
                {
                    age++;
                    NetworkVariable["age"].SetValue(age.ToString());
                    Debug.Log("NETWORKVIEW: Set Network Var 'age' to : " + age);
                }
                else
                {
                    age = GetVariable<int>("age");
                }
                ageDisplay.text = GetVariable<int>("age").ToString();

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
            Command CallRPC = new Command(Network.client, 0, "RPC", $"¬{rpcName}¬{(int)target}¬{jsonParams}");
            CallRPC.Format();
            Packet output = new Packet(Network.client, CallRPC.GetBytes(), DataType.Custom);
            Network.client.SendPacket(output);

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

        public void RunRPC(string rpcName, params object[] args)
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
    }
}
