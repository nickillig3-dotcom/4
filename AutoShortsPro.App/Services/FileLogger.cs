using System;
using System.IO;

namespace AutoShortsPro.App.Services
{
    public static class FileLogger
    {
        private static readonly object _lock = new();
        private static readonly string LogDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GDPRBlurPro", "logs");
        private static string LogPath => Path.Combine(LogDir, "app.log");

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
                lock (_lock) { File.AppendAllText(LogPath, line + Environment.NewLine); }
            }
            catch { }
        }
    }
}
