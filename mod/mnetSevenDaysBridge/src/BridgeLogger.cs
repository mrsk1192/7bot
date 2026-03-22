using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace mnetSevenDaysBridge
{
    public sealed class BridgeLogger
    {
        private readonly object syncRoot = new object();
        private readonly Queue<string> tailBuffer;
        private readonly string logFilePath;
        private readonly int maxLines;

        public BridgeLogger(string modRootPath, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(modRootPath))
            {
                throw new ArgumentException("Mod root path must not be empty.", nameof(modRootPath));
            }

            this.maxLines = Math.Max(100, maxLines);
            tailBuffer = new Queue<string>(this.maxLines);

            var logDirectory = Path.Combine(modRootPath, "Logs");
            Directory.CreateDirectory(logDirectory);
            logFilePath = Path.Combine(logDirectory, "mnetSevenDaysBridge.log");
        }

        public string LogFilePath
        {
            get { return logFilePath; }
        }

        public void Info(string message)
        {
            Write("INF", message, null);
        }

        public void Warn(string message)
        {
            Write("WRN", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERR", message, exception);
        }

        public IList<string> GetTail(int count)
        {
            lock (syncRoot)
            {
                var safeCount = Math.Max(1, count);
                var list = new List<string>(tailBuffer);
                if (list.Count <= safeCount)
                {
                    return list;
                }

                return list.GetRange(list.Count - safeCount, safeCount);
            }
        }

        private void Write(string level, string message, Exception exception)
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.UtcNow.ToString("o"));
            builder.Append(" [");
            builder.Append(level);
            builder.Append("] ");
            builder.Append(message);
            if (exception != null)
            {
                builder.Append(" | ");
                builder.Append(exception);
            }

            var line = builder.ToString();
            try
            {
                Log.Out("[mnetSevenDaysBridge] " + line);
            }
            catch
            {
                Debug.Log("[mnetSevenDaysBridge] " + line);
            }

            lock (syncRoot)
            {
                try
                {
                    var logDirectory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrWhiteSpace(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    File.AppendAllText(logFilePath, line + Environment.NewLine);
                }
                catch (Exception fileException)
                {
                    try
                    {
                        Log.Warning("[mnetSevenDaysBridge] File logging failed: " + fileException);
                    }
                    catch
                    {
                        Debug.LogWarning("[mnetSevenDaysBridge] File logging failed: " + fileException);
                    }
                }

                tailBuffer.Enqueue(line);
                while (tailBuffer.Count > maxLines)
                {
                    tailBuffer.Dequeue();
                }
            }
        }
    }
}
