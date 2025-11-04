using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoShortsPro.App.Services
{
    public static class CliProcessor
    {
        public static int Run(string[] args)
        {
            string? input = null;
            string? outputDir = null;
            string? licensePath = null;
            bool pixelate = false;
            bool recursive = false;
            bool dnnFace = false;
            int kernel = 35;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "-i":
                    case "--input":
                        input = Next(args, ref i);
                        break;
                    case "-o":
                    case "--output":
                        outputDir = Next(args, ref i);
                        break;
                    case "-k":
                    case "--kernel":
                        int.TryParse(Next(args, ref i), out kernel);
                        break;
                    case "--pixelate":
                        pixelate = true;
                        break;
                    case "-r":
                    case "--recursive":
                        recursive = true;
                        break;
                    case "--license":
                        licensePath = Next(args, ref i);
                        break;
                    case "--dnn-face":
                        dnnFace = true;
                        break;
                    case "-h":
                    case "--help":
                    case "/?":
                        PrintHelp();
                        return 0;
                }
            }

            if (!string.IsNullOrWhiteSpace(licensePath) && File.Exists(licensePath))
            {
                try { LicenseService.LoadAndVerify(licensePath); } catch { }
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintHelp(error: true);
                return 2;
            }

            var files = ExpandToMediaFiles(input!, recursive).ToList();
            if (files.Count == 0)
            {
                FileLogger.Log("CLI: keine passenden Dateien gefunden.");
                return 2;
            }

            bool watermark = !LicenseService.IsPro;
            int ok = 0, fail = 0;
            foreach (var f in files)
            {
                try
                {
                    var outPath = MakeOutPath(f, outputDir);
                    if (IsVideo(f))
                        VideoProcessor.ProcessVideo(f, outPath, kernel, pixelate, watermark, dnnFace);
                    else
                        BlurEngine.ProcessImage(f, outPath, kernel, pixelate, watermark, dnnFace);
                    ok++;
                    FileLogger.Log($"OK: {f} -> {outPath}");
                }
                catch (Exception ex)
                {
                    fail++;
                    FileLogger.Log($"ERR: {f} -> {ex.Message}");
                }
            }

            FileLogger.Log($"CLI fertig. OK={ok} Fehler={fail}");
            return fail > 0 ? 1 : 0;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) return "";
            i++;
            return args[i];
        }

        private static IEnumerable<string> ExpandToMediaFiles(string path, bool recursive)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".mp4", ".mov", ".avi", ".mkv", ".wmv" };

            if (Directory.Exists(path))
            {
                var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opt))
                    if (exts.Contains(Path.GetExtension(f))) yield return f;
                yield break;
            }

            if (File.Exists(path) && exts.Contains(Path.GetExtension(path)))
                yield return path;
        }

        private static bool IsVideo(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv" }.Contains(ext);
        }

        private static string MakeOutPath(string input, string? outputDir)
        {
            var dir = outputDir;
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.GetDirectoryName(input)!;

            Directory.CreateDirectory(dir);
            var fn = Path.GetFileNameWithoutExtension(input);
            var ext = Path.GetExtension(input);
            return Path.Combine(dir, fn + "_blurred" + ext);
        }

        private static void PrintHelp(bool error = false)
        {
            var t = @"GDPR Blur Pro – CLI
Usage:
  app.exe --input <file|folder> [--output <folder>] [--pixelate] [--kernel 35] [--recursive] [--license path] [--dnn-face]
Examples:
  app.exe -i ""C:\media\foto.jpg"" --dnn-face
  app.exe -i ""C:\media\ordner"" -o ""C:\out"" -r --pixelate -k 45 --license C:\key.lic
Logs: %LOCALAPPDATA%\GDPRBlurPro\logs\app.log";
            try { FileLogger.Log(t); } catch { }
            try { Console.WriteLine(t); } catch { }
        }
    }
}
