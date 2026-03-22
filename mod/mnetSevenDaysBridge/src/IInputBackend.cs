using System.Collections.Generic;

namespace mnetSevenDaysBridge
{
    public interface IInputBackend
    {
        string Name { get; }

        bool IsAvailable { get; }

        string AvailabilityNote { get; }

        bool TryGetPlayer(out EntityPlayerLocal player, out string reason);

        void Apply(InputFrameState frameState);

        void ForceNeutralState();

        UiState GetUiState();

        IList<string> GetAvailableBackendNames();
    }
}
