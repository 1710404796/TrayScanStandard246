using LinxUniverse.Algo.Common;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace TrayScanStandard.Utils
{
    internal static class RoiOverlayImageWriter
    {
        public static void WriteFailedRois(
            string imagePath,
            byte[] imageBytes,
            IEnumerable<ROI> rois,
            IEnumerable<CodeInfo> successfulCodes)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || imageBytes == null || imageBytes.Length == 0)
                return;

            var successfulIndexes = successfulCodes
                .Where(c => !string.IsNullOrEmpty(c.Code))
                .Select(c => c.Index)
                .ToHashSet();

            var failedRois = rois
                .Where(roi => !successfulIndexes.Contains(roi.Index))
                .ToArray();

            if (failedRois.Length == 0)
                return;

            // 保存到 Data2D/Failed/ 子目录，不覆盖原图
            var dir = Path.GetDirectoryName(imagePath);
            var failedDir = Path.Combine(dir ?? "", "Failed");
            Directory.CreateDirectory(failedDir);

            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var ext = Path.GetExtension(imagePath);
            var failedPath = Path.Combine(failedDir, $"{fileName}-Failed{ext}");

            try
            {
                using var stream = new MemoryStream(imageBytes);
                using var sourceBitmap = new Bitmap(stream);
                using var bitmap = new Bitmap(sourceBitmap);
                using var graphics = Graphics.FromImage(bitmap);

                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 绘制所有 ROI：成功 = 绿色，失败 = 红色
                foreach (var roi in rois)
                {
                    bool isSuccess = successfulIndexes.Contains(roi.Index);
                    var color = isSuccess ? Color.Green : Color.Red;

                    var rect = new Rectangle(
                        (int)roi.Rect.X,
                        (int)roi.Rect.Y,
                        (int)roi.Rect.Width,
                        (int)roi.Rect.Height);

                    var penWidth = Math.Max(4, Math.Min(rect.Width, rect.Height) / 25f);
                    using var pen = new Pen(color, penWidth);
                    using var font = new Font("Arial", Math.Max(16, penWidth * 4), FontStyle.Bold);

                    graphics.DrawRectangle(pen, rect);
                    graphics.DrawString(
                        roi.Index.ToString(),
                        font,
                        isSuccess ? Brushes.Green : Brushes.Red,
                        new PointF(rect.X, Math.Max(0, rect.Y - font.Height)));
                }

                // 保存到失败目录，不覆盖原始文件
                bitmap.Save(failedPath, ImageFormat.Png);
            }
            catch
            {
                // 静默处理，不中断主流程
            }
        }
    }
}
