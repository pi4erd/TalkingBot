﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalkingBot.Core.Logging
{
    public class Logger : ILogger
    {
        public static Logger? Instance;

        private LogLevel _level;
        private bool enabled;

        public string[] colorCodes = new string[7] {
            "\033[39m", "\033[37m", "\033[39m", "\033[93m", 
            "\033[91m", "\033[31m", "\033[39m"
        };
        public Logger(LogLevel logLevel = LogLevel.Debug)
        {
            _level = logLevel;
            enabled = true;
        }
        public static Logger Initialize(LogLevel logLevel = LogLevel.Debug)
        {
            Logger logger = new(logLevel);

            if (Instance != null) throw new Exception("Instance of Logger already exists!");
            Instance = logger;
            return Instance;
        }
        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            enabled = true;

            throw new NotImplementedException();
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return enabled;
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, 
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel < _level) return;
            Console.WriteLine($"[{DateTime.Now.ToString("T")}] [{logLevel}]: " + formatter(state, exception));

            if (exception is not null)
            {
                Console.WriteLine($"[{DateTime.Now.ToString("T")}] [{logLevel}] " + exception.Message);
                if (_level == LogLevel.Debug) Console.WriteLine($"[{DateTime.Now.ToString("T")}] [{logLevel}] {exception!.InnerException}");
            }
        }
    }
}
