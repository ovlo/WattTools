using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using SkiaSharp;
using System.Application.Models;
using System.Application.Services;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using BitmapInterpolationMode = Avalonia.Visuals.Media.Imaging.BitmapInterpolationMode;

namespace System.Application.Converters
{
    public abstract class ImageValueConverter : IValueConverter
    {
        public abstract object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);

        public virtual object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }

        protected static Bitmap? DownloadImage(string? url, int width = 0)
        {
            if (url == null) return null;
            var task = IHttpService.Instance.GetAsync<Stream>(url, MediaTypeNames.All);
            var stream = task.GetAwaiter().GetResult();
            if (stream == null) return null;
            var s = new MemoryStream();
            stream.CopyTo(s);
            stream.Dispose();
            return GetDecodeBitmap(s, width);
        }

        protected static void TryReset(Stream s)
        {
            if (s.CanSeek)
            {
                if (s.Position > 0)
                {
                    s.Position = 0;
                }
            }
        }

        /// <summary>
        /// 将 图片源(流) 转换为 <see cref="SKBitmap"/>(Skia)
        /// </summary>
        /// <param name="clipStream"></param>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        static bool TryGetBitmap([NotNullWhen(true)] ImageSouce.ClipStream? clipStream, [NotNullWhen(true)] out SKBitmap? bitmap)
        {
            if (clipStream?.Stream == null)
            {
                bitmap = null;
                return false;
            }

            var filename = clipStream.Name;
            if (filename != null)
            {
                bitmap = SKBitmap.Decode(filename);
            }
            else
            {
                TryReset(clipStream.Stream);
                using var ms = new MemoryStream();
                clipStream.Stream.CopyTo(ms);
                TryReset(ms);
                bitmap = SKBitmap.Decode(ms);
            }
            return true;
        }

        /// <summary>
        /// 将 图片源(流) 根据预设参数处理并转换为 <see cref="Bitmap"/>(Avalonia)
        /// </summary>
        /// <param name="clipStream"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        protected static Bitmap? GetBitmap(ImageSouce.ClipStream? clipStream, int width = 0)
        {
            if (!TryGetBitmap(clipStream, out var bitmap)) return null;

            using var bitmapSource = bitmap;
            using var bitmapDest = new SKBitmap(bitmapSource.Width, bitmapSource.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

            using var canvas = new SKCanvas(bitmapDest);

            var rect = clipStream.Circle ?
                new SKRect(0, 0, bitmapSource.Width, bitmapSource.Height) :
                new SKRect(clipStream.Left, clipStream.Top, clipStream.Right, clipStream.Bottom);
            var roundRect = clipStream.Circle ?
                new SKRoundRect(rect, bitmapSource.Width / 2f, bitmapSource.Height / 2f) :
                new SKRoundRect(rect, clipStream.Radius_X, clipStream.Radius_Y);
            canvas.ClipRoundRect(roundRect, antialias: true);

            canvas.DrawBitmap(bitmapSource, 0, 0);

            #region Obsolete

            //var stream = bitmapDest.Encode(SKEncodedImageFormat.Png, 100).AsStream();
            //TryReset(stream);

            ////var tempFilePath = Path.Combine(IOPath.CacheDirectory, Path.GetFileName(Path.GetTempFileName() + ".png"));
            ////using (var fs = File.Create(tempFilePath))
            ////{
            ////    stream.CopyTo(fs);
            ////    TryReset(stream);
            ////}

            //return GetDecodeBitmap(stream, width);

            #endregion

            #region New Code

            return GetDecodeBitmap(bitmapDest, width);

            #endregion
        }

        /// <summary>
        /// 将 <see cref="SKBitmap"/>(Skia) 转换为 <see cref="Bitmap"/>(Avalonia)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("s")]
        protected static Bitmap? GetDecodeBitmap(SKBitmap? s, int width)
        {
            if (s == null)
            {
                return null;
            }
            if (width < 1)
            {
                return SkiaSharpHelpers.GetBitmap(s);
            }
            return SkiaSharpHelpers.DecodeToWidth(s, width, BitmapInterpolationMode.MediumQuality);
        }

        /// <summary>
        /// 将 <see cref="Stream"/> 转换为 <see cref="Bitmap"/>(Avalonia)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("s")]
        protected static Bitmap? GetDecodeBitmap(Stream? s, int width)
        {
            if (s == null)
            {
                return null;
            }
            if (width < 1)
            {
                return new Bitmap(s);
            }
            return Bitmap.DecodeToWidth(s, width, BitmapInterpolationMode.MediumQuality);
        }

        /// <summary>
        /// 将 <see cref="string"/>(filePath) 转换为 <see cref="Bitmap"/>(Avalonia)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        [return: NotNullIfNotNull("s")]
        protected static Bitmap? GetDecodeBitmap(string? s, int width)
        {
            #region Obsolete

            //if (IOPath.TryOpenRead(s, out var stream, out var _))
            //    return GetDecodeBitmap(stream, width);
            //else
            //    return null;

            #endregion

            #region New Code

            if (s == null)
            {
                return null;
            }
            if (width < 1)
            {
                var image = SKImage.FromEncodedData(s);
                return SkiaSharpHelpers.GetBitmap(image);
            }
            var bitmap = SKBitmap.Decode(s);
            return SkiaSharpHelpers.DecodeToWidth(bitmap, width, BitmapInterpolationMode.MediumQuality);

            #endregion
        }

        protected static Stream? OpenAssets(Uri uri)
        {
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            if (assets == null) return null;
            if (assets.Exists(uri)) return assets.Open(uri);
            return null;
        }

        protected static Uri GetResUri(string relativeUri, string? assemblyName = null)
        {
            assemblyName ??= (Assembly.GetEntryAssembly() ?? typeof(ImageValueConverter).Assembly).GetName().Name;
            if (assemblyName == null) throw new ArgumentNullException(assemblyName);
            var uri = new Uri($"avares://{assemblyName}{relativeUri}");
            return uri;
        }
    }
}