using EasyNet.Manager;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using EasyNet_Debugging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
namespace EasyNet.View
{
    public static class Network
    {
        /// <summary>
        /// The Unique ID of the network object
        /// </summary>
        public static long ID;
        /// <summary>
        /// The Client ID of the person who Instantiated the object (who owns it)
        /// </summary>
        public static uint OwnerID;
        /// <summary>
        /// The Object Reference from NetworkObjects list in NetworkManager
        /// </summary>
        public static int spawnID = -1;

        /// <summary>
        /// Is Connected to the EasyNet Unity Server
        /// </summary>
        public static bool Connected;
        /// <summary>
        /// Is owned by the client
        /// </summary>
        public static bool IsPlayers = false;
        public static bool NetworkSync = true;

        public static Dictionary<string, Delegate> bindedRpcs = new Dictionary<string, Delegate>();
        public static Dictionary<string, NetworkVariableBase> NetworkVariable = new Dictionary<string, NetworkVariableBase>();
        public static void Initialize()
        {
            Loop();
        }
        async static Task Loop()
        {
            while (true)
            {
                await Task.Delay(100);
                Update();
            }
        }

        private static void Update()
        {
            if (!IsPlayers)
                return;
            if (!NetworkSync)
                spawnID = -1;
            if (NetworkManager.client != null && IsPlayers)
            {
                Connected = NetworkManager.client.isConnected();
            }
        }
        public static T GetVariable<T>(string VariableName)
        {
            if (NetworkVariable.ContainsKey(VariableName))
                return ((NetworkVariable<T>)NetworkVariable[VariableName]).Value;
            else
            {
                if(debug.Warning())
                    Console.WriteLine($"Cant find referenced Variable at '{VariableName}'. Creating new one.");
                NetworkVariable.Add(VariableName, new NetworkVariable<T>(VariableName));
                return ((NetworkVariable<T>)NetworkVariable[VariableName]).Value;
            }
        }
        public static void RegisterVariable<T>(string VariableName)
        {
            NetworkVariable<T> var = new NetworkVariable<T>(VariableName);
            NetworkVariable[VariableName] = var;
        }
        public static void RegisterVariable<T>(string VariableName, T value)
        {
            RegisterVariable<T>(VariableName);
            NetworkVariable[VariableName].SetValue(value.ToString());
        }
        public static void SetVariable<T>(string VariableName, T value)
        {
            NetworkVariable[VariableName].SetValue(value.ToString());
        }
        /// <summary>
        /// Calls an RPC
        /// </summary>
        /// <param name="RPC_name">Name of RPC you want to call (name set in BindRPC).</param>
        /// <param name="target">Who you want to send RPC to.</param>
        /// <param name="args">Parameters for RPC if needed. They have to be serializable.</param>
        public static  void RPC(string rpcName, RPCTarget target, params object[] args)
        {
            string jsonParams = JsonConvert.SerializeObject(args);
            Command CallRPC = new Command(NetworkManager.client, 0, "RPC", $"¬{rpcName}¬{(int)target}¬{jsonParams}");
            CallRPC.Format();
            Packet output = new Packet(NetworkManager.client, CallRPC.GetBytes(), DataType.Custom);
            NetworkManager.client.SendPacket(output);

            if (RPCTarget.All == target)
                RunRPC(rpcName, args);
        }

        /// <summary>
        /// When binding an RPC, the method must be return type void.
        /// </summary>
        /// <param name="RPC_name">Set the name of the RPC.</param>
        /// <param name="Method">The method you want to be able to call by the RPC name.</param>
        public static void BindRPC(string rpcName, Delegate method)
        {
            if (rpcName == null || rpcName == " ")
            {
                if (debug.Error())
                    Console.WriteLine("Actual RPC name needed!");
                return;
            }
            if (!bindedRpcs.ContainsKey(rpcName))
            {
                bindedRpcs[rpcName] = method;
            }
            else
            {
                if (debug.Error())
                    Console.WriteLine($"RPC already exists with name {rpcName}!");
            }
        }
        public static void BindRPC(string rpcName, Action m) => BindRPC(rpcName, (Delegate)m);
        public static void BindRPC<T>(string rpcName, Action<T> m) => BindRPC(rpcName, (Delegate)m);
        public static void BindRPC<T1, T2>(string rpcName, Action<T1, T2> m) => BindRPC(rpcName, (Delegate)m);
        public static void BindRPC<T1, T2, T3>(string rpcName, Action<T1, T2, T3> m) => BindRPC(rpcName, (Delegate)m);
        public static void BindRPC<T1, T2, T3, T4>(string rpcName, Action<T1, T2, T3, T4> m) => BindRPC(rpcName, (Delegate)m);
        public static void BindRPC<T1, T2, T3, T4, T5>(string rpcName, System.Action<T1, T2, T3, T4, T5> m) => BindRPC(rpcName, (Delegate)m);

        public static void RunRPC(string rpcName, params object[] args)
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
                    if (debug.Error())
                        Console.WriteLine($"Error trying to execute RPC: '{rpcName}'. \nError: {e}");
                }
            }
            else
            {
                if (debug.Warning())
                    Console.WriteLine($"RPC Command '{rpcName}' recieved but no RPC was bound, use BindRPC('{rpcName}', Delegate) to do so");
            }
        }
    }
}