using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Controls;
// Alias für OpenCvSharp.Rect, keine globale using OpenCvSharp (vermeidet Konflikte)
using CvRect = OpenCvSharp.Rect;

namespace AutoShortsPro.App.Views
{
    public partial class ImageReviewWindow : System.Windows.Window
    {
        private readonly string _path;
        private readonly bool _preferDnn;

        private System.Windows.Point? _dragStart;
        private WpfRectangle? _currentRectShape;

        public List<CvRect> ResultRects { get; private set; } = new();

        public ImageReviewWindow(string imagePath, bool preferDnn)
        {
            InitializeComponent();
            _path = imagePath;
            _preferDnn = preferDnn;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(imagePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            Img.Source = bmp;

            Overlay.Width = bmp.PixelWidth;
            Overlay.Height = bmp.PixelHeight;

            try
            {
                using var m = OpenCvSharp.Cv2.ImRead(imagePath);
                var boxes = Services.BlurEngine.DetectRegions(m, _preferDnn);
                foreach (var r in boxes) AddRectShape(r);
                UpdateCount();
            }
            catch { }
        }

        private void AddRectShape(CvRect r)
        {
            var shape = new Rectangle
            {
                Width = r.Width,
                Height = r.Height,
                Stroke = Brushes.Yellow,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0))
            };
            Canvas.SetLeft(shape, r.X);
            Canvas.SetTop(shape, r.Y);
            Overlay.Children.Add(shape);
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(Overlay);
            _currentRectShape = new Rectangle
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255))
            };
            Overlay.Children.Add(_currentRectShape);
            CaptureMouse();
        }

        private void Overlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStart == null || _currentRectShape == null) return;
            var p0 = _dragStart.Value;
            var p1 = e.GetPosition(Overlay);

            var x = Math.Min(p0.X, p1.X);
            var y = Math.Min(p0.Y, p1.Y);
            var w = Math.Abs(p1.X - p0.X);
            var h = Math.Abs(p1.Y - p0.Y);

            if (w < 1 || h < 1) return;

            Canvas.SetLeft(_currentRectShape, x);
            Canvas.SetTop(_currentRectShape, y);
            _currentRectShape.Width = w;
            _currentRectShape.Height = h;
        }

        private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragStart == null || _currentRectShape == null) { ReleaseMouseCapture(); return; }

            _currentRectShape.Stroke = Brushes.Yellow;
            _currentRectShape.Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0));

            _dragStart = null;
            _currentRectShape = null;
            ReleaseMouseCapture();
            UpdateCount();
        }

        private void Overlay_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && hit is not WpfRectangle) hit = VisualTreeHelper.GetParent(hit);
            if (hit is WpfRectangle rect)
            {
                Overlay.Children.Remove(rect);
                UpdateCount();
            }
        }

        private void UpdateCount()
        {
            int n = Overlay.Children.OfType<Rectangle>().Count();
            CountText.Text = $"Boxen: {n}";
        }

        private List<CvRect> CollectRects()
        {
            var list = new List<CvRect>();
            foreach (var s in Overlay.Children.OfType<Rectangle>())
            {
                var x = (int)Math.Round(Canvas.GetLeft(s));
                var y = (int)Math.Round(Canvas.GetTop(s));
                var w = (int)Math.Round(s.Width);
                var h = (int)Math.Round(s.Height);
                if (w > 2 && h > 2)
                    list.Add(new CvRect(x, y, w, h));
            }
            return list;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            ResultRects = CollectRects();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}


