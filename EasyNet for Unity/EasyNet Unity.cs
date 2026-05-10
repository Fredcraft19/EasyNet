using EasyNet.Manager;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using System;
using System.Text;
using UnityEngine;

namespace EasyNet
{
    public class Command
    {
        public uint SenderID;   // Sender ID
        public long TargetID;   // Affected Object ID (if a position update, NetworkObjectsSpawn[ID] will move)
        public string str;      // The command / message
        public string data;     // Data of Message, eg. x,y,z

        public byte[] rawBytes;

        public Client client;

        public Command(Packet packet, Client _client)
        {
            client = _client;
            packet.Format();
            SenderID = packet.SenderID;
            rawBytes = packet.Data;
            Format();
        }

        public Command(Client _client, long target, string command, string _data)
        {
            SenderID = _client.ID;
            client = _client;
            TargetID = target;
            str = command;
            data = _data;
        }

        public Packet GetPacket()
        {
            byte[] bytes = GetBytes();
            Packet output = new Packet(client, bytes, DataType.Custom);
            output.Format();
            return output;
        }
        public byte[] GetBytes()
        {
            Format();
            string formatted = $"{str} {SenderID} {TargetID} {data}";
            rawBytes = Encoding.UTF8.GetBytes(formatted);
            return Encoding.UTF8.GetBytes(formatted);
        }

        public void Format()
        {
            if (rawBytes == null) return;

                string formatted = Encoding.UTF8.GetString(rawBytes);
                string[] parts = formatted.Split(' ');
                str = parts[0];
                SenderID = (uint)Convert.ToInt32(parts[1]);
                TargetID = Convert.ToInt64(parts[2]);
                for (int i = 3; i < parts.Length; i++)
                {
                    data += parts[i] + " ";
                }
           
            
        }
    }
    public static class Send
    {
        public static void Command(Client client, Command command)
        {
            command.Format();
            Packet output = new Packet(client, command.GetBytes(), DataType.Custom);
            client.client.Send(output.GetBytes());
        }
    }
    [Serializable]
    public class LerpedObject
    {
        public GameObject obj;
        public UnityEngine.Vector3 target;
        public bool done = false;

        private UnityEngine.Vector3 currentVelocity;

        public float smoothTime = 0.05f;

        public LerpedObject(GameObject go, UnityEngine.Vector3 _target)
        {
            obj = go;
            target = _target;
            // Start velocity at zero
            currentVelocity = UnityEngine.Vector3.zero;
        }

        public void Lerp()
        {
            if (obj == null) return;

            obj.transform.position = UnityEngine.Vector3.SmoothDamp(
                obj.transform.position,
                target,
                ref currentVelocity,
                smoothTime
            );

            if (UnityEngine.Vector3.Distance(obj.transform.position, target) < 0.01f)
            {
                done = true;
            }
        }
    }
    public abstract class NetworkVariableBase
    {
        public string name;
        public abstract object BoxedValue { get; set; }
        internal abstract void SetValue(string value);
        internal abstract void SetLocalValue(string value);
        internal abstract string SerializeValue();
    }
    class NetworkVariable<T> : NetworkVariableBase
    {
        public T _value { get; private set; }
        private NetworkManager net;
        public NetworkVariable(NetworkManager _net, string _name)
        {
            net = _net;
            if (_name.Contains(" "))
                throw new Exception("NetworkVariable name cannot contain spaces");

            name = _name;
            Command makeVaraible = new Command(net.client, 0, "UPDATEVAR", $"{name} {_value}");
            makeVaraible.Format();
            Packet output = new Packet(net.client, makeVaraible.GetBytes(), DataType.Custom);
        }
        internal override void SetValue(string value)
        {
            _value = Parse(value);
            Debug.Log($"Updating Variable {name} to {value} Command:\nUPDATEVAR SERVER_TICK {name} {value}");
            Command updateVar = new Command(net.client, 0, "UPDATEVAR", $"SERVER_TICK {name} {value}");
            updateVar.Format();
            Packet output = new Packet(net.client, updateVar.GetBytes(), DataType.Custom);
            net.client.notSentPackets.TryAdd(output.PacketID, output);
        }
        internal override void SetLocalValue(string value)
        {
            _value = Parse(value);
            Debug.Log($"Updating Variable {name} to {value} Command:\nUPDATEVAR SERVER_TICK {name} {value}");
        }
        public T Value
        {
            get => _value;
            set => _value = value;
        }

        public override object BoxedValue
        {
            get => _value;
            set => _value = (T)value;
        }

        internal override string SerializeValue()
        {
            return _value.ToString();
        }

        private T Parse(string str)
        {
            return (T)Convert.ChangeType(str, typeof(T));
        }

    }
    /// <summary>
    /// Select who you want to send an RPC to.
    /// </summary>
    public enum RPCTarget
    {
        /// <summary>
        /// All the players including sender
        /// </summary>
        All,
        /// <summary>
        /// All of the players exept the sender
        /// </summary>
        Others,
        /// <summary>
        /// Only the Master Client
        /// </summary>
        Master,
        /// <summary>
        /// All of the players including the sender, the sender runs RPC when recieved by the server, not locally
        /// </summary>
        AllByServer
    }
}

