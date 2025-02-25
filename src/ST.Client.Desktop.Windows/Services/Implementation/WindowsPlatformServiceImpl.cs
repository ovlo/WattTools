using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Application.Models;
using System.Application.UI;
using System.Application.UI.Resx;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Windows.ApplicationModel;
using System.Application.Properties;

namespace System.Application.Services.Implementation
{
    internal sealed partial class WindowsPlatformServiceImpl : IPlatformService
    {
        const string TAG = "WindowsPlatformS";
        const string SteamRegistryPath = @"SOFTWARE\Valve\Steam";

        public string? GetRegistryVdfPath() { return null; }

        public string GetCommandLineArgs(Process process)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                      "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
                using var objects = searcher.Get();
                var @object = objects.Cast<ManagementBaseObject>().SingleOrDefault();
                return @object?["CommandLine"]?.ToString() ?? "";
            }
            catch (Win32Exception ex) when ((uint)ex.ErrorCode == 0x80004005)
            {
                // 没有对该进程的安全访问权限。
                return string.Empty;
            }
        }

        static readonly Lazy<string> mHostsFilePath = new(() =>
        {
            return Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
        });

        public string HostsFilePath => mHostsFilePath.Value;

        public string DefaultHostsContent => SR.hosts;

        static string GetFolderPath(Environment.SpecialFolder folder)
        {
            switch (folder)
            {
                case Environment.SpecialFolder.ProgramFiles:
                    var trimEndMark = " (x86)";
                    var value = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (value.EndsWith(trimEndMark, StringComparison.OrdinalIgnoreCase))
                    {
                        var value2 = value.Substring(0, value.Length - trimEndMark.Length);
                        if (Directory.Exists(value2))
                        {
                            return value2;
                        }
                    }
                    return value;
                default:
                    return Environment.GetFolderPath(folder);
            }
        }

        const string explorer = "explorer.exe";

        static string Explorer => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), explorer);

        public void OpenFolderByDirectoryPath(DirectoryInfo info)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Explorer,
                Arguments = $"\"{info.FullName}\"",
                UseShellExecute = false,
            });
        }

        public void OpenFolderSelectFilePath(FileInfo info)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Explorer,
                Arguments = $"/select,\"{info.FullName}\"",
                UseShellExecute = false,
            });
        }

        public string? GetFileName(TextReaderProvider provider)
        {
            switch (provider)
            {
                case TextReaderProvider.VSCode:
                    var vsCodePaths = new[] {
                        GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    }.Distinct().Select(x => Path.Combine(x, "Microsoft VS Code", "Code.exe"));

                    foreach (var vsCodePath in vsCodePaths)
                    {
                        if (File.Exists(vsCodePath))
                        {
                            return vsCodePath;
                        }
                    }

                    return null;
                case TextReaderProvider.Notepad:
                    return "notepad";
                default:
                    return null;
            }
        }

        /// <summary>
        /// UWP 的开机自启 TaskId，在 Package.appxmanifest 中设定
        /// </summary>
        const string BootAutoStartTaskId = "BootAutoStartTask";

        public async void SetBootAutoStart(bool isAutoStart, string name)
        {
            if (DesktopBridge.IsRunningAsUwp)
            {
                // https://blogs.windows.com/windowsdeveloper/2017/08/01/configure-app-start-log/
                // https://blog.csdn.net/lh275985651/article/details/109360162
                // https://www.cnblogs.com/wpinfo/p/uwp_auto_startup.html
                // 还需要通过 global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs() 判断为自启时最小化，不能通过参数启动
                var startupTask = await StartupTask.GetAsync(BootAutoStartTaskId);
                if (isAutoStart)
                {
                    var startupTaskState = startupTask.State;
                    if (startupTask.State == StartupTaskState.Disabled)
                    {
                        startupTaskState = await startupTask.RequestEnableAsync();
                    }
                    if (startupTaskState != StartupTaskState.Enabled &&
                        startupTaskState != StartupTaskState.EnabledByPolicy)
                    {
                        Toast.Show(AppResources.SetBootAutoStartTrueFail_.Format(startupTaskState));
                    }
                }
                else
                {
                    startupTask.Disable();
                }
            }
            else
            {
                // 开机启动使用 taskschd.msc 实现
                try
                {
                    var identity = WindowsIdentity.GetCurrent();
                    var hasSid = identity.User?.IsAccountSid() ?? false;
                    var userId = hasSid ? identity.User!.ToString() : identity.Name;
                    var tdName = hasSid ? userId : userId.Replace(Path.DirectorySeparatorChar, '_');
                    tdName = $"{name}_{{{tdName}}}";
                    if (isAutoStart)
                    {
                        using var td = TaskService.Instance.NewTask();
                        td.RegistrationInfo.Description = name + " System Boot Run";
                        td.Settings.Priority = ProcessPriorityClass.Normal;
                        td.Settings.ExecutionTimeLimit = new TimeSpan(0);
                        td.Settings.AllowHardTerminate = false;
                        td.Settings.StopIfGoingOnBatteries = false;
                        td.Settings.DisallowStartIfOnBatteries = false;
                        td.Triggers.Add(new LogonTrigger { UserId = userId });
                        td.Actions.Add(new ExecAction(IApplication.ProgramName,
                            IPlatformService.SystemBootRunArguments,
                            IOPath.BaseDirectory));
                        if (((IPlatformService)this).IsAdministrator)
                            td.Principal.RunLevel = TaskRunLevel.Highest;
                        TaskService.Instance.RootFolder.RegisterTaskDefinition(tdName, td);
                    }
                    else
                    {
                        TaskService.Instance.RootFolder.DeleteTask(name, exceptionOnNotExists: false);
                        TaskService.Instance.RootFolder.DeleteTask(tdName, exceptionOnNotExists: false);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(TAG, e,
                        "SetBootAutoStart Fail, isAutoStart: {0}, name: {1}.", isAutoStart, name);
                }
            }
        }

        public void SetSystemSessionEnding(Action action)
        {
            SystemEvents.SessionEnding += (sender, e) =>
            {
                //IDesktopAppService.Instance.CompositeDisposable.Dispose();
                action.Invoke();
            };
        }

        static string? GetFullPath(string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                return Path.GetFullPath(s);
            }
            return null;
        }

        public string? GetSteamDirPath()
            => GetFullPath(Registry.CurrentUser.Read(SteamRegistryPath, "SteamPath"));

        public string? GetSteamProgramPath()
            => GetFullPath(Registry.CurrentUser.Read(SteamRegistryPath, "SteamExe"));

        public string GetLastSteamLoginUserName()
            => Registry.CurrentUser.Read(SteamRegistryPath, "AutoLoginUser");

        public void SetCurrentUser(string userName)
        {
            if (DesktopBridge.IsRunningAsUwp)
            {
                var reg = $"Windows Registry Editor Version 5.00{Environment.NewLine}[HKEY_CURRENT_USER\\Software\\Valve\\Steam]{Environment.NewLine}\"AutoLoginUser\"=\"{userName}\"";
                var path = Path.Combine(IOPath.CacheDirectory, "switchuser.reg");
                File.WriteAllText(path, reg, Text.Encoding.UTF8);
                var p = Process2.Start("regedit", $"/s \"{path}\"", true);
                p?.WaitForExit();
            }
            else
            {
                Registry.CurrentUser.AddOrUpdate(SteamRegistryPath, "AutoLoginUser", userName, RegistryValueKind.String);
            }
        }

        static string GetMachineSecretKey()
            => Registry.LocalMachine.Read(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid");

        static readonly Lazy<(byte[] key, byte[] iv)> mMachineSecretKey = IPlatformService.GetMachineSecretKey(GetMachineSecretKey);

        public (byte[] key, byte[] iv) MachineSecretKey => mMachineSecretKey.Value;

        //const string runas_exe = "runas.exe";
        //static Process? StartAsInvokerByRunas(string fileName, string? arguments = null)
        //{
        //    // https://blog.lindexi.com/post/%E5%A6%82%E4%BD%95%E5%9C%A8-RunAs-%E5%90%AF%E5%8A%A8%E7%9A%84%E8%BD%AF%E4%BB%B6%E4%BC%A0%E5%85%A5%E5%B8%A6%E7%A9%BA%E6%A0%BC%E7%9A%84%E8%B7%AF%E5%BE%84%E5%B8%A6%E7%A9%BA%E6%A0%BC%E5%8F%82%E6%95%B0.html
        //    string arguments_;
        //    if (string.IsNullOrEmpty(arguments))
        //        arguments_ = $"/trustlevel:0x20000 \"{fileName}\"";
        //    else arguments_ = $"/trustlevel:0x20000 \"{fileName} \"{arguments}\"\"";
        //    return Process.Start(runas_exe, arguments_);
        //}

        const string explorer_exe = "explorer.exe";

        static Process? StartAsInvokerByExplorer(string fileName, string? arguments = null)
        {
            if (string.IsNullOrEmpty(arguments))
                return Process.Start(explorer_exe, $"\"{fileName}\"");
            var cacheCmdFile = Path.Combine(IOPath.CacheDirectory, "StartAsInvokerByExplorer" + FileEx.CMD);
            if (File.Exists(cacheCmdFile)) File.Delete(cacheCmdFile);
            File.WriteAllText(cacheCmdFile, $"@echo {Constants.HARDCODED_APP_NAME} StartAsInvokerByExplorer{Environment.NewLine}start \"\" \"{fileName}\" {arguments}{Environment.NewLine}exit 0");
            return Process.Start(explorer_exe, $"\"{cacheCmdFile}\"");
        }

        public Process? StartAsInvoker(string fileName, string? arguments = null)
        {
            if (((IPlatformService)this).IsAdministrator)
            {
                // runas /trustlevel:0x20000 没有真正的降权，只是作为具有限制特权，使用 explorer 最好，但不接受参数，可以创建一个临时cmd脚本启动
                //return StartAsInvokerByRunas(fileName, arguments);
                return StartAsInvokerByExplorer(fileName, arguments);

            }
            else
            {
                return Process2.Start(fileName, arguments);
            }
        }

        //public Process? StartAsInvoker(ProcessStartInfo startInfo)
        //{
        //    startInfo.FileName = runas_exe;
        //    startInfo.Arguments = $"/trustlevel:0x20000 \"{startInfo.FileName}\"";
        //    return Process.Start(startInfo);
        //}

        //public Process? GetProcessByPortOccupy(ushort port, bool isTCPorUDP = true)
        //{
        //    if (isTCPorUDP)
        //    {
        //        return SocketHelper.GetProcessByTcpPort(port);
        //    }
        //    else
        //    {
        //        throw new NotSupportedException();
        //    }
        //    //try
        //    //{
        //    //    using var p = new Process
        //    //    {
        //    //        StartInfo = new ProcessStartInfo
        //    //        {
        //    //            FileName = "cmd",
        //    //            UseShellExecute = false,
        //    //            RedirectStandardInput = true,
        //    //            RedirectStandardOutput = true,
        //    //            CreateNoWindow = true,
        //    //        },
        //    //    };
        //    //    p.Start();
        //    //    p.StandardInput.WriteLine($"netstat -ano|findstr \"{port}\"&exit");
        //    //    p.StandardInput.AutoFlush = true;
        //    //    var reader = p.StandardOutput;
        //    //    while (!reader.EndOfStream)
        //    //    {
        //    //        var line = reader.ReadLine();
        //    //        if (line == null) break;
        //    //        var lineArray = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        //    //        if (lineArray.Length != 5) continue;
        //    //        if (!lineArray[0].Equals(isTCPorUDP ? "TCP" : "UDP", StringComparison.OrdinalIgnoreCase)) continue;
        //    //        if (!lineArray[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase)) continue;
        //    //        if (!lineArray[1].EndsWith($":{port}")) continue;
        //    //        if (!ushort.TryParse(lineArray[4], out var pid)) continue;
        //    //        p.Close();
        //    //        return Process.GetProcessById(pid);
        //    //    }
        //    //    _ = p.WaitForExit(550);
        //    //}
        //    //catch
        //    //{
        //    //}
        //    //return default;
        //}

        /// <summary>
        /// 设置启用或关闭系统代理
        /// </summary>
        public bool SetAsSystemProxy()
        {
            return true;
        }

        const string ProxyOverride = "\"ProxyOverride\"=\"localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*\"";

        public bool SetAsSystemProxy(bool state, IPAddress? ip, int port)
        {
            if (DesktopBridge.IsRunningAsUwp)
            {
                var proxyEnable = $"\"ProxyEnable\"=dword:{(state ? "00000001" : "00000000")}";
                var hasIpAndProt = ip != null && port >= 0;
                var proxyServer = hasIpAndProt ? $"\"proxyServer\"=\"{ip}:{port}\"" : "";
                var reg = $"Windows Registry Editor Version 5.00{Environment.NewLine}[HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Internet Settings]{Environment.NewLine}{proxyEnable}{Environment.NewLine}{(state ? $"{ProxyOverride}{Environment.NewLine}" : "")}{proxyServer}";
                var path = Path.Combine(IOPath.CacheDirectory, "switchproxy.reg");
                File.WriteAllText(path, reg, Text.Encoding.UTF8);
                var p = Process2.Start("regedit", $"/s \"{path}\"", true);
                p?.WaitForExit();
            }
            return true;
        }

        public IReadOnlyCollection<KeyValuePair<string, string>> GetFontsByGdiPlus()
        {
            // https://docs.microsoft.com/zh-cn/typography/font-list
            var culture = R.Culture;
            InstalledFontCollection ifc = new();
            var list = ifc.Families.Where(x => x.IsStyleAvailable(FontStyle.Regular)).Select(x => KeyValuePair.Create(x.GetName(culture.LCID), x.GetName(1033))).ToList();
            list.Insert(0, IFontManager.Default);
            return list;
        }

        #region IsInstall

        static readonly Lazy<bool> _IsInstall = new(() =>
        {
            using var reg32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            var value = reg32.Read(@"SOFTWARE\Steam++", "InstPath").TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(IOPath.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), value, StringComparison.OrdinalIgnoreCase);
        });

        /// <inheritdoc cref="IPlatformService.IsInstall"/>
        public static bool IsInstall => _IsInstall.Value;

        bool IPlatformService.IsInstall => IsInstall;

        #endregion
    }
}