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
        public static LavaNode _lavaNode;
        public static DiscordSocketClient _client;
        private static DiscordSocketConfig _config;
        private static TalkingBotConfig _talbConfig;
        private SlashCommandHandler _handler;

        public TalkingBotClient(TalkingBotConfig tbConfig, DiscordSocketConfig? clientConfig = null)
        {
            _talbConfig = tbConfig;

            _config = new DiscordSocketConfig() {
                MessageCacheSize = 100,
                UseInteractionSnowflakeDate = true,
                AlwaysDownloadUsers = true,
            };
            if (clientConfig != null) _config = clientConfig;

            _handler = CommandsContainer.BuildHandler();

            _client = new(_config);
            _client.Log += Log;

            _client.MessageUpdated += MessageUpdated;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
            _client.Ready += async () =>
            {
                await Log(new(LogSeverity.Info, "TalkingBotClient.Ready()", 
                    $"Logged in as {_client.CurrentUser.Username}!"));
            };
            _client.SlashCommandExecuted += _handler.HandleCommands;

            LavaLogger logger = new LavaLogger(LogLevel.Information);

            _lavaNode = new(_client, new()
            {
                Hostname = "localhost",
                Port = 2333,
                Authorization = "youshallnotpass",
                SelfDeaf = false,
                IsSecure = false,
                SocketConfiguration = new() { ReconnectAttempts = 3, BufferSize = 2048, 
                    ReconnectDelay = TimeSpan.FromSeconds(5) }
            }, logger);

            _client.PresenceUpdated += PresenceUpdated;

            RandomStatic.Initialize();

            Logger.Initialize(LogLevel.Debug);
        }
        private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState vs1, SocketVoiceState vs2)
        {
            //Logger.Instance?.LogDebug(vs1.VoiceChannel?.Equals(vs2).ToString());

            //if (vs1.VoiceChannel.ConnectedUsers.Count == 0)
            //{
            //    await AudioManager.LeaveAsync(vs1.VoiceChannel.Guild);
            //}
        }
        private async Task PresenceUpdated(SocketUser user, SocketPresence oldPresence, SocketPresence newPresence)
        {

        }
        public async Task Run()
        {
            await _client.LoginAsync(TokenType.Bot, _talbConfig.Token);
            await _client.StartAsync();

            while (_client.ConnectionState != ConnectionState.Connected) await Task.Delay(100);

            foreach(ulong guildid in _talbConfig.Guilds)
            {
                await _handler.BuildCommands(_client, guildid);
                await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", 
                    $"Commands built successfully for {_client.GetGuild(guildid).Name} ({guildid})"));
            }
            await _client.SetActivityAsync(
                new Game($"Nothing", ActivityType.Watching, ActivityProperties.Instance));
            
            try
            {
                await _lavaNode.ConnectAsync();
                await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", "Lavalink connecting..."));
            }
            catch (Exception ex)
            {
                await Log(new(LogSeverity.Critical, "TalkingBotClient.Run()", "Lavalink connection failed!", ex));
            }
            await Log(new(LogSeverity.Info, "TalkingBotClient.Run()", "Lavalink connected"));

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