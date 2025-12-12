using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2.Core.Menus.OptionsBase;

namespace MixScrims;

public partial class MixScrims
{
    private List<IPlayer> pickedCtPlayers = [];
    private List<IPlayer> pickedTPlayers = [];
    private IPlayer? captainCt { get; set; }
    private IPlayer? captainT { get; set; }

    /// <summary>
    /// Initiates the team-picking phase of the match, assigning captains to teams and prompting the first captain to
    /// pick a player.
    /// </summary>
    private void StartTeamPickingPhase()
    {
        PickCaptains();

        if (captainCt == null || captainT == null)
        {
            logger.LogError("StartTeamPickingPhase: One or both captains are null.");
            PrintMessageToAllPlayers(Core.Localizer["error.CaptainSelectionFailed"]);
            ResetPluginState();
            return;
        }

        matchState = MatchState.PickingTeam;

        captainsAnnouncementsTimer?.Cancel();
        playerStatusTimer?.Cancel();

        PauseMatch();
        Core.Engine.ExecuteCommand("exec mixscrims/teampick.cfg");

        MovePlayersToDesignatedTeamsPrePick();

        SetTeamName(Team.CT, captainCt.Controller.PlayerName);
        SetTeamName(Team.T, captainT.Controller.PlayerName);

        Random random = new Random();
        int teamStarting = random.Next(2, 4);
        if (teamStarting == 3)
        {
            PromptCaptainToPickPlayer(captainCt, Team.CT);
            return;
        }
        if (teamStarting == 2)
        {
            PromptCaptainToPickPlayer(captainT, Team.T);
            return;
        }
    }

    /// <summary>
    /// Prompts the specified team captain to select a player for their team.
    /// </summary>
    private void PromptCaptainToPickPlayer(IPlayer? captain, Team team)
    {
        if (captain == null)
        {
            logger.LogError("PromptCaptainToPickPlayer: Captain is null.");
            return;
        }

        var players = GetPlayers();
        players.RemoveAll(p => pickedCtPlayers.Contains(p) || pickedTPlayers.Contains(p) || p.PlayerID == captain.PlayerID);

        if (players.Count == 0)
        {
            logger.LogWarning("PromptCaptainToPickPlayer: No players available to pick.");
            StartKnifeRound();
            return;
        }

        // Bot: auto-pick random player
        if (IsBot(captain))
        {
            var randomIndex = new Random().Next(players.Count);
            var selectedPlayer = players[randomIndex];
            if (team == Team.CT)
            {
                Core.Scheduler.NextTick(() => AssignPickedPlayerToTeamCt(captain, selectedPlayer.Controller!.PlayerName));
            }
            else
            {
                Core.Scheduler.NextTick(() => AssignPickedPlayerToTeamT(captain, selectedPlayer.Controller!.PlayerName));
            }
            return;
        }

        // Build menu for captain
        var builder = Core.MenusAPI
            .CreateBuilder()
            .Design.SetMenuTitle(Core.Localizer["menu.teamPickingTitle", team == Team.CT ? "CT" : "T"])
            .Design.SetMenuTitleVisible(true)
            .Design.SetMenuFooterVisible(true)
            .EnableSound()
            .SetPlayerFrozen(false)
            .DisableExit()
            .SetAutoCloseDelay(0);

        builder.DisableExit();

        foreach (var player in players)
        {
            var displayName = player.Controller?.PlayerName ?? $"#{player.PlayerID}";
            var button = new ButtonMenuOption(displayName);
            if (team == Team.CT)
            {
                button.Click += async (sender, args) =>
                {
                    Core.Scheduler.NextTick(()=>AssignPickedPlayerToTeamCt(captain, displayName));
                    await ValueTask.CompletedTask;
                };
            }
            else
            {
                button.Click += async (sender, args) =>
                {
                    Core.Scheduler.NextTick(() => AssignPickedPlayerToTeamT(captain, displayName));
                    await ValueTask.CompletedTask;
                };
            }
            builder.AddOption(button);
        }

        var menu = builder.Build();
        if (IsPlayerValid(captain))
        {
            Core.MenusAPI.OpenMenuForPlayer(captain, menu);
        }
    }

