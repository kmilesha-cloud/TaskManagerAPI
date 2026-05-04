using System;
using System.IO;

namespace SimpleApiServer
{
    internal static class Logger
    {
        public static string LogFilePath = "logs/app.log";

        public static void LogInfo(string message)
        {
            Write("INFO", message);
        }

        public static void LogError(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

            Console.WriteLine(line);

            try
            {
                string directory = Path.GetDirectoryName(LogFilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}