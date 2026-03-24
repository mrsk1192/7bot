using System;
using System.Collections.Generic;
using System.Threading;

namespace mnetSevenDaysBridge
{
    public sealed class ObservationCommandQueue
    {
        private readonly object syncRoot = new object();
        private readonly Queue<QueuedObservationCommand> queue = new Queue<QueuedObservationCommand>();

        public bool TryEnqueue(string commandName, Dictionary<string, object> arguments, out QueuedObservationCommand queuedCommand)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException("Command name must not be empty.", nameof(commandName));
            }

            lock (syncRoot)
            {
                queuedCommand = new QueuedObservationCommand(commandName, arguments);
                queue.Enqueue(queuedCommand);
                return true;
            }
        }

        public IList<QueuedObservationCommand> Drain()
        {
            lock (syncRoot)
            {
                var drained = new List<QueuedObservationCommand>(queue.Count);
                while (queue.Count > 0)
                {
                    drained.Add(queue.Dequeue());
                }

                return drained;
            }
        }
    }

    public sealed class QueuedObservationCommand
    {
        public QueuedObservationCommand(string commandName, Dictionary<string, object> arguments)
        {
            CommandName = commandName;
            Arguments = arguments ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Completion = new ManualResetEventSlim(false);
        }

        public string CommandName { get; }

        public Dictionary<string, object> Arguments { get; }

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
}
