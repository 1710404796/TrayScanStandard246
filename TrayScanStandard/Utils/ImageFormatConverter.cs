using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace TrayScanStandard.Utils
{
    internal static class ImageFormatConverter
    {
        private const long DefaultJpegQuality = 85L;

        /// <summary>
        /// 将 BMP 格式的字节数据转换为 JPEG 格式并保存到指定路径
        /// </summary>
        public static void SaveBmpAsJpeg(byte[] bmpData, string path, long quality = DefaultJpegQuality)
        {
            using var stream = new MemoryStream(bmpData);
            using var bitmap = new Bitmap(stream);

            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            bitmap.Save(path, jpegEncoder, parameters);
        }

        /// <summary>
        /// 将 BMP 格式的字节数据转换为 PNG 格式
        /// </summary>
        public static byte[] ConvertBmpToPng(byte[] bmpData)
        {
            using var stream = new MemoryStream(bmpData);
            using var bitmap = new Bitmap(stream);
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream, ImageFormat.Png);
            return pngStream.ToArray();
        }

        /// <summary>
        /// 检查字节数据是否为BMP格式（前2字节为"BM"）
        /// </summary>
        public static bool IsBmp(byte[] data) =>
            data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D;
    }
}
