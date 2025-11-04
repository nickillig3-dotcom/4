using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace AutoShortsPro.App.Services
{
    public static class BlurEngine
    {
        private static readonly string BaseDir     = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string CascadeDir  =
            Path.Combine(BaseDir, "Assets", "cascades");
        private static readonly string FaceProto   =
            Path.Combine(BaseDir, "Assets", "models", "face_ssd", "deploy.prototxt");
        private static readonly string FaceModel   =
            Path.Combine(BaseDir, "Assets", "models", "face_ssd", "res10_300x300_ssd_iter_140000.caffemodel");

        private static readonly Lazy<CascadeClassifier> FaceCascade = new(() =>
            new CascadeClassifier(Path.Combine(CascadeDir, "haarcascade_frontalface_default.xml")));

        private static readonly Lazy<CascadeClassifier> PlateCascade = new(() =>
            new CascadeClassifier(Path.Combine(CascadeDir, "haarcascade_russian_plate_number.xml")));

        private static Net? _faceDnn;

        public static void ProcessImage(
            string inputPath,
            string outputPath,
            int blurKernel = 35,
            bool pixelate = false,
            bool trialWatermark = false,
            bool preferDnnFaces = false)
        {
            using var img = Cv2.ImRead(inputPath);
            if (img.Empty()) throw new Exception("Bild konnte nicht geladen werden: " + inputPath);

            var rects = DetectRegions(img, preferDnnFaces);
            foreach (var r in rects)
                ApplyBlur(img, r, blurKernel, pixelate);

            if (trialWatermark) TrialWatermark.Apply(img);

            var outDir = Path.GetDirectoryName(outputPath) ?? Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(outDir);
            Cv2.ImWrite(outputPath, img);
        }

        // Verarbeitung mit vom UI vorgegebenen Boxen (für manuelle Bildprüfung)
        public static void ProcessImageWithRects(
            string inputPath,
            string outputPath,
            IEnumerable<Rect> rects,
            int blurKernel = 35,
            bool pixelate = false,
            bool trialWatermark = false)
        {
            using var img = Cv2.ImRead(inputPath);
            if (img.Empty()) throw new Exception("Bild konnte nicht geladen werden: " + inputPath);

            foreach (var r in rects)
                ApplyBlur(img, r, blurKernel, pixelate);

            if (trialWatermark) TrialWatermark.Apply(img);

            var outDir = Path.GetDirectoryName(outputPath) ?? Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
            Directory.CreateDirectory(outDir);
            Cv2.ImWrite(outputPath, img);
        }

        // Erkennung (DNN-Face optional, Kennzeichen via Haar)
        public static List<Rect> DetectRegions(Mat frame, bool preferDnnFaces = false)
        {
            Rect[] faces;
            if (preferDnnFaces && File.Exists(FaceProto) && File.Exists(FaceModel))
                faces = DetectFacesDnn(frame, 0.5f);
            else
                faces = FaceCascade.Value.DetectMultiScale(ToGray(frame), 1.1, 4, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(24, 24));

            var plates = PlateCascade.Value.DetectMultiScale(ToGray(frame), 1.1, 4, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(24, 24));

            var result = new List<Rect>(faces.Length + plates.Length);
            result.AddRange(faces);
            result.AddRange(plates);
            return result;
        }

        private static Mat ToGray(Mat bgr)
        {
            var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);
            return gray;
        }

        private static Rect[] DetectFacesDnn(Mat frame, float confThresh)
        {
            var net = _faceDnn ??= CvDnn.ReadNetFromCaffe(FaceProto, FaceModel);
            if (net is null) throw new Exception("Face-DNN konnte nicht geladen werden.");

            using var blob = CvDnn.BlobFromImage(frame, 1.0, new OpenCvSharp.Size(300, 300), new Scalar(104, 177, 123), false, false);
            net.SetInput(blob);
            using var prob = net.Forward(); // 1x1xNx7

            using var det = prob.Reshape(1, (int)prob.Total() / 7); // Nx7
            var faces = new List<Rect>(det.Rows);

            for (int i = 0; i < det.Rows; i++)
            {
                float confidence = det.At<float>(i, 2);
                if (confidence < confThresh) continue;

                int x1 = (int)(det.At<float>(i, 3) * frame.Cols);
                int y1 = (int)(det.At<float>(i, 4) * frame.Rows);
                int x2 = (int)(det.At<float>(i, 5) * frame.Cols);
                int y2 = (int)(det.At<float>(i, 6) * frame.Rows);

                x1 = Math.Max(0, Math.Min(x1, frame.Cols - 1));
                y1 = Math.Max(0, Math.Min(y1, frame.Rows - 1));
                x2 = Math.Max(0, Math.Min(x2, frame.Cols - 1));
                y2 = Math.Max(0, Math.Min(y2, frame.Rows - 1));

                int w = Math.Max(0, x2 - x1);
                int h = Math.Max(0, y2 - y1);
                if (w > 0 && h > 0) faces.Add(new Rect(x1, y1, w, h));
            }

            return faces.ToArray();
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
                Cv2.GaussianBlur(sub, sub, new OpenCvSharp.Size(k, k), 0);
            }
            else
            {
                using var tmp = new Mat();
                Cv2.Resize(sub, tmp, new OpenCvSharp.Size(Math.Max(1, sub.Width / 10), Math.Max(1, sub.Height / 10)), 0, 0, InterpolationFlags.Area);
                Cv2.Resize(tmp, sub, new OpenCvSharp.Size(sub.Width, sub.Height), 0, 0, InterpolationFlags.Nearest);
            }
        }
    }
}

