using System;
using System.Reflection;
using UnityEngine;

namespace NOLoader.Core
{
    internal static class UnityLogBridge
    {
        public static void Log(string message) => Write(Debug.Log, message);

        public static void LogWarning(string message) => Write(Debug.LogWarning, "[WARN] " + message);

        public static void LogError(string message) => Write(Debug.LogError, "[ERR] " + message);

        private static void Write(Action<string> log, string message)
        {
            try
            {
                UnityMainThread.Post(() => log(message));
            }
            catch
            {
                Logging.RingBufferLog.WriteAscii(message);
            }
        }
    }
}
