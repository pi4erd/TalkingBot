﻿using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TalkingBot.Core;
using Microsoft.Extensions.DependencyInjection;
using Victoria.Player;
using Victoria.Node;
using Victoria;
using TalkingBot.Utils;
using TalkingBot.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TalkingBot.Core.Music;

namespace TalkingBot
{
    public class TalkingBotClient : IDisposable
    {
        public const int Branch = 2;
        public const int Commit = 1;
        public const int Tweak = 3;
        public const bool IsBuilt = false;

        public static LavaNode _lavaNode;
        public static DiscordSocketClient _client;
        private static DiscordSocketConfig _config;
        private static TalkingBotConfig _talbConfig;
        private static SlashCommandHandler _handler;

        public TalkingBotClient(TalkingBotConfig tbConfig, DiscordSocketConfig? clientConfig = null)
        {
            _talbConfig = tbConfig;
            _config = new DiscordSocketConfig() {
                MessageCacheSize = 100,
                UseInteractionSnowflakeDate = true,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildPresences
            };
            if (clientConfig != null) _config = clientConfig;

            _handler = CommandsContainer.BuildHandler();

            _client = new(_config);
            _client.Log += Log;

            _client.MessageUpdated += MessageUpdated;

            _client.Ready += async () =>
            {
                await Log(new(LogSeverity.Info, "TalkingBotClient.Ready()", 
                    $"Logged in as {_client.CurrentUser.Username}!"));
            };
            _client.UserVoiceStateUpdated += OnUserVoiceUpdate;
            _client.SlashCommandExecuted += _handler.HandleCommands;

            LavaLogger logger = new LavaLogger(LogLevel.Information);
            
            _lavaNode = new(_client, new(){
                Hostname = "localhost",
                Port = 2333,
                Authorization = "youshallnotpass",
                SelfDeaf = false,
                SocketConfiguration = new() {
                    ReconnectAttempts = 3, 
                    ReconnectDelay = TimeSpan.FromSeconds(5), 
                    BufferSize = 1024
                },
                IsSecure = false
            }, logger);

            ServiceCollection collection = new();
            collection.AddSingleton(_client);
            collection.AddSingleton<AudioManager>();
            collection.AddSingleton(_handler);
            collection.AddSingleton(_lavaNode);
            //collection.AddLavaNode(conf => {
            //    conf.Hostname = "localhost";
            //    conf.Port = 2333;
            //    conf.Authorization = "youshallnotpass";
            //    conf.SelfDeaf = false;
            //    conf.SocketConfiguration = new() {
            //        ReconnectAttempts = 3, 
            //        ReconnectDelay = TimeSpan.FromSeconds(5), 
            //        BufferSize = 1024
            //    };
            //});
            collection.AddSingleton(logger);
            ServiceManager.SetProvider(collection);

            RandomStatic.Initialize();

            Logger.Initialize(LogLevel.Debug);
        }
        
        public async Task OnUserVoiceUpdate(SocketUser user, SocketVoiceState prevVs, SocketVoiceState newVs) {
            if(user is not SocketGuildUser guildUser) return;

            SocketVoiceChannel channel = prevVs.VoiceChannel;

            if(channel != null && channel.Id != newVs.VoiceChannel.Id) {
                var clientUser = channel.GetUser(_client.CurrentUser.Id);
                if(clientUser != null) {
                    IGuildUser bot = clientUser as IGuildUser;
                    var users = channel.ConnectedUsers;
                    if(bot.VoiceChannel != null && bot.VoiceChannel.Id == channel.Id && users.Count == 1) {
                        await bot.VoiceChannel!.DisconnectAsync();
                    }
                }
            }
        }

        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, _talbConfig.Token);
            await _client.StartAsync();

            Stopwatch sw = new();

            while (_client.ConnectionState != ConnectionState.Connected) await Task.Delay(10);

            foreach(ulong guildid in _talbConfig.Guilds)
            {
                sw.Restart();
                await _handler.BuildCommands(_client, guildid);
                sw.Stop();
                await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", 
                    $"Commands ({_handler.GetLength()} in total) built successfully for {_client.GetGuild(guildid).Name} ({guildid}) in "+
                    $"{sw.Elapsed.TotalSeconds}s."));
            }
            await _client.SetActivityAsync(
                new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
            
            try
            {
                await VictoriaExtensions.UseLavaNodeAsync(ServiceManager.ServiceProvider);
                await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", "Lavalink connected"));
            }
            catch (Exception ex)
            {
                await Log(new(LogSeverity.Critical, "TalkingBotClient.Run()", "Lavalink connection failed!", ex));
            }

            await Task.Delay(-1);
        }
        private async Task MessageUpdated(Cacheable<IMessage, ulong> before, 
            SocketMessage after, ISocketMessageChannel channel)
        {
            var message = await before.GetOrDownloadAsync();
            Logger.Instance?.LogDebug($"Message update: {message} -> {after}");
        }
        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
        private Task Log(LogMessage message)
        {
            if (Logger.Instance is null) throw new NullReferenceException("Logger instance was null");

            LogSeverity sev = message.Severity;
            if (sev == LogSeverity.Error)
                Logger.Instance.Log(LogLevel.Error, message.Exception, message.Message);
            else if (sev == LogSeverity.Warning)
                Logger.Instance.Log(LogLevel.Warning, message.Exception, message.Message);
            else if(sev == LogSeverity.Critical)
                Logger.Instance.Log(LogLevel.Critical, message.Exception, message.Message);
            else if(sev == LogSeverity.Info)
                Logger.Instance.Log(LogLevel.Information, message.Exception, message.Message);
            else if(sev == LogSeverity.Debug)
                Logger.Instance.Log(LogLevel.Debug, message.Exception, message.Message);

            return Task.CompletedTask;
        }
    }
}
