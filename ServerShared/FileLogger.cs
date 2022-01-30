﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileFlows.ServerShared
{
    public class FileLogger : FileFlows.Plugin.ILogger
    {
        private string logFile;

        private string LogPrefix;
        private string LoggingPath;

        private DateOnly LogDate = DateOnly.MinValue;

        public FileLogger(string loggingPath, string logPrefix)
        {
            this.LoggingPath = loggingPath;
            this.LogPrefix = LogPrefix;


        }

        private enum LogType { Error, Warning, Debug, Info }
        private void Log(LogType type, object[] args)
        {
            if(DateOnly.FromDateTime(DateTime.Now) != LogDate)
            {
                // need a new log file
                SetLogFile();
            }

            string message = type + " -> " + string.Join(", ", args.Select(x =>
                x == null ? "null" :
                x.GetType().IsPrimitive ? x.ToString() :
                x is string ? x.ToString() :
                System.Text.Json.JsonSerializer.Serialize(x)));
            Console.WriteLine(message);
            System.IO.File.AppendAllText(logFile, message + Environment.NewLine);
        }

        public void ILog(params object[] args) => Log(LogType.Info, args);
        public void DLog(params object[] args) => Log(LogType.Debug, args);
        public void WLog(params object[] args) => Log(LogType.Warning, args);
        public void ELog(params object[] args) => Log(LogType.Error, args);

        static FileFlows.Plugin.ILogger _Instance;
        public static FileFlows.Plugin.ILogger Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new Logger();
                return _Instance;
            }
        }

        private void SetLogFile()
        {
            this.LogDate = DateOnly.FromDateTime(DateTime.Now);
            this.logFile = Path.Combine(LoggingPath, LogPrefix + "_" + LogDate.ToString("mmmdd") + ".log");
        }
    }
}
