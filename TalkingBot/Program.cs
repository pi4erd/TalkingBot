﻿using Discord.Interactions;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using TalkingBot.Core;
using TalkingBot;
using Discord.WebSocket;

namespace TalkingBotMain
{
    internal class Program
    {
        public static Task Main(string[] args) => MainAsync(args);

        private static async Task MainAsync(string[] args)
        {
            string cnfpath = "Config.json";
            string jsonconfig = "";
            if(args.Length > 1) 
            {
                int idx = Array.FindIndex(args, 0, args.Length, str => str == "-C");

                try
                {
                    if (idx != -1) cnfpath = args[idx + 1];
                } catch(ArgumentOutOfRangeException)
                {
                    Console.Error.WriteLine("Expected string after '-C'. Got null");
                    Environment.Exit(-1);
                }
            }
            if(!File.Exists(cnfpath))
            {
                Console.WriteLine("Config not found at: {0}\nCreating new one...", cnfpath);
                await Config.CreateDefaultConfig(cnfpath);
            }
            jsonconfig = File.ReadAllText(cnfpath);
            TalkingBotConfig config = JsonConvert.DeserializeObject<TalkingBotConfig>(jsonconfig)!;

            Console.Clear();

            SlashCommandHandler handler = new SlashCommandHandler()
                .AddCommand(new()
                {
                    name = "echo",
                    description = "Echo a message",
                    Handler = HandleEcho,
                    options = new List<SlashCommandOption>
                    {
                        new()
                        {
                            name = "message",
                            description = "Message to echo",
                            optionType = Discord.ApplicationCommandOptionType.String,
                            isRequired = true,
                        }
                    }
                });

            TalkingBotClient client = new(config, handler);

            Console.CancelKeyPress += delegate
            {
                client.Dispose();
                Environment.Exit(0);
            };

            await client.Run();

            await Task.Delay(-1);
        }
        public static async Task HandleEcho(SocketSlashCommand cmd)
        {
            await cmd.RespondAsync((string)cmd.Data.Options.First().Value);
        }
    }
}