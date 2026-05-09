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

        public long ID;
        public uint OwnerID;

        public bool Connected;
        public bool isPlayers = false;

        private TextMeshProUGUI ageDisplay;

        [Header("Debug")]
        public bool showEndpoint = false;
        public int age;

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
            if (isPlayers)
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
                if (isPlayers)
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
            if (!GetComponent<NetworkBehaviour>().isPlayers) { return; }
            if (showEndpoint)
            {
                showEndpoint = false;
                Debug.LogWarning($"Endpoint: {(IPEndPoint)root.client.client._socket.LocalEndPoint}");
            }
            if (root.client != null && isPlayers)
            {
                Connected = root.client.isConnected();


            }
        }

    }
}
