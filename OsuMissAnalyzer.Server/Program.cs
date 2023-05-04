﻿using System;
using System.Threading.Tasks;
using Mono.Unix;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OsuMissAnalyzer.Server.Settings;
using Microsoft.Extensions.DependencyInjection;
using DSharpPlus;
using System.Net.Http;
using OsuMissAnalyzer.Server.Database;
using DSharpPlus.SlashCommands;
using System.Collections.Generic;

namespace OsuMissAnalyzer.Server
{
    public class Program
    {
        static UnixPipes interruptPipe;
        public static async Task Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    IConfiguration configurationRoot = context.Configuration;
                    services.Configure<ServerOptions>(configurationRoot.GetRequiredSection(nameof(ServerOptions)));
                    services.Configure<DiscordOptions>(configurationRoot.GetRequiredSection(nameof(DiscordOptions)));
                    services.Configure<OsuApiOptions>(configurationRoot.GetRequiredSection(nameof(OsuApiOptions)));
                    services.AddSingleton<DiscordConfiguration>((serviceProvider) => new DiscordConfiguration
                    {
                        Token = serviceProvider.GetRequiredService<DiscordOptions>().DiscordToken,
                        TokenType = TokenType.Bot,
                        Intents = DiscordIntents.Guilds |
                            DiscordIntents.GuildMessages |
                            DiscordIntents.GuildMessageReactions |
                            DiscordIntents.DirectMessages |
                            DiscordIntents.DirectMessageReactions |
                            DiscordIntents.MessageContents
                    });
                    services.AddSingleton<ILogger, UnixLogger>();
                    services.AddSingleton<GuildManager>();
                    services.AddHttpClient();

                    services.AddSingleton<ServerBeatmapDb>();
                    services.AddSingleton<ServerReplayDb>();

                    services.AddSingleton<DiscordShardedClient>();
                    services.AddSingleton<SlashCommandsConfiguration>(serviceProvider => new SlashCommandsConfiguration { Services = serviceProvider });
                    services.AddHostedService<ServerContext>();
                })
                .Build();
            var slashSettings = host.Services.GetRequiredService<SlashCommandsConfiguration>();
            var slash = await host.Services.GetRequiredService<DiscordShardedClient>().UseSlashCommandsAsync(slashSettings);
            var settings = host.Services.GetRequiredService<ServerOptions>();
            if (settings.Test)
            {
                slash.RegisterCommands<Commands>(settings.TestGuild);
            }
            else
            {
                slash.RegisterCommands<Commands>();
            }
            foreach (var s in slash) {
                s.Value.SlashCommandErrored += async (d, e) =>
                {
                    await Logger.WriteLine(e.Context.CommandName);
                    await Logger.LogException(e.Exception);
                };
            }

            await Logger.WriteLine("Init complete");

            await host.RunAsync();
        }
    }
}