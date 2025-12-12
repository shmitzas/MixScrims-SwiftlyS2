using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MixScrims;

public sealed partial class MixScrims
{
    // Timers (CancellationTokenSource in SwiftlyS2)
    private CancellationTokenSource? playerStatusTimer;
    private CancellationTokenSource? commandRemindersTimer;
    private CancellationTokenSource? captainsAnnouncementsTimer;

    private readonly List<IPlayer> readyPlayers = [];
    private readonly List<int> freshlyJoinedPlayers = new();
    private readonly List<int> recentlyDisconnectedPlayers = new();
    private Team previousAutoJoinedTeam = Team.None;
    private bool canPlayerBeRespawned = true;
    private bool isMovingPlayersToTeams = false;

    private DateTime lastDiscordInviteSentAt = DateTime.MinValue;


    private void StartAnnouncementTimers()
    {
        // Players ready status
        playerStatusTimer = Core.Scheduler.RepeatBySeconds(
            periodSeconds: cfg.ChatAnnouncementTimers.PlayersReadyStatus,
            task: PrintReadyAndNotReadyPlayers
        );

        // Command reminders
        commandRemindersTimer = Core.Scheduler.RepeatBySeconds(
            periodSeconds: cfg.ChatAnnouncementTimers.CommandReminders,
            task: PrintCommandReminders
        );
    }

    /// <summary>
    /// Checks whether the required number of players are ready to begin the next phase of the match, and advances the
    /// match state if conditions are met.
    /// </summary>
    private void CheckReadyPlayersToStart()
    {
        logger.LogInformation("CheckReadyPlayersToStart: readyPlayers={ReadyCount} | Required={Required}", readyPlayers.Count, GetNumberOfPlayersRequiredToStart());
        if (matchState == MatchState.Warmup && readyPlayers.Count >= GetNumberOfPlayersRequiredToStart())
        {
            logger.LogInformation("CheckReadyPlayersToStart: Starting Map Voting Phase");
            StartMapVotingPhase();
        }

        if (matchState == MatchState.MapChosen && readyPlayers.Count >= GetNumberOfPlayersRequiredToStart())
        {
            logger.LogInformation("CheckReadyPlayersToStart: Starting Team Picking Phase");
            StartTeamPickingPhase();
        }
    }
    
