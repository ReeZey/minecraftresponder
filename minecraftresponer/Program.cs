using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using minecraftresponer.structs;
using Newtonsoft.Json;

namespace minecraftresponer
{
    internal class Program
    {
        private const int SEGMENT_BITS = 0x7F;
        private const int CONTINUE_BIT = 0x80;

        private static NetworkStream _stream;
        private static List<byte> _buffer = new List<byte>();

        private static void Main(string[] args)
        {
            StartServer();
            Console.WriteLine("doid");
        }

        private static void StartServer()
        {
            var tcp = new TcpListener(IPAddress.Any, 25565);
            tcp.Start();

            while (true)
            {
                var handler = tcp.AcceptSocket();

                _stream = new NetworkStream(handler);

                int length = ReadVarInt();
                int packedid = ReadVarInt();

                Console.WriteLine($"Packet {packedid}, len {length}");
                
                var pingResponse = new PingResponse
                {
                    version = {
                        name = "1.19",
                        protocol = 759
                    },
                    players = {
                        max = 420,
                        online = 69,
                        sample = new[] {
                            new PlayerSampleStruct("ReeZey","2a350988-50ac-41df-b274-1b5eb6e633c1") 
                        }
                    },
                    description = {
                        text = $"{DateTime.Now}"
                    },
                    //favicon = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray())
                };
                
                
                
                switch (packedid)
                {
                    case 0:
                        int version = ReadVarInt();
                        string address = ReadString(ReadVarInt());

                        var yep = Read(2);
                        Array.Reverse(yep); //little endian moment
                        var serverport = BitConverter.ToUInt16(yep, 0);
                        
                        int nextstate = ReadVarInt();

                        switch (nextstate)
                        {
                            case 1:
                                string str = JsonConvert.SerializeObject(pingResponse);
                        
                                _buffer.Clear();
                                WriteString(str);
                                Flush(0);

                                long longvalue = BitConverter.ToInt64(Read(8), 0);
                                
                                _buffer.Clear();
                                WriteBytes(BitConverter.GetBytes(longvalue));
                                Flush(1);

                                Console.WriteLine($"[{version}] [{address}] [{serverport}] [{nextstate}]");
                                break;
                            
                            case 2:
                                var connectionid = ReadVarInt();
                                var identifier = ReadVarInt();
                                
                                var username = ReadString(ReadVarInt());
                                
                                Console.WriteLine($"{connectionid} {identifier} {username}");
                                handler.Close();
                                break;
                        }
                        break;
                }

                Console.WriteLine("end query");
                //handler.Close();
            }
        }

        private static string PrintByteArray(byte[] bytes, int end)
        {
            var newarray = new byte[end];

            Array.Copy(bytes, 0, newarray, 0, end);
            return $"[ {string.Join(", ", newarray)} ]";
        }

        private static byte[] Read(int length)
        {
            var data = new byte[length];
            _stream.Read(data, 0, length);
            return data;
        }

        private static int ReadVarInt()
        {
            var value = 0;
            var size = 0;
            int b;
            while (((b = _stream.ReadByte()) & CONTINUE_BIT) == CONTINUE_BIT)
            {
                value |= (b & SEGMENT_BITS) << (size++ * 7);
                if (size > 5) throw new IOException("This VarInt is an imposter!");
            }

            return value | ((b & SEGMENT_BITS) << (size * 7));
        }

        private static string ReadString(int length)
        {
            return Encoding.UTF8.GetString(Read(length));
        }

        private static void WriteVarInt(int value)
        {
            while ((value & CONTINUE_BIT) != 0)
            {
                _buffer.Add((byte) ((value & SEGMENT_BITS) | CONTINUE_BIT));
                value = (int) (uint) value >> 7;
            }

            _buffer.Add((byte) value);
        }

        private static void WriteBytes(IEnumerable<byte> value)
        {
            _buffer.AddRange(value);
        }

        private static void WriteString(string data)
        {
            var buffer = Encoding.UTF8.GetBytes(data);
            WriteVarInt(buffer.Length);
            _buffer.AddRange(buffer);
        }

        private static void Flush(int id = -1)
        {
            var buffer = _buffer.ToArray();
            _buffer.Clear();

            var add = 0;
            var packetData = new[] {(byte) 0x00};
            if (id >= 0)
            {
                WriteVarInt(id);
                packetData = _buffer.ToArray();
                add = packetData.Length;
                _buffer.Clear();
            }

            WriteVarInt(buffer.Length + add);
            var bufferLength = _buffer.ToArray();
            _buffer.Clear();

            _stream.Write(bufferLength, 0, bufferLength.Length);
            _stream.Write(packetData, 0, packetData.Length);
            _stream.Write(buffer, 0, buffer.Length);
        }
    }
}