namespace MixScrims;

public class Config
{
    public List<DiscordInvite> DiscordInviteWebhooks { get; set; } = [
        new() {
            Message = "<&role_id> +{0} ||| `connect {1}`",
            WebhookUrl = "https://discord.com/api/webhooks/webhook_token"
        }
    ];
    public int DiscordInviteDelayMinutes { get; set; } = 5;
    public int MinimumReadyPlayers { get; set; } = 10;
    public int DisallowVotePreviousMaps { get; set; } = 2;
    public int DefaultVoteTimeSeconds { get; set; } = 30;
    public int TimeoutDurationSeconds { get; set; } = 60;
    public int Timeouts { get; set; } = 3;
    public bool TestMode { get; set; } = false;
    public AnnouncementTimers ChatAnnouncementTimers { get; set; } = new();
    public List<string> CommandRemindersLocalization { get; set; } =
    [
        "timeout",
        "ready",
        "invite"
    ];
    public Dictionary<string, List<string>> CommandAliases { get; set; } = new()
    {
        // Admin commands
        { "mix_reset", ["reset"] },
        { "mix_start", ["start"] },
        { "forceready", ["fr"] },
        { "captain", ["cap", "capt"] },
        { "map", ["changemap"] },
        { "maps", ["maplist"] },
        { "maplist_all", ["allmaps", "maps_all"] },
    
        // Player commands
        { "ready", ["r"] },
        { "unready", ["u", "ur"] },
        { "revote", ["rv"] },
        { "timeout", ["pause"] },
        { "invite", ["inv"] },
        { "stay", ["st"] },
        { "switch", ["swap"] }
    };
    public List<MapDetails> Maps { get; set; } =
    [
        new() { MapName = "de_mirage", DisplayName = "Mirage", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_dust2", DisplayName = "Dust2", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_inferno", DisplayName = "Inferno", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_anubis", DisplayName = "Anubis", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_overpass", DisplayName = "Overpass", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_ancient", DisplayName = "Ancient", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_ancient_night", DisplayName = "Ancient Night", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_nuke", DisplayName = "Nuke", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false },
        new() { MapName = "de_vertigo", DisplayName = "Vertigo", WorkshopId = "", CanBeNominated = true, IsWorkshopMap = false }
    ];
}

public class MapDetails
{
    public string MapName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkshopId { get; set; } = string.Empty;
    public bool CanBeNominated { get; set; } = true;
    public bool IsWorkshopMap { get; set; } = false;
}

public class VotedMap
{
    public MapDetails Map { get; set; } = new();
    public List<int> VotedBy { get; set; } = [];
    public int Votes { get; set; } = 0;
}

public class DiscordInvite
{
    public string Message { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
}

public class AnnouncementTimers
{
    public int PlayersReadyStatus { get; set; } = 30;
    public int CaptainsAnnouncements { get; set; } = 30;
    public int CommandReminders { get; set; } = 320;
}

