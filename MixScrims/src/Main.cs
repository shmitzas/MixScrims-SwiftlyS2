using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace MixScrims;

[PluginMetadata(
    Id = "MixScrims",
    Version = "1.0.0",
    Name = "MixScrims",
    Author = "Shmitzas",
    Description = "A plugin for PUGS style matches, everything managed in-game instead of external panels or integrations."
)]
public sealed partial class MixScrims(ISwiftlyCore core) : BasePlugin(core)
{
    private enum MatchState
    {
        Ended,
        KnifeRound,
        MapChosen,
        MapLoading,
        MapVoting,
        Match,
        PickingStartingSide,
        PickingTeam,
        Timeout,
        Reset,
        Warmup
    }

    private enum PluginState
    {
        Staging,
        Production
    }

    private MatchState matchState = MatchState.Warmup;
    private PluginState pluginState = PluginState.Staging;
    public static new ISwiftlyCore Core { get; private set; } = null!;
    private ILogger<MixScrims> logger = null!;
    private IOptions<Config> cfgOptions = null!;
    private Config cfg = new();

    public override void Load(bool hotReload)
    {
        Core = base.Core;
        Core.Registrator.Register(this);

        LoadConfig();
        pluginState = cfg.TestMode ? PluginState.Staging : PluginState.Production;

        RegisterListeners();
        ResetVariables();
        RegisterCommandAliases();
    }

    public override void Unload()
    {
        logger?.LogInformation("MixScrims unloading.");
    }

    /// <summary>
    /// Registers all listeners used by the plugin.
    /// </summary>
    private void RegisterListeners()
    {
        RegisterWarmupListeners();
        RegisterStateAgnosticListeners();
    }

    /// <summary>
    /// Registers predefined command aliases with aliases from config.
    /// </summary>
    private void RegisterCommandAliases()
    {
        foreach(var alias in cfg.CommandAliases)
        {
            foreach(var command in alias.Value)
            {
                Core.Command.RegisterCommandAlias(alias.Key, command);
            }
        }
    }

    /// <summary>
    /// Loads the configuration and initializes dependency injection services
    /// </summary>
    private void LoadConfig()
    {
        try
        {
            const string fileName = "config.jsonc";
            const string section = "MixScrims";

            Core.Configuration
                .InitializeJsonWithModel<Config>(fileName, section)
                .Configure(builder =>
                {
                    builder.AddJsonFile(
                        Core.Configuration.GetConfigPath(fileName),
                        optional: false,
                        reloadOnChange: true
                    );
                });

            ServiceCollection services = new();
            services
                .AddSwiftly(Core, addLogger: true, addConfiguration: true)
                .AddOptionsWithValidateOnStart<Config>()
                .BindConfiguration(section);

            var provider = services.BuildServiceProvider();

            logger = provider.GetRequiredService<ILogger<MixScrims>>();
            cfgOptions = provider.GetRequiredService<IOptions<Config>>();
            cfg = cfgOptions.Value;
            pluginState = cfg.TestMode ? PluginState.Staging : PluginState.Production;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load MixScrims configuration/services.");
        }
    }
}