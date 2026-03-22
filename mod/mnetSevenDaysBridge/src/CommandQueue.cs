using System;
using System.Collections.Generic;
using System.Threading;

namespace mnetSevenDaysBridge
{
    public sealed class CommandQueue
    {
        private readonly object syncRoot = new object();
        private readonly Queue<QueuedBridgeCommand> queue = new Queue<QueuedBridgeCommand>();
        private readonly Queue<DateTime> enqueueTimestamps = new Queue<DateTime>();
        private readonly BridgeConfig config;

        public CommandQueue(BridgeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool TryEnqueue(BridgeCommand command, out QueuedBridgeCommand queuedCommand, out BridgeError error)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            lock (syncRoot)
            {
                TrimOldTimestamps();
                if (enqueueTimestamps.Count >= config.MaxCommandsPerSecond)
                {
                    queuedCommand = null;
                    error = new BridgeError
                    {
                        Type = "rate_limited",
                        Message = "Command rate limit exceeded."
                    };
                    return false;
                }

                if (queue.Count >= config.MaxCommandQueueLength)
                {
                    queuedCommand = null;
                    error = new BridgeError
                    {
                        Type = "queue_full",
                        Message = "Command queue is full."
                    };
                    return false;
                }

                queuedCommand = new QueuedBridgeCommand(command);
                queue.Enqueue(queuedCommand);
                enqueueTimestamps.Enqueue(DateTime.UtcNow);
                error = null;
                return true;
            }
        }

        public IList<QueuedBridgeCommand> Drain()
        {
            lock (syncRoot)
            {
                var drained = new List<QueuedBridgeCommand>(queue.Count);
                while (queue.Count > 0)
                {
                    drained.Add(queue.Dequeue());
                }

                return drained;
            }
        }

        private void TrimOldTimestamps()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-1);
            while (enqueueTimestamps.Count > 0 && enqueueTimestamps.Peek() < cutoff)
            {
                enqueueTimestamps.Dequeue();
            }
        }
    }

    public sealed class QueuedBridgeCommand
    {
        public QueuedBridgeCommand(BridgeCommand command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Completion = new ManualResetEventSlim(false);
        }

        public BridgeCommand Command { get; }

        public ManualResetEventSlim Completion { get; }

        public object ResultData { get; private set; }

        public BridgeError Error { get; private set; }

        public void CompleteSuccess(object resultData)
        {
            ResultData = resultData;
            Completion.Set();
        }

        public void CompleteFailure(string errorType, string errorMessage)
        {
            Error = new BridgeError
            {
                Type = errorType,
                Message = errorMessage
            };
            Completion.Set();
        }
    }

    public sealed class BridgeCommandException : Exception
    {
        public BridgeCommandException(int statusCode, string errorType, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
        }

        public int StatusCode { get; }

        public string ErrorType { get; }
    }
}
