using Avalonia.Data;
using System.Application.Models;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace System.Application.Converters
{
    public class BitmapAssetValueConverter : ImageValueConverter
    {
        public override object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
            int width = 0;
            if (parameter is int w)
            {
                width = w;
            }

            if (value is string rawUri)
            {
                if (rawUri == string.Empty) return null;

                Uri uri;
                // Allow for assembly overrides
                if (File.Exists(rawUri))
                {
                    return GetDecodeBitmap(rawUri, width);
                }
                // 在列表中使用此方法性能极差
                else if (Browser2.IsHttpUrl(rawUri))
                {
                    return DownloadImage(rawUri, width);
                }
                else if (rawUri.StartsWith("avares://"))
                {
                    uri = new Uri(rawUri);
                }
                else
                {
                    uri = GetResUri(rawUri);
                }
                var asset = OpenAssets(uri);
                return GetDecodeBitmap(asset, width);
            }
            else if (value is Stream s)
            {
                TryReset(s);
                return GetDecodeBitmap(s, width);
            }
            else if (value is ImageSouce.ClipStream clipStream)
            {
                return GetBitmap(clipStream);
            }
            else if (value is Guid imageid)
            {
                if (imageid == default)
                {
                    return null;
                }
                return DownloadImage(ImageUrlHelper.GetImageApiUrlById(imageid), width);
            }
            return BindingOperations.DoNothing;
        }
    }
}