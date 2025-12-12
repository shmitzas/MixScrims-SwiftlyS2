using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace MixScrims;

partial class MixScrims
{
	[Command("ready")]
	/// <summary>
	/// Marks player as ready if they are not already ready. If they are ready, they get informed that they are already ready
	/// </summary>
	public void OnReady(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnReady: command can only be used by players");
			return;
        }

        var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnReady: player is invalid");
			return;
		}
		AddPlayerToReadyList(player, true);
	}

	/// <summary>
	/// Marks player as unready if they were ready (for example if a player disconnects while being ready)
	/// </summary>
	[Command("unready")]
	public void OnUnReady(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnUnReady: command can only be used by players");
			return;
        }

        var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnUnReady: player is invalid");
			return;
		}
		RemovePlayerFromReadyList(player, true);
	}

	/// <summary>
	/// Players can revote map pick if the map picking is not over yet
	/// </summary>
	[Command("revote")]
	public void OnRevote(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnRevote: command can only be used by players");
			return;
        }

            var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnRevote: player is invalid");
			return;
		}

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        if (matchState == MatchState.MapVoting)
		{
			if (mapVotingMenu == null)
			{
				logger.LogError("OnRevote: mapVotingMenu is null");
				PrintMessageToPlayer(player, localizer["command.invalidState", "revote"]);
				return;
			}
			DisplayMapVotingMenu(player);
			return;
		}
		PrintMessageToPlayer(player, localizer["command.invalidState", "revote"]);
	}

	/// <summary>
	/// Players can call timeout if they have timeouts left and the match is in progress
	/// </summary>
	[Command("timeout")]
	public void OnTimeout(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnTimeout: command can only be used by players");
			return;
        }

        var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnTimeout: player is invalid");

			return;
		}

		if (player.PlayerPawn == null)
		{
			logger.LogError("OnTimeout: PlayerPawn is null for player {PlayerName}", player.Controller?.PlayerName);
			return;
        }

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        if (matchState != MatchState.Match)
		{
			PrintMessageToPlayer(player, localizer["command.invalidState", "timeout"]);
			return;
        }

		if (timeoutPending != TimeoutPending.None)
		{
			PrintMessageToPlayer(player, localizer["error.timeoutPending"]);
			return;
        }

        var team = (Team)player.PlayerPawn.TeamNum;
		
		if (team == Team.CT)
		{
			if (timeoutCountCt < 1)
			{
                PrintMessageToPlayer(player, localizer["error.noTimeoutsLeft", timeoutCountT, cfg.Timeouts]);
                return;
            }
			StartTimeoutVote(player, Team.CT);
        }

        if (team == Team.T)
        {
            if (timeoutCountT < 1)
            {
                PrintMessageToPlayer(player, localizer["error.noTimeoutsLeft", timeoutCountT, cfg.Timeouts]);
                return;
            }
            StartTimeoutVote(player, Team.T);
        }
    }

	/// <summary>
	/// Sends an invite message to the discord webhook
	/// </summary>
	[Command("invite")]
	public void OnInvite(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnKviesti: command can only be used by players");
            return;
		}

        var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnKviesti: player is invalid");
			return;
		}

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        var timeSinceLastInvite = DateTime.Now - lastDiscordInviteSentAt;
        var timeRemaining = TimeSpan.FromMinutes(cfg.DiscordInviteDelayMinutes) - timeSinceLastInvite;

        if (timeRemaining > TimeSpan.Zero)
        {
            // Format as "X minutes Y seconds"
            int minutes = (int)timeRemaining.TotalMinutes;
            int seconds = timeRemaining.Seconds;
            string formattedTime = $"{minutes}min {seconds}s";

            PrintMessageToPlayer(player, localizer["command.inviteToEarly", formattedTime]);
            return;
        }

        int playingPlayers = GetPlayers().Count;

        int remainingPlayers = cfg.MinimumReadyPlayers - playingPlayers;

		if (remainingPlayers < 1)
		{
			PrintMessageToPlayer(player, localizer["command.sendInviteNoNeed", playingPlayers, cfg.MinimumReadyPlayers]);
			return;
        }

        _ = Task.Run(async () =>
		{
			foreach (var webhook in cfg.DiscordInviteWebhooks)
			{
				string message = webhook.Message
					.Replace("{0}", remainingPlayers.ToString());
				await SendToDiscord(message, webhook.WebhookUrl);
			}
		});

        lastDiscordInviteSentAt = DateTime.Now;
        PrintMessageToAllPlayers(localizer["command.sendInvite", player.Controller.PlayerName]);
	}

	/// <summary>
	/// Additional way of chosing wheter to stay or switch teams after knife round
	/// </summary>
	[Command("stay")]
	public void OnStay(ICommandContext context)
	{
		if (!context.IsSentByPlayer)
		{
			logger.LogError("OnStay: command can only be used by players");
			return;
        }

        var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnStay: player is invalid");
			return;
		}

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        if (matchState != MatchState.PickingStartingSide)
		{
			PrintMessageToPlayer(player, localizer["command.invalidState", "sidePick"]);
			return;
		}

		if (player != winnerCaptain)
		{
			PrintMessageToPlayer(player, localizer["error.notCaptain"]);
			return;
		}

        if (!IsBot(player) && IsPlayerValid(player))
        {
            var menu = Core.MenusAPI.GetCurrentMenu(player);
			if (menu != null)
			{
                Core.MenusAPI.CloseMenuForPlayer(player, menu);
            }
        }

        var token = Core.Scheduler.DelayBySeconds(1, () => StayStartingSides(player));
        Core.Scheduler.StopOnMapChange(token);
        logger.LogInformation($"OnStay: Captain {player.Controller.PlayerName} chose to !stay");
	}

	/// <summary>
	/// Additional way of chosing wheter to stay or switch teams after knife round
	/// </summary>
	[Command("switch")]
	public void OnSwitch(ICommandContext context)
	{
		var player = context.Sender;
        if (player == null || !IsPlayerValid(player))
		{
			logger.LogError("OnStay: player is invalid");
			return;
		}

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        if (matchState != MatchState.PickingStartingSide)
		{
			PrintMessageToPlayer(player, localizer["command.invalidState", "sidePick"]);
			return;
		}

		if (player != winnerCaptain)
		{
			PrintMessageToPlayer(player, localizer["error.notCaptain"]);
			return;
		}

        if (!IsBot(player) && IsPlayerValid(player))
        {
            var menu = Core.MenusAPI.GetCurrentMenu(player);
            if (menu != null)
            {
                Core.MenusAPI.CloseMenuForPlayer(player, menu);
            }
        }

        var token = Core.Scheduler.DelayBySeconds(1, () => SwitchStartingSides(player));
		Core.Scheduler.StopOnMapChange(token);
        logger.LogInformation($"OnStay: Captain {player.Controller.PlayerName} chose to !switch");
    }
}
