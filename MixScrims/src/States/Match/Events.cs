using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace MixScrims;

public partial class MixScrims
{
    /// <summary>
    /// Handles the end of a match and transitions the system to a fresh match state.
    /// </summary>
    [GameEventHandler (HookMode.Pre)]
    public HookResult HandleMatchEnd(EventCsWinPanelMatch @event)
    {
        if (matchState == MatchState.Match)
        {
            Core.Scheduler.DelayBySeconds(10, () =>
            {
                logger.LogInformation("Match ended, transitioning to Fresh match state.");
                ResetPluginState();
            });
        }
        return HookResult.Continue;
    }

    /// <summary>
    /// Handles the halftime event during a match by preparing for team swap.
    /// </summary>
    [GameEventHandler (HookMode.Pre)]
    public HookResult HandleMatchHalftime(EventRoundAnnounceLastRoundHalf @event)
    {
        if (matchState == MatchState.Match)
        {
            logger.LogInformation("Match halftime announced - disabling team validation");
            
            // Disable validation IMMEDIATELY before any swaps occur
            isMovingPlayersToTeams = true;
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// Handles the round start event, checking if halftime swap needs to be processed.
    /// </summary>
    [GameEventHandler(HookMode.Post)]
    public HookResult HandleRoundStart(EventRoundStart @event)
    {
        if (matchState == MatchState.Match && isMovingPlayersToTeams)
        {
            logger.LogInformation("Round started after halftime - updating team lists");
            
            // Wait for the game engine to complete the swap
            Core.Scheduler.DelayBySeconds(1f, () =>
            {
                // Now swap our internal lists to match the game state
                var oldPlayingCtPlayers = playingCtPlayers.ToList();
                var oldPlayingTPlayers = playingTPlayers.ToList();
                playingCtPlayers = oldPlayingTPlayers;
                playingTPlayers = oldPlayingCtPlayers;

                logger.LogInformation($"Halftime team lists updated - CT: {playingCtPlayers.Count}, T: {playingTPlayers.Count}");

                // Re-enable validation after players have settled
                Core.Scheduler.DelayBySeconds(1f, () =>
                {
                    isMovingPlayersToTeams = false;
                    logger.LogInformation("Halftime complete - team validation re-enabled");
                });
            });
        }

        return HookResult.Continue;
    }
}
