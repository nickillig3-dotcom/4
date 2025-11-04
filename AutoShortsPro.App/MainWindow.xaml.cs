using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AutoShortsPro.App.Services;
using AutoShortsPro.App.Views;
using WinForms = System.Windows.Forms;

namespace AutoShortsPro.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            try
            {
                var s = SettingsService.Load();
                BlurSlider.Value               = s.BlurKernel;
                PixelateCheck.IsChecked        = s.Pixelate;
                DnnFaceCheck.IsChecked         = s.PreferDnn;
                ReviewImagesCheck.IsChecked    = s.ReviewImages;
                if (!string.IsNullOrWhiteSpace(s.OutputDir)) OutDirBox.Text = s.OutputDir!;
            }
            catch { }

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var s = new SettingsService.Model
                {
                    BlurKernel   = (int)BlurSlider.Value,
                    Pixelate     = PixelateCheck.IsChecked == true,
                    PreferDnn    = DnnFaceCheck.IsChecked == true,
                    ReviewImages = ReviewImagesCheck.IsChecked == true,
                    OutputDir    = string.IsNullOrWhiteSpace(OutDirBox.Text) ? null : OutDirBox.Text
                };
                SettingsService.Save(s);
            }
            catch { }
        }

        private async void PickFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Bilder/Videos|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.mp4;*.mov;*.avi;*.mkv;*.wmv|Alle Dateien|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                await ProcessPathsAsync(dlg.FileNames);
            }
        }

        private async void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                await ProcessPathsAsync(dropped);
            }
        }

        private void BrowseOutDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var fb = new WinForms.FolderBrowserDialog();
                fb.Description = "Ausgabeordner wählen (leer lassen = neben Eingabedatei)";
                fb.ShowNewFolderButton = true;
                if (fb.ShowDialog() == WinForms.DialogResult.OK)
                {
                    OutDirBox.Text = fb.SelectedPath;
                }
            }
            catch { }
        }

        private void ClearOutDir_Click(object sender, RoutedEventArgs e)
        {
            OutDirBox.Text = string.Empty;
        }

        private static bool IsVideo(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv" }.Contains(ext);
        }

        private static IEnumerable<string> ExpandToMediaFiles(IEnumerable<string> paths)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".mp4", ".mov", ".avi", ".mkv", ".wmv" };

            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    foreach (var f in Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                                               .Where(f => exts.Contains(Path.GetExtension(f))))
                        yield return f;
                }
                else if (File.Exists(p) && exts.Contains(Path.GetExtension(p)))
                {
                    yield return p;
                }
            }
        }

        private static string GetOutPath(string inputPath, string? overrideDir)
        {
            var dir = string.IsNullOrWhiteSpace(overrideDir)
                ? (Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory)
                : overrideDir!;
            Directory.CreateDirectory(dir);
            var fn = Path.GetFileNameWithoutExtension(inputPath);
            var ext = Path.GetExtension(inputPath);
            return Path.Combine(dir, fn + "_blurred" + ext);
        }

        private async Task ProcessPathsAsync(IEnumerable<string> paths)
        {
            var files = ExpandToMediaFiles(paths).ToList();
            if (files.Count == 0)
            {
                StatusText.Text = "Keine passenden Dateien gefunden.";
                return;
            }

            Progress.Value = 0;
            StatusText.Text = $"Starte… (0/{files.Count})";

            bool addWatermark = !LicenseService.IsPro;
            bool preferDnn    = DnnFaceCheck.IsChecked == true;
            bool reviewImages = ReviewImagesCheck.IsChecked == true;
            string? outDir    = string.IsNullOrWhiteSpace(OutDirBox.Text) ? null : OutDirBox.Text;

            int i = 0;
            foreach (var f in files)
            {
                var outPath = GetOutPath(f, outDir);
                try
                {
                    if (IsVideo(f))
                    {
                        await Task.Run(() => VideoProcessor.ProcessVideo(
                            f, outPath, (int)BlurSlider.Value, PixelateCheck.IsChecked == true, addWatermark, preferDnn));
                    }
                    else
                    {
                        if (reviewImages)
                        {
                            System.Collections.Generic.List<OpenCvSharp.Rect>? rects = null;
                            await Dispatcher.InvokeAsync(() =>
                            {
                                var win = new ImageReviewWindow(f, preferDnn) { Owner = this };
                                var ok = win.ShowDialog() == true;
                                if (ok) rects = win.ResultRects;
                            });

                            if (rects != null)
                            {
                                await Task.Run(() => BlurEngine.ProcessImageWithRects(
                                    f, outPath, rects!, (int)BlurSlider.Value, PixelateCheck.IsChecked == true, addWatermark));
                            }
                        }
                        else
                        {
                            await Task.Run(() => BlurEngine.ProcessImage(
                                f, outPath, (int)BlurSlider.Value, PixelateCheck.IsChecked == true, addWatermark, preferDnn));
                        }
                    }
                }
                catch (Exception)
                {
                    StatusText.Text = $"Fehler bei: {System.IO.Path.GetFileName(f)}";
                }

                i++;
                Progress.Value = (double)i / files.Count * 100.0;
                StatusText.Text = $"Fertig: {i}/{files.Count}";
            }

            System.Windows.MessageBox.Show("Verarbeitung abgeschlossen.", "GDPR Blur Pro");
        }

        private void BuyPro_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://www.paypal.com/";
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        private void LoadLicense_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Lizenzdatei|*.lic;*.json|Alle Dateien|*.*" };
            if (dlg.ShowDialog() == true)
            {
                if (LicenseService.LoadAndVerify(dlg.FileName))
                    System.Windows.MessageBox.Show("Lizenz akzeptiert. Pro aktiviert.", "Lizenz");
                else
                    System.Windows.MessageBox.Show("Lizenz ungültig.", "Lizenz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version?.ToString() ?? "n/a";
                System.Windows.MessageBox.Show($"GDPR Blur Pro\nVersion {ver}\n© 2025", "Über");
            }
            catch { }
        }
    }
}
