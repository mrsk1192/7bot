using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public sealed class InputBackendRouter
    {
        public const string InternalBackendName = "internal";
        public const string OsBackendName = "os";
        public const string HybridBackendName = "hybrid";

        private readonly InternalInputBackend internalBackend;
        private readonly OSInputBackend osBackend;

        public InputBackendRouter(InternalInputBackend internalBackend, OSInputBackend osBackend)
        {
            this.internalBackend = internalBackend;
            this.osBackend = osBackend;
        }

        public string ActiveBackendName
        {
            get
            {
                return osBackend.IsAvailable ? HybridBackendName : internalBackend.Name;
            }
        }

        public IList<string> GetAvailableBackends()
        {
            var backends = new List<string> { internalBackend.Name };
            if (osBackend.IsAvailable)
            {
                backends.Add(osBackend.Name);
            }

            return backends;
        }

        public bool UsesInternal(string action)
        {
            var backend = ActionCatalog.Get(action).Backend;
            return backend == InternalBackendName || backend == HybridBackendName;
        }

        public bool UsesOs(string action)
        {
            var backend = ActionCatalog.Get(action).Backend;
            return osBackend.IsAvailable && (backend == OsBackendName || backend == HybridBackendName);
        }

        public string GetReportedBackend(string action)
        {
            var backend = ActionCatalog.Get(action).Backend;
            if (backend == OsBackendName && !osBackend.IsAvailable)
            {
                return "unsupported";
            }

            return backend;
        }
    }
}
