using SwiftlyS2.Shared.Events;
namespace MixScrims;

public partial class MixScrims
{
    /// <summary>
    /// Registers listeners for events during the Warmup state.
    /// </summary>
    private void RegisterWarmupListeners()
    {
        Core.Event.OnMapLoad += WarmupHandleOnMapStart;
    }

    /// <summary>
    /// Handles map start events during Warmup state by clearing player lists.
    /// </summary>
    private void WarmupHandleOnMapStart(IOnMapLoadEvent @event)
    {
        if (matchState != MatchState.Warmup)
            return;
        LoadWarmupConfig();
    }
}
