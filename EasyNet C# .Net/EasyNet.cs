using EasyNet_BackEnd.Data;
using EasyNet_BackEnd.System;
using EasyNet_BackEnd.UDP;
using EasyNet_Debugging;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EasyNet_BackEnd
{
    namespace System
    {
        public class SystemClient
        {
            public UDP_Client client;
            public uint ID = 0;
            public bool managed = false;
            public ConcurrentQueue<Packet> cache = new ConcurrentQueue<Packet>();
            public ConcurrentDictionary<long, Packet> notSentPackets = new ConcurrentDictionary<long, Packet>();

            public float UpdateRate = 20f;
            private uint packetCount = 0;

            public SystemClient(IPAddress ip, int port)
            {
                client = new UDP_Client(ip, port);
                StartMessageLoop();
                PacketSender();
            }

            public void SendPacket(Packet packet)
            {
                notSentPackets.TryAdd(packet.PacketID, packet);
            }

            public uint GetPacketCount()
            {
                packetCount++;
                if (packetCount > uint.MaxValue)
                {
                    packetCount = 0;
                }
                return packetCount;
            }

            public void UpdateCache()
            {
                if (!managed)
                {
                    while (cache.TryDequeue(out Packet p))
                    {
                        p.Format();
                        DataType type = p.dataType;
                        if (DataType.String == type)
                        {
                            string message = Bytes.ToString(p.Data);
                            Console.WriteLine($"[Server] {message}");
                        }
                        else if (DataType.Int == type)
                        {
                            int payload = Bytes.ToInt32(p.Data);
                            Console.WriteLine($"[Server] {payload}");
                        }
                        else if (DataType.SetID == type)
                        {
                            ID = Bytes.ToUInt32(p.Data);
                            Console.WriteLine($"Set Client ID: {ID}");
                        }
                        else if (DataType.Ping == type)
                        {
                            Packet pingBack = new Packet(this, Bytes.Get("Pingback"), DataType.Ping);
                            _ = client.Send(pingBack.GetBytes());
                        }
                        else
                        {
                            Console.WriteLine($"Packet ingored with type: {type} ({(int)type})");
                        }
                    }
                }
            }

            public void PacketSender()
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        UpdateCache();
                        foreach (var packet in notSentPackets.Values)
                        {
                            UpdateCache();
                            if (packet.resendCount < 1)
                            {
                                await client.Send(packet.GetBytes());
                                packet.resendCount++;

                                // Remove for resend checks!
                                notSentPackets.TryRemove(packet.PacketID, out _);
                            }
                            else
                            {
                                notSentPackets.TryRemove(packet.PacketID, out _);
                            }
                        }
                        await Task.Delay(1000 / (int)UpdateRate);
                    }
                });
            }

            public void StartMessageLoop()
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                        while (true)
                        {

                            SocketReceiveFromResult res = await client._socket.ReceiveFromAsync(
                                new ArraySegment<byte>(client._buffer_recv),
                                SocketFlags.None,
                                endPoint
                            );
                            byte[] recievedData = new byte[res.ReceivedBytes];
                            Buffer.BlockCopy(client._buffer_recv, 0, recievedData, 0, res.ReceivedBytes);
                            Packet recieved = new Packet(this, null);
                            recieved.rawData = recievedData;
                            recieved.Format();

                            if (debug.Log())
                                Console.WriteLine($"Packet recieved of type: {recieved.dataType} from {recieved.SenderID}");


                            if (0 != recieved.SenderID)
                            {
                                cache.Enqueue(recieved);
                            }


                            if (recieved.dataType != DataType.Callback)
                            {
                                Packet callback = new Packet(this, Bytes.Get(1), DataType.Callback);
                                callback.PacketID = recieved.PacketID;
                                notSentPackets.TryAdd(callback.PacketID, callback);
                            }
                            else
                            {
                                notSentPackets.TryRemove(recieved.PacketID, out _);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (debug.Log())
                            Console.WriteLine($"Message Loop Failed with error:\n{e}");
                    }
                });
            }

            public bool isConnected() => ID != 0;
        }

        public class Server
        {
            public UDP_Server server;
            public uint ID = 1;
            public ConcurrentQueue<Packet> cache = new ConcurrentQueue<Packet>();
            public Dictionary<uint, EndPoint> clients = new Dictionary<uint, EndPoint>();
            public ConcurrentDictionary<long, Packet> notSentPackets = new ConcurrentDictionary<long, Packet>();

            public bool DEBUG_LOG;
            public bool managed = false;
            private int UpdateRate = 20;
            private uint packetCount = 0;

            private Dictionary<uint, DateTime> lastSeen = new();

            public Server(int port) : this(port, false) { }

            public Server(int port, bool debugMode)
            {
                DEBUG_LOG = debugMode;
                server = new UDP_Server(port);
                StartMessageLoop();
                PacketSender();
                _ = ClientPings();
            }

            public uint GetMasterClient()
            {
                uint masterClient = uint.MaxValue;
                foreach (uint playerID in clients.Keys)
                {
                    if (playerID < masterClient)
                    {
                        masterClient = playerID;
                    }
                }
                return masterClient;
            }

            public void SendData(string data)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                Packet packet = new Packet(this, bytes, DataType.String);
                notSentPackets.TryAdd(packet.PacketID, packet);
            }

            public void SendData(int data)
            {
                byte[] bytes = Bytes.Get(data);
                Packet packet = new Packet(this, bytes, DataType.Int);
                notSentPackets.TryAdd(packet.PacketID, packet);
            }

            async Task ClientPings()
            {
                while (true)
                {
                    if (clients.Count < 1)
                    {
                        await Task.Delay(500);
                        if (debug.Log())
                            Console.WriteLine("Not Pinging to 0 Clients");
                        continue;
                    }

                    if (debug.Log())
                        Console.WriteLine("Sent out Pings to clients");
                    Packet ping = new Packet(this, Bytes.Get("Ping"), DataType.Ping);
                    notSentPackets.TryAdd(ping.PacketID, ping);

                    await Task.Delay(5000);

                    var now = DateTime.UtcNow;
                    var timeout = TimeSpan.FromSeconds(10);

                    List<uint> toRemove = new();

                    foreach (var kvp in clients)
                    {
                        uint id = kvp.Key;

                        if (!lastSeen.ContainsKey(id) || now - lastSeen[id] > timeout)
                        {
                            toRemove.Add(id);
                        }
                    }

                    foreach (uint id in toRemove)
                    {
                        clients.Remove(id);
                        lastSeen.Remove(id);
                        if (debug.Log())
                        Console.WriteLine($"Client[{id}] timed out");
                        Packet kickedClient = new Packet(this, Bytes.Get($"KICK 1 0 {id}"));
                        notSentPackets.TryAdd(kickedClient.PacketID, kickedClient);
                    }

                }
            }
            public void UpdateCache()
            {
                if (!managed)
                {
                    while (cache.TryDequeue(out Packet p))
                    {
                        notSentPackets[p.PacketID] = p;
                        p.Format();
                        DataType type = p.dataType;
                        if (DataType.String == type)
                        {
                            string message = Bytes.ToString(p.Data);
                            Console.WriteLine($"[Server] {message}");
                        }
                        else if (DataType.Int == type)
                        {
                            int payload = Bytes.ToInt32(p.Data);
                            Console.WriteLine($"[Server] {payload}");
                        }
                        else if (DataType.SetID == type)
                        {
                            uint reply = Bytes.ToUInt32(p.Data);
                            Console.WriteLine($"ClientID set replied: '{reply}'");
                        }
                        else if (DataType.Custom == type)
                        {
                            Console.WriteLine("Custom Packet found: Echoing");
                            notSentPackets.TryAdd(p.PacketID, p);
                        }
                        else
                        {
                            if (debug.Log())
                                Console.WriteLine($"Packet ingored with type: {type} ({(int)type})");
                        }
                    }
                }
            }

            public void PacketSender()
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        UpdateCache();
                        foreach (var packet in notSentPackets.Values)
                        {
                            if (packet.resendCount < 1)
                            {
                                if (packet.target == null)
                                {
                                    foreach (EndPoint ep in clients.Values)
                                    {
                                        UpdateCache();

                                        if (packet.dataType != DataType.Callback)
                                        {
                                            packet.Format();
                                            string value = ".";
                                            if (packet.dataType == DataType.SetID)
                                                value = $" -> Value: {Bytes.ToUInt32(packet.Data)}";
                                            if (debug.Log())
                                                Console.WriteLine($"Sent Packet -> {packet.dataType}{value}");
                                            await server.SendTo(ep, packet.GetBytes());
                                        }
                                    }
                                }
                                else
                                {
                                    UpdateCache();

                                    if (packet.dataType != DataType.Callback)
                                    {
                                        packet.Format();
                                        string value = ".";
                                        if (packet.dataType == DataType.SetID)
                                            value = $" -> Value: {Bytes.ToUInt32(packet.Data)}";
                                        if (debug.Log())
                                            Console.WriteLine($"Sent Packet -> {packet.dataType}{value}");
                                        await server.SendTo(packet.target, packet.GetBytes());
                                    }
                                }


                                packet.resendCount++;
                                notSentPackets.TryRemove(packet.PacketID, out _);
                            }
                            else
                            {
                                notSentPackets.TryRemove(packet.PacketID, out _);
                            }
                        }
                        await Task.Delay(1000 / UpdateRate);
                    }
                });
            }

            public void StartMessageLoop()
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            SocketReceiveMessageFromResult res = await server._socket.ReceiveMessageFromAsync(server._buffer_recv_segment, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                            byte[] recievedData = new byte[res.ReceivedBytes];
                            Buffer.BlockCopy(server._buffer_recv, 0, recievedData, 0, res.ReceivedBytes);
                            Packet echoPacket = new Packet(this, Bytes.Get("EchoPacket"), DataType.String);
                            echoPacket.rawData = recievedData;
                            echoPacket.Format();

                            if (echoPacket.SenderID != 0 && echoPacket.dataType != DataType.Callback)
                                cache.Enqueue(echoPacket);

                            EndPoint endPoint = res.RemoteEndPoint;
                            if (debug.Log())
                                Console.WriteLine($"Recieved Packet: {echoPacket.dataType} From ID: {echoPacket.SenderID}");

                            lastSeen[echoPacket.SenderID] = DateTime.UtcNow;

                            if (!clients.ContainsValue(endPoint))
                            {
                                uint newId = (clients.Keys.Count > 0 ? clients.Keys.Max() : 1) + 1;
                                if (debug.Log())
                                    Console.WriteLine($"Given a new client an id of {newId}.");

                                clients.Add(newId, endPoint);
                                SendIdUpdate(newId, endPoint);
                            }
                            else if (echoPacket.SenderID == 0)
                            {
                                uint existingId = clients.FirstOrDefault(x => x.Value.Equals(endPoint)).Key;

                                if (existingId == 0)
                                {
                                    existingId = (clients.Keys.Count > 0 ? clients.Keys.Max() : 1) + 1;
                                    clients.Add(existingId, endPoint);
                                }

                                SendIdUpdate(existingId, endPoint);
                            }
                            if (echoPacket.dataType == DataType.Callback)
                            {
                                notSentPackets.TryRemove(echoPacket.PacketID, out _);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });
            }
            void SendIdUpdate(uint id, EndPoint target)
            {
                if (debug.Log())
                    Console.WriteLine($"Setting ID: {id}\nRetranslated: {Bytes.ToUInt32(Bytes.Get(id))}");
                byte[] bytes = Bytes.Get(id);
                Packet updateID = new Packet(this, bytes, DataType.SetID);
                updateID.target = target;
                notSentPackets.TryAdd(updateID.PacketID, updateID);
            }

            public uint GetPacketCount()
            {
                packetCount++;
                return packetCount;
            }
        }
    }

    namespace Data
    {
        public class Packet
        {
            public uint SenderID;
            public long PacketID;
            public DataType dataType;

            public byte[] Data;
            public byte[] rawData;

            public int resendCount = 0;
            public EndPoint target;

            private SystemClient cParent;
            private Server hParent;

            public Packet(SystemClient me, byte[] _data)
            {
                Data = _data; SenderID = me.ID;
                PacketID = ((long)SenderID << 32 | me.GetPacketCount());
                cParent = me;
            }
            public Packet(SystemClient me, byte[] _data, DataType type) : this(me, _data)
            {
                dataType = type;
            }
            public Packet(Server me, byte[] _data, DataType type)
            {
                Data = _data; SenderID = me.ID;
                PacketID = ((long)SenderID << 32 | me.GetPacketCount());
                hParent = me;
                dataType = type;
            }
            public Packet(Server me, byte[] _data)
            {
                Data = _data; SenderID = me.ID;
                PacketID = ((long)SenderID << 32 | me.GetPacketCount());
                hParent = me;
            }

            public byte[] GetBytes()
            {
                byte[] sId = Bytes.Get(SenderID);
                byte[] pId = Bytes.Get(PacketID);
                byte[] dType = Bytes.Get((int)dataType);
                int dataLen = Data?.Length ?? 0;
                byte[] combined = new byte[16 + dataLen];
                Buffer.BlockCopy(sId, 0, combined, 0, 4);
                Buffer.BlockCopy(pId, 0, combined, 4, 8);
                Buffer.BlockCopy(dType, 0, combined, 12, 4);
                if (dataLen > 0) Buffer.BlockCopy(Data, 0, combined, 16, dataLen);
                return combined;
            }

            public object GetData()
            {
                Format();
                if (Data == null) return null;
                switch (dataType)
                {
                    case DataType.Int: return Bytes.ToInt32(Data);
                    case DataType.String: return Encoding.UTF8.GetString(Data);
                    case DataType.Vector2: return Bytes.ToVector2(Data);
                    case DataType.Vector3: return Bytes.ToVector3(Data);
                    case DataType.Vector4: return Bytes.ToVector4(Data);
                    default: return null;
                }
            }

            public void Format()
            {
                if (rawData == null) rawData = GetBytes();
                SenderID = Bytes.ToUInt32Range(rawData, 0, 4);
                PacketID = Bytes.ToInt64Range(rawData, 4, 8);
                dataType = (DataType)Bytes.ToInt32Range(rawData, 12, 4);
                if (!Enum.IsDefined(typeof(DataType), dataType)) dataType = DataType.Error;
                int payloadLen = rawData.Length - 16;
                Data = new byte[payloadLen];
                if (payloadLen > 0) Buffer.BlockCopy(rawData, 16, Data, 0, payloadLen);
            }
        }

        public enum DataType { Int = 0, String = 1, Vector2 = 2, Vector3 = 3, Vector4 = 4, Callback = 100, SetID = 101, Ping = 200, Error = 404, Custom = 999 }
        public class Vector2 { public float x, y; public Vector2(float _x, float _y) { x = _x; y = _y; } }
        public class Vector3 { public float x, y, z; public Vector3(float _x, float _y, float _z) { x = _x; y = _y; z = _z; } }
        public class Vector4 { public float x, y, z, w; public Vector4(float _x, float _y, float _z, float _w) { x = _x; y = _y; z = _z; w = _w; } }
        /// <summary>
        /// Converts between Bytes and Common Data Types
        /// </summary>
        static class Bytes
        {
            public static byte[] Get(uint value) { byte[] b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); return b; }
            public static byte[] Get(int value) { byte[] b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, value); return b; }
            public static byte[] Get(long value) { byte[] b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, value); return b; }
            public static byte[] Get(Vector2 v) { byte[] b = new byte[8]; Buffer.BlockCopy(Get(v.x), 0, b, 0, 4); Buffer.BlockCopy(Get(v.y), 0, b, 4, 4); return b; }
            public static byte[] Get(string value) { return Encoding.UTF8.GetBytes(value); }
            public static byte[] Get(float value)
            {
                byte[] b = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                return b;
            }

            public static uint ToUInt32(byte[] b) => BinaryPrimitives.ReadUInt32LittleEndian(b);
            public static int ToInt32(byte[] b) => BinaryPrimitives.ReadInt32LittleEndian(b);
            public static float ToFloat(byte[] b) => BitConverter.ToSingle(b, 0);
            public static string ToString(byte[] b) => Encoding.UTF8.GetString(b);
            public static int ToInt32Range(byte[] b, int s, int l) => BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(b, s, l));
            public static uint ToUInt32Range(byte[] b, int s, int l) => BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(b, s, l));
            public static long ToInt64Range(byte[] b, int s, int l) => BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(b, s, l));
            public static Vector2 ToVector2(byte[] b) => new Vector2(ToFloat(Slice(b, 0, 4)), ToFloat(Slice(b, 4, 4)));
            public static Vector3 ToVector3(byte[] b) => new Vector3(ToFloat(Slice(b, 0, 4)), ToFloat(Slice(b, 4, 4)), ToFloat(Slice(b, 8, 4)));
            public static Vector4 ToVector4(byte[] b) => new Vector4(ToFloat(Slice(b, 0, 4)), ToFloat(Slice(b, 4, 4)), ToFloat(Slice(b, 8, 4)), ToFloat(Slice(b, 12, 4)));
            private static byte[] Slice(byte[] b, int s, int l) { byte[] res = new byte[l]; Buffer.BlockCopy(b, s, res, 0, l); return res; }
        }
    }

    namespace UDP
    {
        public class UDP_Server
        {
            public int PORT;
            public Socket _socket;
            public byte[] _buffer_recv;
            public ArraySegment<byte> _buffer_recv_segment;
            public UDP_Server(int newPORT)
            {
                PORT = newPORT;
                _buffer_recv = new byte[2048];
                _buffer_recv_segment = new ArraySegment<byte>(_buffer_recv);
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }

                _socket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            }
            public async Task SendTo(EndPoint recipeint, byte[] data)
            {
                await _socket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, recipeint);
            }
        }

        public class UDP_Client
        {
            public Socket _socket;
            public EndPoint _ep;
            public byte[] _buffer_recv;
            public ArraySegment<byte> _buffer_recv_segment;
            public UDP_Client(IPAddress address, int port)
            {
                _buffer_recv = new byte[2048];
                _buffer_recv_segment = new ArraySegment<byte>(_buffer_recv);
                _ep = new IPEndPoint(address, port);
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }

                _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            }
            public async Task Send(byte[] data)
            {
                await _socket.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, _ep);
            }
        }
    }
}
