using System;
using System.Application;
using System.Application.Services;
using System.Application.Services.Implementation;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPlatformService(this IServiceCollection services, StartupOptions options)
        {
#pragma warning disable CA1416 // 验证平台兼容性
            if (OperatingSystem2.IsWindows)
            {
                services.AddSingleton<IHttpPlatformHelperService, WindowsClientHttpPlatformHelperServiceImpl>();
                services.AddSingleton<WindowsPlatformServiceImpl>();
                services.AddSingleton<IPlatformService>(s => s.GetRequiredService<WindowsPlatformServiceImpl>());
                if (options.HasSteam)
                {
                    services.AddSingleton<ISteamworksLocalApiService, SteamworksLocalApiServiceImpl>();
                }
                if (options.HasGUI)
                {
                    services.AddSingleton<IBiometricService, PlatformBiometricServiceImpl>();
                }
                services.AddSingleton<WindowsProtectedData>();
                services.AddSingleton<IProtectedData>(s => s.GetRequiredService<WindowsProtectedData>());
                services.AddSingleton<ILocalDataProtectionProvider.IProtectedData>(s => s.GetRequiredService<WindowsProtectedData>());
                if (OperatingSystem2.IsWindows10AtLeast)
                {
                    services.AddSingleton<IEmailPlatformService>(s => s.GetRequiredService<WindowsPlatformServiceImpl>());
                    services.AddSingleton<ILocalDataProtectionProvider.IDataProtectionProvider, Windows10DataProtectionProvider>();
                }
                services.AddSingleton<INativeWindowApiService, NativeWindowApiServiceImpl>();
                if (Windows10JumpListServiceImpl.IsSupported)
                {
                    services.AddSingleton<IJumpListService, Windows10JumpListServiceImpl>();
                }
                else
                {
                    services.AddSingleton<IJumpListService, JumpListServiceImpl>();
                }
                if (options.HasMainProcessRequired)
                {
                    services.AddSingleton(typeof(NotifyIcon), NotifyIcon.ImplType);
                }
                //services.AddSingleton<AvaloniaFontManagerImpl, WindowsAvaloniaFontManagerImpl>();
                services.AddSingleton<ISevenZipHelper, SevenZipHelper>();
                services.AddMSAppCenterApplicationSettings();
                services.AddPlatformNotificationService();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
            return services;
#pragma warning restore CA1416 // 验证平台兼容性
        }

        /// <summary>
        /// 添加适用于 Windows 的 <see cref="INotificationService"/>
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        static IServiceCollection AddPlatformNotificationService(this IServiceCollection services)
        {
            if (!DesktopBridge.IsRunningAsUwp && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                // 桌面 (MSIX) 中 无法处理通知点击事件
                services.AddSingleton<INotificationService, Windows10NotificationServiceImpl>();
            }
            else
            {
                services.AddSingleton<INotificationService, WindowsNotificationServiceImpl>();
            }
            return services;
        }
    }
}