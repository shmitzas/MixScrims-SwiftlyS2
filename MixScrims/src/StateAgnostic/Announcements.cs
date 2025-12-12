using Microsoft.Extensions.Logging;

namespace MixScrims;

public partial class MixScrims
{
    List<string> usedReminders = [];

    /// <summary>
    /// Prints ready and not ready players to in-game chat.
    /// </summary>
    private void PrintReadyAndNotReadyPlayers()
    {
        if (cfg.DetailedLogging)
            logger.LogInformation("PrintReadyAndNotReadyPlayers");

        var notReadyPlayers = GetNotReadyPlayers();
        if (cfg.DetailedLogging)
            logger.LogInformation($"Not ready players count: {notReadyPlayers.Count}");

        if (notReadyPlayers.Count > 0)
        {
            string notReadyPlayersNames = string.Join(", ", notReadyPlayers.Select(p => p.Controller.PlayerName));
            if (cfg.DetailedLogging)
                logger.LogInformation($"Not ready players: {notReadyPlayersNames}");
            PrintMessageToAllPlayers(Core.Localizer["announcement.readyStatus", readyPlayers.Count, GetNumberOfPlayersRequiredToStart()]);
            PrintMessageToAllPlayers(Core.Localizer["announcement.notReadyPlayers", notReadyPlayersNames]);
        }
    }

    /// <summary>
    /// Prints command reminders to all players, cycling through all available reminders.
    /// </summary>
    private void PrintCommandReminders()
    {
        if (cfg.DetailedLogging)
            logger.LogInformation("PrintCommandReminders");
        var reminders = cfg.CommandRemindersLocalization;
        string? reminderToUse = reminders.FirstOrDefault(r => !usedReminders.Contains(r));

        if (reminderToUse == null)
        {
            usedReminders.Clear();
            reminderToUse = reminders.FirstOrDefault();
        }

        if (reminderToUse != null)
        {
            PrintMessageToAllPlayers(Core.Localizer[$"commandReminders.{reminderToUse}"]);
            usedReminders.Add(reminderToUse);
        }
    }

    /// <summary>
    /// Announces the chosen captains for both teams to all players, if applicable.
    /// </summary>
    private void PrintChosenCaptains()
    {
        if (cfg.DetailedLogging)
            logger.LogInformation("PrintChosenCaptains");
        if (matchState != MatchState.MapChosen)
        {
            return;
        }

        if (captainCt != null)
        {
            PrintMessageToAllPlayers(Core.Localizer["announcement.captainChosenCt", captainCt.Controller.PlayerName]);
        }
        else
        {
            PrintMessageToAllPlayers(Core.Localizer["announcement.captainNotChosenCt"]);
        }

        if (captainT != null)
        {
            PrintMessageToAllPlayers(Core.Localizer["announcement.captainChosenT", captainT.Controller.PlayerName]);
        }
        else
        {
            PrintMessageToAllPlayers(Core.Localizer["announcement.captainNotChosenT"]);
        }
    }
}
