using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NOLoader.DiagCommon
{
    public struct No2Sample
    {
        public bool Received;
        public int FieldCount;
        public float Px;
        public float Py;
        public float Pz;
        public float Vx;
        public float Vy;
        public float Vz;
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float GLoad;
        public int Station;
        public int WeaponHash;
        public int Ammo;
    }

    public static class TelemetryProbe
    {
        public static bool TryReceiveNo2(int port, int timeoutMs, out No2Sample sample)
        {
            sample = default;
            UdpClient? client = null;
            try
            {
                client = new UdpClient(AddressFamily.InterNetwork);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
                client.Client.ReceiveTimeout = timeoutMs;
                var remote = new IPEndPoint(IPAddress.Loopback, 0);
                byte[] data = client.Receive(ref remote);
                string text = Encoding.UTF8.GetString(data);
                return TryParseNo2(text, out sample);
            }
            catch
            {
                return false;
            }
            finally
            {
                client?.Close();
            }
        }

        public static bool TryReceiveNo2Async(int port, int timeoutMs, Action<bool, No2Sample> callback)
        {
            var thread = new Thread(() =>
            {
                bool ok = TryReceiveNo2(port, timeoutMs, out No2Sample sample);
                callback(ok, sample);
            })
            {
                IsBackground = true,
                Name = "NOLoader.DiagNo2Probe"
            };
            thread.Start();
            return true;
        }

        public static bool TryReceiveNo2Burst(int port, int listenMs, int minPackets, out int packetCount, out No2Sample lastSample)
        {
            packetCount = 0;
            lastSample = default;
            UdpClient? client = null;
            try
            {
                client = new UdpClient(AddressFamily.InterNetwork);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));

                int deadline = Environment.TickCount + listenMs;
                while (Environment.TickCount < deadline && packetCount < minPackets)
                {
                    int remaining = deadline - Environment.TickCount;
                    if (remaining <= 0)
                        break;

                    client.Client.ReceiveTimeout = Math.Max(50, Math.Min(400, remaining));
                    try
                    {
                        var remote = new IPEndPoint(IPAddress.Loopback, 0);
                        byte[] data = client.Receive(ref remote);
                        string text = Encoding.UTF8.GetString(data);
                        if (TryParseNo2(text, out No2Sample sample))
                        {
                            packetCount++;
                            lastSample = sample;
                        }
                    }
                    catch (SocketException)
                    {
                        // timeout — keep listening until deadline
                    }
                }

                return packetCount > 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                client?.Close();
            }
        }

        public static void TryReceiveNo2BurstAsync(int port, int listenMs, int minPackets, Action<bool, int, No2Sample> callback)
        {
            var thread = new Thread(() =>
            {
                bool ok = TryReceiveNo2Burst(port, listenMs, minPackets, out int count, out No2Sample sample);
                callback(ok, count, sample);
            })
            {
                IsBackground = true,
                Name = "NOLoader.DiagNo2Burst"
            };
            thread.Start();
        }

        public static bool TryParseNo2(string packet, out No2Sample sample)
        {
            sample = default;
            if (string.IsNullOrEmpty(packet))
                return false;

            string[] parts = packet.Split(',');
            if (parts.Length < 13 || !string.Equals(parts[0], "NO2", StringComparison.OrdinalIgnoreCase))
                return false;

            sample = new No2Sample
            {
                Received = true,
                FieldCount = parts.Length,
                Px = ParseFloat(parts[1]),
                Py = ParseFloat(parts[2]),
                Pz = ParseFloat(parts[3]),
                Vx = ParseFloat(parts[4]),
                Vy = ParseFloat(parts[5]),
                Vz = ParseFloat(parts[6]),
                Pitch = ParseFloat(parts[7]),
                Roll = ParseFloat(parts[8]),
                Yaw = ParseFloat(parts[9]),
                GLoad = ParseFloat(parts[10]),
                Station = ParseInt(parts[11]),
                WeaponHash = ParseInt(parts[12]),
                Ammo = parts.Length > 13 ? ParseInt(parts[13]) : 0
            };
            return true;
        }

        private static float ParseFloat(string text)
            => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;

        private static int ParseInt(string text)
            => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
    }
}
