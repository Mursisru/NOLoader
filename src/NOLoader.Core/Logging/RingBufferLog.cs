using System;
using System.Text;
using System.Threading;
using NOLoader.Core.Runtime;

namespace NOLoader.Core.Logging
{
    /// <summary>Zero-allocation ring buffer logger — hot path writes UTF-8 bytes without string concat.</summary>
    public static class RingBufferLog
    {
        private const int Capacity = 65536;
        private static readonly byte[] Buffer = new byte[Capacity];
        private static int _head;
        private static int _tail;
        private static readonly object Sync = new object();
        private static Thread? _flushThread;
        private static string _logDir = string.Empty;
        private static volatile bool _running;
        private static int _flushIntervalMs = 4000;
        [ThreadStatic] private static byte[]? _utf8Scratch;

        public static void StartBackgroundFlush(string loaderRoot)
        {
            _logDir = System.IO.Path.Combine(loaderRoot, "logs");
            System.IO.Directory.CreateDirectory(_logDir);
            _flushIntervalMs = RuntimeConfig.RingFlushIntervalMs;
            if (_flushThread != null) return;
            if (!RuntimeConfig.RingLogEnabled) return;
            _running = true;
            _flushThread = new Thread(FlushLoop)
            {
                IsBackground = true,
                Name = "NOLoader.LogFlush",
                Priority = ThreadPriority.Lowest
            };
            _flushThread.Start();
        }

        public static void WriteAscii(string message)
        {
            if (!RuntimeConfig.RingLogEnabled || string.IsNullOrEmpty(message)) return;
            lock (Sync)
            {
                for (int i = 0; i < message.Length; i++)
                    AppendUtf8Char(message[i]);
                AppendByte((byte)'\r');
                AppendByte((byte)'\n');
            }
        }

        private static void AppendUtf8Char(char c)
        {
            if (c < 0x80)
            {
                AppendByte((byte)c);
                return;
            }

            _utf8Scratch ??= new byte[4];
            int count = EncodeUtf8(c, _utf8Scratch);
            for (int i = 0; i < count; i++)
                AppendByte(_utf8Scratch[i]);
        }

        private static int EncodeUtf8(char c, byte[] target)
        {
            if (c <= 0x7FF)
            {
                target[0] = (byte)(0xC0 | (c >> 6));
                target[1] = (byte)(0x80 | (c & 0x3F));
                return 2;
            }

            target[0] = (byte)(0xE0 | (c >> 12));
            target[1] = (byte)(0x80 | ((c >> 6) & 0x3F));
            target[2] = (byte)(0x80 | (c & 0x3F));
            return 3;
        }

        private static void AppendByte(byte value)
        {
            Buffer[_head] = value;
            _head = (_head + 1) % Capacity;
            if (_head == _tail)
                _tail = (_tail + 1) % Capacity;
        }

        public static string ReadTail(int maxBytes = 4096)
        {
            lock (Sync)
            {
                int len = _head >= _tail ? _head - _tail : Capacity - _tail + _head;
                len = Math.Min(len, maxBytes);
                if (len <= 0) return string.Empty;
                var chunk = new byte[len];
                for (int i = 0; i < len; i++)
                    chunk[i] = Buffer[(_tail + i) % Capacity];
                return Encoding.UTF8.GetString(chunk);
            }
        }

        private static void FlushLoop()
        {
            while (_running)
            {
                Thread.Sleep(_flushIntervalMs);
                FlushToDisk();
            }
        }

        public static void FlushToDisk()
        {
            if (string.IsNullOrEmpty(_logDir)) return;
            string text = ReadTail(Capacity);
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                string path = System.IO.Path.Combine(_logDir, "noloader_ring.log");
                System.IO.File.AppendAllText(path, text);
            }
            catch { /* ignore IO */ }
        }
    }
}