    /// <summary>
    /// Assigns captains for the teams if they have not already been selected.
    /// </summary>
    private void PickCaptains()
    {
        if (captainCt == null)
        {
            PickCtCaptain(null);
        }
        if (captainT == null)
        {
            PickTCaptain(null);
        }

    }

    /// <summary>
    /// Assigns a Counter-Terrorist team captain.
    /// </summary>
    private void PickCtCaptain(IPlayer? player)
    {
        if (captainCt != null)
        {
            if (matchState == MatchState.PickingTeam || matchState == MatchState.MapChosen)
            {
                pickedCtPlayers.Remove(captainCt);
            }
            if (matchState == MatchState.KnifeRound)
            {
                playingCtPlayers.Remove(captainCt);
            }
        }

        captainCt = player;

        if (captainCt == null || !IsPlayerValid(captainCt))
        {
            logger.LogError("PickCtCaptain: player is invalid");
            captainCt = PickRandomCaptain();
        }

        if (captainCt != null)
        {
            if (matchState == MatchState.PickingTeam || matchState == MatchState.MapChosen)
            {
                pickedCtPlayers.Add(captainCt);
            }
            if (matchState == MatchState.KnifeRound)
            {
                playingCtPlayers.Add(captainCt);
            }
            logger.LogInformation($"PickCtCaptain: picked {captainCt.Controller!.PlayerName}");
            PrintMessageToAllPlayers(Core.Localizer["teamPicking.pickedCaptainCt", captainCt.Controller!.PlayerName]);
        }
        else
        {
            logger.LogError("PickCtCaptain: Failed to pick a CT captain.");
        }
    }

    /// <summary>
    /// Assigns a Counter-Terrorist team captain.
    /// </summary>
    private void PickTCaptain(IPlayer? player)
    {
        if (captainT != null)
        {
            if (matchState == MatchState.PickingTeam || matchState == MatchState.MapChosen)
            {
                pickedTPlayers.Remove(captainT);
            }
            if (matchState == MatchState.KnifeRound)
            {
                playingTPlayers.Remove(captainT);
            }
        }

        captainT = player;

        if (captainT == null || !IsPlayerValid(captainT))
        {
            logger.LogError("PickTCaptain: player is invalid");
            captainT = PickRandomCaptain();
        }

        if (captainT != null)
        {
            if (matchState == MatchState.PickingTeam || matchState == MatchState.MapChosen)
            {
                pickedTPlayers.Add(captainT);
            }
            if (matchState == MatchState.KnifeRound)
            {
                playingTPlayers.Add(captainT);
            }
            logger.LogInformation($"PickTCaptain: picked {captainT.Controller!.PlayerName}");
            PrintMessageToAllPlayers(Core.Localizer["teamPicking.pickedCaptainT", captainT.Controller!.PlayerName]);
        }
        else
        {
            logger.LogError("PickTCaptain: Failed to pick a T captain.");
        }

    }

    /// <summary>
    /// Selects a random player to serve as a captain from the list of currently playing players.
    /// </summary>
    private IPlayer? PickRandomCaptain()
    {
        var players = GetPlayingPlayers();

        if (captainCt != null)
            players.RemoveAll(p => p.PlayerID == captainCt.PlayerID);
        if (captainT != null)
            players.RemoveAll(p => p.PlayerID == captainT.PlayerID);

        if (players.Count == 0)
        {
            logger.LogWarning("PickRandomCaptain: No players available to pick a captain.");
            return null;
        }
        var random = new Random();
        var captainIndex = random.Next(players.Count);
        return players[captainIndex];
    }

