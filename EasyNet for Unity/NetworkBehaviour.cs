using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using EasyNet.Manager;
using EasyNet_BackEnd.System;
using EasyNet_BackEnd.Data;
using System.Threading.Tasks;
using TMPro;

namespace EasyNet.Behaviour
{
    class NetworkBehaviour : MonoBehaviour
    {
        public NetworkManager root;
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
        public int spawnID;

        /// <summary>
        /// Is Connected to the EasyNet Unity Server
        /// </summary>
        public bool Connected;
        /// <summary>
        /// Is owned by the client
        /// </summary>
        public bool IsPlayers = false;


        [Header("Debug")]
        public bool showEndpoint = false;
        public int age;
        private TextMeshProUGUI ageDisplay;


        private void Awake()
        {
            ageDisplay = GameObject.Find("Age").GetComponent<TextMeshProUGUI>();
            root = FindFirstObjectByType<NetworkManager>();
            try
            {
                root.NetworkVariable.Add("age", new NetworkVariable<int>(root, "age"));
            }
            catch(Exception e)
            {
                Debug.LogWarning($"Already made varaible, age:\nError {e}");
            }
            if (IsPlayers)
            {
                
                root.NetworkObjectsSpawn.Add(gameObject);        // Maybe!? or should networkedObjs be for foreign objects?
                Packet id_ping = new Packet(root.client, Bytes.Get(0), DataType.Int);
                id_ping.Format();
                root.client.notSentPackets.TryAdd(id_ping.PacketID, id_ping);
            }
            StartCoroutine(Age());
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
                    root.NetworkVariable["age"].SetValue(age.ToString());
                    Debug.Log("NETWORKVIEW: Set Network Var 'age' to : " + age);
                }
                else
                {
                    age = root.GetVariable<int>("age");
                }
                ageDisplay.text = root.GetVariable<int>("age").ToString();

            }
        }

        private void Update()
        {
            if (!GetComponent<NetworkBehaviour>().IsPlayers) { return; }
            if (showEndpoint)
            {
                showEndpoint = false;
                Debug.LogWarning($"Endpoint: {(IPEndPoint)root.client.client._socket.LocalEndPoint}");
            }
            if (root.client != null && IsPlayers)
            {
                Connected = root.client.isConnected();


            }
        }

    }
}
