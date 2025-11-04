using System;
using OpenCvSharp;

namespace AutoShortsPro.App.Services
{
    public static class TrialWatermark
    {
        public static void Apply(Mat img)
        {
            // halbtransparenter "TRIAL"-Text mittig
            using var overlay = img.Clone();
            string text = "TRIAL";
            double scale = Math.Max(img.Width, img.Height) / 500.0;
            int thickness = Math.Max(1, (int)Math.Round(scale * 2));

            var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, thickness, out _);
            var org = new Point((img.Width - size.Width) / 2, (img.Height + size.Height) / 2);

            Cv2.PutText(overlay, text, org, HersheyFonts.HersheySimplex, scale, Scalar.White, thickness, LineTypes.AntiAlias);
            Cv2.AddWeighted(overlay, 0.35, img, 0.65, 0, img);
        }
    }
}
