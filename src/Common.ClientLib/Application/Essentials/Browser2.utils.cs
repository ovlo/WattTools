using System.ComponentModel;
using System.Threading.Tasks;
using Xamarin.Essentials;

// ReSharper disable once CheckNamespace
namespace System.Application
{
    partial class Browser2
    {
        enum OpenResultCode : sbyte
        {
            /// <summary>
            /// 值为 Http URL
            /// </summary>
            HttpUrl = 0,

            /// <summary>
            /// 已由 Process2 启动
            /// </summary>
            StartedByProcess2 = 1,

            /// <summary>
            /// 出现异常，已由 <see cref="OnError"/> 或 <see cref="Toast"/> 处理
            /// </summary>
            Exception = -1,

            /// <summary>
            /// 值格式不正确或未知
            /// </summary>
            Unknown = -2,
        }

        const string TAG = nameof(Browser2);

        static void HandlerException(Exception e)
        {
            if (OnError == null)
            {
                try
                {
                    e.LogAndShowT(TAG);
                }
                catch
                {
                }
            }
            else
            {
                OnError(e);
            }
        }

        static OpenResultCode OpenAnalysis(string? url)
        {
            if (IsStoreUrl(url) || IsEmailUrl(url))
            {
                return OpenCoreByProcess(url);
            }
            else if (IsHttpUrl(url, HttpsOnly))
            {
                if (DeviceInfo.Platform == DevicePlatform.Unknown)
                {
                    return OpenCoreByProcess(url);
                }
                else
                {
                    return OpenResultCode.HttpUrl;
                }
            }
            return OpenResultCode.Unknown;

            static OpenResultCode OpenCoreByProcess(string url)
            {
                var r = Process2.OpenCoreByProcess(url, s => Toast.Show(s));
                return r ? OpenResultCode.StartedByProcess2 : OpenResultCode.Exception;
            }
        }

        static async Task<bool> OpenCoreAsync(Uri uri, BrowserLaunchMode launchMode)
        {
            try
            {
                await Browser.OpenAsync(uri, launchMode);
                return true;
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static async Task<bool> OpenCoreAsync(string? uri, BrowserLaunchMode launchMode)
        {
            try
            {
                await Browser.OpenAsync(uri, launchMode);
                return true;
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static async Task<bool> OpenCoreAsync(Uri uri, BrowserLaunchOptions options)
        {
            try
            {
                return await Browser.OpenAsync(uri, options);
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static async Task<bool> OpenCoreAsync(string? uri, BrowserLaunchOptions options)
        {
            try
            {
                await Browser.OpenAsync(uri, options);
                return true;
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static async Task<bool> OpenCoreAsync(Uri uri)
        {
            try
            {
                await Browser.OpenAsync(uri);
                return true;
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static async Task<bool> OpenCoreAsync(string? uri)
        {
            try
            {
                await Browser.OpenAsync(uri);
                return true;
            }
            catch (Exception e)
            {
                HandlerException(e);
                return false;
            }
        }

        static bool OpenCore(string? uri)
        {
            OpenCoreSync(uri);
            return true;

            static async void OpenCoreSync(string? uri) => await OpenCoreAsync(uri);
        }
    }
}
