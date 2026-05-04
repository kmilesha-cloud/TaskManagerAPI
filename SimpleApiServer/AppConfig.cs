using System.Collections.Generic;

namespace SimpleApiServer
{
    internal class AppConfig
    {
        public List<string> ListenUrls { get; set; } = new List<string>();
        public string LogLevel { get; set; } = "Information";
        public string LogFilePath { get; set; } = "logs/app.log";
    }
}