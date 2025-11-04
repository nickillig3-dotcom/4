using System;
using System.IO;
using OpenCvSharp;

namespace AutoShortsPro.App.Services
{
    public static class VideoProcessor
    {
        public static void ProcessVideo(
            string inputPath,
            string outputPath,
            int blurKernel = 35,
            bool pixelate = false,
            bool trialWatermark = false,
            bool detectFaces = true,
            bool detectPlates = true)
        {
            using var cap = new VideoCapture(inputPath);
            if (!cap.IsOpened()) throw new Exception("Video konnte nicht geöffnet werden: " + inputPath);

            int w = (int)cap.FrameWidth;
            int h = (int)cap.FrameHeight;
            double fps = cap.Fps > 0 ? cap.Fps : 25;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            string actualOut = outputPath;
            int fourcc = PickFourCC(Path.GetExtension(outputPath));
            VideoWriter? writer = null;

            try
            {
                writer = new VideoWriter(actualOut, fourcc, fps, new Size(w, h));
            }
            catch { }

            if (writer == null || !writer.IsOpened())
            {
                actualOut = Path.Combine(
                    Path.GetDirectoryName(outputPath)!,
                    Path.GetFileNameWithoutExtension(outputPath) + "_reencoded.avi");

                writer?.Dispose();
                writer = new VideoWriter(actualOut, FourCC.XVID, fps, new Size(w, h));
                if (!writer.IsOpened())
                    throw new Exception("VideoWriter konnte nicht geöffnet werden (auch AVI/XVID fehlgeschlagen).");
            }

            using var frame = new Mat();
            while (cap.Read(frame))
            {
                var rects = BlurEngine.DetectRegions(frame, detectFaces, detectPlates);
                foreach (var r in rects)
                {
                    int k = blurKernel % 2 == 0 ? blurKernel + 1 : blurKernel;
                    var safe = new Rect(
                        Math.Max(0, r.X), Math.Max(0, r.Y),
                        Math.Min(r.Width, frame.Width - r.X),
                        Math.Min(r.Height, frame.Height - r.Y));
                    using var sub = new Mat(frame, safe);
                    if (!pixelate) Cv2.GaussianBlur(sub, sub, new Size(k, k), 0);
                    else
                    {
                        using var tmp = new Mat();
                        Cv2.Resize(sub, tmp, new Size(Math.Max(1, sub.Width / 10), Math.Max(1, sub.Height / 10)), 0, 0, InterpolationFlags.Area);
                        Cv2.Resize(tmp, sub, new Size(sub.Width, sub.Height), 0, 0, InterpolationFlags.Nearest);
                    }
                }

                if (trialWatermark) TrialWatermark.Apply(frame);

                writer.Write(frame);
            }

            writer.Release();
            writer.Dispose();
        }

        private static int PickFourCC(string ext)
        {
            ext = (ext ?? "").ToLowerInvariant();
            if (ext == ".mp4" || ext == ".mov") return FourCC.MP4V;
            if (ext == ".avi") return FourCC.XVID;
            return FourCC.MJPG;
        }
    }
}