    /// <summary>
    /// Adds the specified player to the ready list if the match is in a state that allows players to become ready.
    /// </summary>
    private void AddPlayerToReadyList(IPlayer player, bool announce = false)
    {
        var name = player.Controller?.PlayerName ?? $"#{player.PlayerID}";
        logger.LogInformation("AddPlayerToReadyList: called for {Player}", name);

        if (matchState == MatchState.Warmup || matchState == MatchState.MapChosen)
        {
            if (readyPlayers.Any(p => p.PlayerID == player.PlayerID))
            {
                if (announce)
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(player);
                    player.SendChat(localizer["serverPrefix"] + " " + localizer["command.alreadyReady"]);
                }
                return;
            }

            readyPlayers.Add(player);
            if (announce)
            {
                PrintMessageToAllPlayers(Core.Localizer["command.setReady", name]);
            }
            CheckReadyPlayersToStart();
        }
        else
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendChat(localizer["serverPrefix"] + " " + localizer["command.invalidState", "ready"]);
        }
    }

    /// <summary>
    /// Removes the specified player from the ready list, optionally announcing the change.
    /// </summary>
    private void RemovePlayerFromReadyList(IPlayer player, bool announce = false)
    {
        var name = player.Controller?.PlayerName ?? $"#{player.PlayerID}";
        logger.LogInformation("RemovePlayerFromReadyList: called for {Player}", name);

        if (matchState == MatchState.Warmup || matchState == MatchState.MapChosen)
        {
            var existing = readyPlayers.FirstOrDefault(p => p.PlayerID == player.PlayerID);
            if (existing != null)
            {
                if (announce)
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(player);
                    player.SendChat(localizer["serverPrefix"] + " " + localizer["command.setUnready", name]);
                }
                readyPlayers.Remove(existing);
                CheckReadyPlayersToStart();
                return;
            }

            if (announce)
            {
                PrintMessageToAllPlayers(Core.Localizer["command.alreadyUnready"]);
            }
            CheckReadyPlayersToStart();
        }
        else
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendChat(localizer["serverPrefix"] + " " + localizer["command.invalidState", "unready"]);
        }
    }

    /// <summary>
    /// Initiates the process of loading the specified map and updates the match state accordingly.
    /// </summary>
    private void LoadSelectedMap(MapDetails map)
    {
		if (cfg.DetailedLogging)
			logger.LogInformation("LoadSelectedMap: Loading map {Map}", map.MapName);

        if (matchState == MatchState.MapLoading)
        {
            ScheduleMapLoadingAnnouncement(map);
            return;
        }

        Core.Engine.ExecuteCommand("tv_stoprecord");
        var loadMapToken = Core.Scheduler.DelayBySeconds(5, () => LoadMap(map));
        Core.Scheduler.StopOnMapChange(loadMapToken);

        matchState = MatchState.MapLoading;
        PrintMessageToAllPlayers(Core.Localizer["map.changingMap", map.DisplayName]);
        ScheduleMapLoadingAnnouncement(map);
    }

    /// <summary>
    /// Loads the specified map into the game engine, switching the current level to the provided map.
    /// </summary>
    private void LoadMap(MapDetails map)
    {
		if (cfg.DetailedLogging)
			logger.LogInformation("LoadMap: Executing map change to {Map}", map.MapName);
        if (map.IsWorkshopMap && !string.IsNullOrWhiteSpace(map.WorkshopId))
        {
            Core.Engine.ExecuteCommand($"ds_workshop_changelevel {map.MapName}");
            Core.Engine.ExecuteCommand($"host_workshop_map {map.WorkshopId}");
        }
        else
        {
            Core.Engine.ExecuteCommand($"map {map.MapName}");
        }
    }

    /// <summary>
    /// Schedules a chat announcement to notify players that the specified map is loading after a 15-second delay.
    /// </summary>
    private void ScheduleMapLoadingAnnouncement(MapDetails map)
    {
		if (cfg.DetailedLogging)
			logger.LogInformation("ScheduleMapLoadingAnnouncement: Scheduling map loading announcement in 15 seconds");

        var token = Core.Scheduler.DelayBySeconds(15, () =>
        {
            if (matchState == MatchState.MapLoading)
            {
                PrintMessageToAllPlayers(Core.Localizer["map.mapLoading", map.DisplayName]);
                LoadSelectedMap(map);
            }
        });

        Core.Scheduler.StopOnMapChange(token);
    }
    
    /// <summary>
    /// Selects a random map from the list of available maps that can be nominated for voting.
    /// </summary>
    private MapDetails GetRandomMap()
    {
        var maps = cfg.Maps.Where(m => m.CanBeVoted).ToList();
        if (maps.Count == 0)
        {
            logger.LogError("GetRandomMap: No maps available for voting. Check configuration.");
            return new MapDetails { MapName = "de_mirage", DisplayName = "Mirage", CanBeVoted = true };
        }
        var random = new Random();
        int index = random.Next(maps.Count);
        return maps[index];
    }

    /// <summary>
    /// Sets the display name for the specified team.
    /// </summary>
    private void SetTeamName(Team team, string? name)
    {
        var teamName = (name is null || string.IsNullOrWhiteSpace(name)) ? null : name.Trim();

        if (team == Team.CT)
        {
            Core.Scheduler.NextTick(() =>
            {
                if (teamName is null)
                {
                    Core.Engine.ExecuteCommand("mp_teamname_1 COUNTER-TERRORISTS");
                }
                else
                {
                    Core.Engine.ExecuteCommand($"mp_teamname_1 team_{teamName}");
                }
            });
        }
        else if (team == Team.T)
        {
            Core.Scheduler.NextTick(() =>
            {
                if (teamName is null)
                {
                    Core.Engine.ExecuteCommand("mp_teamname_2 TERRORISTS");
                }
                else
                {
                    Core.Engine.ExecuteCommand($"mp_teamname_2 team_{teamName}");
                }
            });
        }
    }

    /// <summary>
    /// Assigns the Counter-Terrorist (CT) captain role to the specified player.
    /// </summary>
    private void SetCtCaptain(IPlayer admin, string pickedPlayerName)
    {
        var player = GetPlayerByName(pickedPlayerName);

        if (player == null || !IsPlayerValid(player))
        {
            logger.LogError("SetCtCaptain: picked player is invalid");
            var localizer = Core.Translation.GetPlayerLocalizer(admin);
            admin.SendChat(localizer["serverPrefix"] + " " + localizer["error.invalidPlayerPicked", pickedPlayerName]);
            return;
        }

        PrintMessageToAllPlayers(Core.Localizer["command.captainCt", admin.Controller?.PlayerName ?? $"#{admin.PlayerID}", player.Controller?.PlayerName ?? $"#{player.PlayerID}"]);
        PickCtCaptain(player);

        CloseMenuForPlayer(admin);
    }

    /// <summary>
    /// Assigns the Terrorist team captain to the specified player, as selected by an administrator.
    /// </summary>
    private void SetTCaptain(IPlayer admin, string pickedPlayerName)
    {
        var player = GetPlayerByName(pickedPlayerName);

        if (player == null || !IsPlayerValid(player))
        {
            logger.LogError("SetTCaptain: picked player is invalid");
            var localizer = Core.Translation.GetPlayerLocalizer(admin);
            admin.SendChat(localizer["serverPrefix"] + " " + localizer["error.invalidPlayerPicked", pickedPlayerName]);
            return;
        }

        PrintMessageToAllPlayers(Core.Localizer["command.captainT", admin.Controller?.PlayerName ?? $"#{admin.PlayerID}", player.Controller?.PlayerName ?? $"#{player.PlayerID}"]);
        PickTCaptain(player);

        CloseMenuForPlayer(admin);
    }
}