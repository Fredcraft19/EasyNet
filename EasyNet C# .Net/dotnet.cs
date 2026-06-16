using EasyNet.Manager;
using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using EasyNet_Debugging;
using System;
using System.Diagnostics;
using System.Text;
using NetworkView = EasyNet.View.Network;

namespace EasyNet
{
    public class Command
    {
        public uint SenderID;   // Sender ID
        public long TargetID;   // Affected Object ID (if a position update, NetworkObjectsSpawn[ID] will move)
        public string str;      // The command / message
        public string data;     // Data of Message, eg. x,y,z

        public byte[] rawBytes;

        public SystemClient client;

        public Command(Packet packet, SystemClient _client)
        {
            client = _client;
            packet.Format();
            SenderID = packet.SenderID;
            rawBytes = packet.Data;
            Format();
        }

        public Command(SystemClient _client, long target, string command, string _data)
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
        public static void Command(SystemClient client, Command command)
        {
            command.Format();
            Packet output = new Packet(client, command.GetBytes(), DataType.Custom);
            client.client.Send(output.GetBytes());
        }
    }
    public abstract class NetworkVariableBase
    {
        public string name;
        public long tick = -1;
        public abstract object BoxedValue { get; set; }
        internal abstract void SetValue(string value);
        internal abstract void SetLocalValue(string value);
        internal abstract string SerializeValue();
        internal abstract void Refresh();
    }
    class NetworkVariable<T> : NetworkVariableBase
    {
        public T _value { get; private set; }
        public NetworkVariable(string _name)
        {
            if (_name.Contains(" "))
            {
                if (debug.Error())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("NetworkVariable name cannot contain spaces");
                    Console.ResetColor();
                }
                return;
            }


            name = _name;
            Command makeVaraible = new Command(NetworkManager.client, 0, "UPDATEVAR", $"{name} {_value}");
            makeVaraible.Format();
            Packet output = new Packet(NetworkManager.client, makeVaraible.GetBytes(), DataType.Custom);
        }
        internal override void SetValue(string value)
        {
            _value = Parse(value);
            if (debug.Log())
                Console.WriteLine($"Updating Variable {name} to {value} Command:\nUPDATEVAR SERVER_TICK {name} {value}");
            Command updateVar = new Command(NetworkManager.client, 0, "UPDATEVAR", $"SERVER_TICK {name} {value}");
            updateVar.Format();
            Packet output = new Packet(NetworkManager.client, updateVar.GetBytes(), DataType.Custom);
            NetworkManager.client.notSentPackets.TryAdd(output.PacketID, output);
        }
        internal override void SetLocalValue(string value)
        {
            _value = Parse(value);
            if (debug.Log())
                Console.WriteLine($"Updating Variable {name} to {value} Command:\nUPDATEVAR SERVER_TICK {name} {value}");
        }
        public T Value
        {
            get => _value;
            set => _value = value;
        }
        internal override void Refresh()   // Send a server a message basically saying Variable = Variable. For Late Joiners
        {
            try
            {
                if (debug.Log())
                    Console.WriteLine("Refreshing Variable!");
                SetValue(_value.ToString());
            }
            catch (Exception e)
            {
                if (debug.Error())
                    Console.WriteLine("Error when refreshing variable: \n" + e);
            }
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
