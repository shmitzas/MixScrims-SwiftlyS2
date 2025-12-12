using Microsoft.Extensions.Logging;

namespace MixScrims;

public partial class MixScrims
{
    ///<summary>
    ///Reset the plugin state to initial values
    ///</summary>
    private void ResetPluginState()
    {
        Core.Scheduler.NextTick(() =>
        {
            LoadSelectedMap(cfg.Maps.First());
            ResetVariables();
        });
    }

    private void ResetVariables()
    {
        logger.LogInformation("ResetPluginState");
        matchState = MatchState.Warmup;
        readyPlayers.Clear();
        playingCtPlayers.Clear();
        playingTPlayers.Clear();
        captainCt = null;
        captainT = null;
        pickedCtPlayers.Clear();
        pickedTPlayers.Clear();
        votedMaps.Clear();
        commandRemindersTimer?.Cancel();
        playerStatusTimer?.Cancel();
        captainsAnnouncementsTimer?.Cancel();
        timeoutCountCt = 3;
        timeoutCountT = 3;
        timeoutPending = TimeoutPending.None;
        canPlayerBeRespawned = true;
    }
}
