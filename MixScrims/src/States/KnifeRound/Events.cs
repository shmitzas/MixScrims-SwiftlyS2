using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace MixScrims;

public partial class MixScrims
{
    [GameEventHandler(HookMode.Pre)]
    public HookResult HandleRoundEndOnKnifeRound(EventRoundEnd @event)
    {
        if (matchState == MatchState.KnifeRound)
        {
            if (cfg.DetailedLogging)
                logger.LogInformation("HandleRoundEndOnKnifeRound: Knife round ended, transitioning to PickingStartingSide state.");
            if (@event.Winner == 2)
            {
                PromptWinnerCaptainToChoseStartingSide(Team.T);
            }
            else if (@event.Winner == 3)
            {
                PromptWinnerCaptainToChoseStartingSide(Team.CT);
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult HandleRoundPrestartPostKnifeRound(EventRoundPrestart @event)
    {
        if (matchState == MatchState.PickingStartingSide)
        {
            PauseMatch();
        }
        return HookResult.Continue;
    }
}