    /// <summary>
    /// Assigns the player selected by the CT captain to the CT team.
    /// </summary>
    private void AssignPickedPlayerToTeamCt(IPlayer captain, string pickedPlayerName)
    {
        var player = GetPlayerByName(pickedPlayerName);

        if (player == null || !IsPlayerValid(player))
        {
            logger.LogError("AssignPickedPlayerToTeamCt: picked player is invalid");
            PrintMessageToPlayer(captain, Core.Localizer["error.invalidPlayerPicked", pickedPlayerName]);
            PromptCaptainToPickPlayer(captain, Team.CT);
            return;
        }

        pickedCtPlayers.Add(player);

        if (IsBot(player))
        {
            player.SwitchTeam(Team.CT);
        }
        player.ChangeTeam(Team.CT);

        logger.LogInformation($"AssignPickedPlayerToTeamCt: {captain.Controller!.PlayerName} picked {player.Controller!.PlayerName} for CT team.");
        PrintMessageToAllPlayers(Core.Localizer["teamPicking.pickedMemberCt", captain.Controller!.PlayerName, player.Controller!.PlayerName]);

        if (pickedCtPlayers.Count + pickedTPlayers.Count >= cfg.MinimumReadyPlayers)
        {
            StartKnifeRound();
            return;
        }

        PromptCaptainToPickPlayer(captainT, Team.T);
        CloseMenuForPlayer(captain);
    }

    /// <summary>
    /// Assigns the player selected by the T captain to the T team.
    /// </summary>
    private void AssignPickedPlayerToTeamT(IPlayer captain, string pickedPlayerName)
    {
        var player = GetPlayerByName(pickedPlayerName);

        if (player == null || !IsPlayerValid(player))
        {
            logger.LogError("AssignPickedPlayerToTeamT: picked player is invalid");
            PrintMessageToPlayer(captain, Core.Localizer["error.invalidPlayerPicked", pickedPlayerName]);
            PromptCaptainToPickPlayer(captain, Team.T);
            return;
        }

        pickedTPlayers.Add(player);

        if (IsBot(player))
        {
            player.SwitchTeam(Team.T);
        }
        player.ChangeTeam(Team.T);

        // Close active menu if any
        var currentMenu = Core.MenusAPI.GetCurrentMenu(captain);
        if (currentMenu != null)
        {
            Core.MenusAPI.CloseMenuForPlayer(captain, currentMenu);
        }

        logger.LogInformation($"AssignPickedPlayerToTeamT: {captain.Controller!.PlayerName} picked {player.Controller!.PlayerName} for T team.");
        PrintMessageToAllPlayers(Core.Localizer["teamPicking.pickedMemberT", captain.Controller!.PlayerName, player.Controller!.PlayerName]);

        if (pickedCtPlayers.Count + pickedTPlayers.Count >= cfg.MinimumReadyPlayers)
        {
            StartKnifeRound();
            return;
        }

        PromptCaptainToPickPlayer(captainCt, Team.CT);
        CloseMenuForPlayer(captain);
    }

    /// <summary>
    /// Moves players to their designated teams before the picking phase begins.
    /// </summary>
    private void MovePlayersToDesignatedTeamsPrePick()
    {
        logger.LogInformation("MovePlayersToDesignatedTeamsPrePick");
        var players = GetPlayingPlayers();
        players.RemoveAll(p => pickedCtPlayers.Contains(p) || pickedTPlayers.Contains(p));

        foreach (var player in players)
        {
            if (IsBot(player))
            {
                continue;
            }
            logger.LogInformation($"Moving {player.Controller!.PlayerName} to SPEC");
            player.ChangeTeam(Team.Spectator);
        }

        foreach (var player in pickedCtPlayers)
        {
            logger.LogInformation($"Moving {player.Controller!.PlayerName} to CT");
            if (IsBot(player))
            {
                player.SwitchTeam(Team.CT);
            }
            player.ChangeTeam(Team.CT);
        }

        foreach (var player in pickedTPlayers)
        {
            logger.LogInformation($"Moving {player.Controller!.PlayerName} to T");
            if (IsBot(player))
            {
                player.SwitchTeam(Team.T);
            }
            player.ChangeTeam(Team.T);
        }
    }
}
