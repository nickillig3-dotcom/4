using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace AutoShortsPro.App.Services
{
    public static class BlurEngine
    {
        private static readonly string CascadeDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "cascades");

        private static readonly Lazy<CascadeClassifier> FaceCascade = new Lazy<CascadeClassifier>(() =>
            new CascadeClassifier(Path.Combine(CascadeDir, "haarcascade_frontalface_default.xml")));

        private static readonly Lazy<CascadeClassifier> PlateCascade = new Lazy<CascadeClassifier>(() =>
            new CascadeClassifier(Path.Combine(CascadeDir, "haarcascade_russian_plate_number.xml")));

        public static void ProcessImage(string inputPath, string outputPath, int blurKernel = 35, bool pixelate = false)
        {
            using var img = Cv2.ImRead(inputPath);
            if (img.Empty()) throw new Exception("Bild konnte nicht geladen werden: " + inputPath);

            var rects = DetectRegions(img);
            foreach (var r in rects)
                ApplyBlur(img, r, blurKernel, pixelate);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Cv2.ImWrite(outputPath, img);
        }

        public static List<Rect> DetectRegions(Mat frame)
        {
            if (!File.Exists(Path.Combine(CascadeDir, "haarcascade_frontalface_default.xml")) ||
                !File.Exists(Path.Combine(CascadeDir, "haarcascade_russian_plate_number.xml")))
                throw new FileNotFoundException(@"Cascade XMLs fehlen in Assets\cascades");

            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var faces = FaceCascade.Value.DetectMultiScale(gray, 1.1, 4, HaarDetectionTypes.ScaleImage, new Size(24, 24));
            var plates = PlateCascade.Value.DetectMultiScale(gray, 1.1, 4, HaarDetectionTypes.ScaleImage, new Size(24, 24));

            var result = new List<Rect>(faces.Length + plates.Length);
            result.AddRange(faces);
            result.AddRange(plates);
            return result;
        }

        private static void ApplyBlur(Mat img, Rect roi, int blurKernel, bool pixelate)
        {
            var safe = new Rect(
                Math.Max(0, roi.X), Math.Max(0, roi.Y),
                Math.Min(roi.Width, img.Width - roi.X),
                Math.Min(roi.Height, img.Height - roi.Y));

            using var sub = new Mat(img, safe);
            if (!pixelate)
            {
                int k = blurKernel % 2 == 0 ? blurKernel + 1 : blurKernel;
                Cv2.GaussianBlur(sub, sub, new Size(k, k), 0);
            }
            else
            {
                using var tmp = new Mat();
                Cv2.Resize(sub, tmp, new Size(Math.Max(1, sub.Width / 10), Math.Max(1, sub.Height / 10)), 0, 0, InterpolationFlags.Area);
                Cv2.Resize(tmp, sub, new Size(sub.Width, sub.Height), 0, 0, InterpolationFlags.Nearest);
            }
        }
    }
}
