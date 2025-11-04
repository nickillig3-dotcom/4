using System;
using System.IO;
using System.Text.Json;

namespace AutoShortsPro.App.Services
{
    public static class SettingsService
    {
        public class Model
        {
            public int BlurKernel { get; set; } = 35;
            public bool Pixelate { get; set; } = false;
            public bool PreferDnn { get; set; } = false;
            public bool ReviewImages { get; set; } = false;
            public string? OutputDir { get; set; } = null; // leer/null = gleicher Ordner wie Eingabe
        }

        private static string Dir  => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GDPRBlurPro");
        private static string File => Path.Combine(Dir, "settings.json");

        public static Model Load()
        {
            try
            {
                if (System.IO.File.Exists(File))
                {
                    var json = System.IO.File.ReadAllText(File);
                    return JsonSerializer.Deserialize<Model>(json) ?? new Model();
                }
            }
            catch { }
            return new Model();
        }

        public static void Save(Model m)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(File, json);
            }
            catch { }
        }
    }
}
