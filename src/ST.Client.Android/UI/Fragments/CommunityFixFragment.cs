using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.View.Menu;
using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Binding;
using ReactiveUI;
using System.Application.Services;
using System.Application.Services.Native;
using System.Application.Settings;
using System.Application.UI.Adapters;
using System.Application.UI.Adapters.Internals;
using System.Application.UI.Resx;
using System.Application.UI.ViewModels;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Application.Services.Implementation;
using Android.Net;
using Android.Content;
using System.Application.UI.Activities;
using Android.App;
using static AndroidX.Activity.Result.ActivityResultTask;
using static System.Application.UI.ViewModels.CommunityProxyPageViewModel;

namespace System.Application.UI.Fragments
{
    /// <summary>
    /// 网络加速 Fragment
    /// <para>已知问题：</para>
    /// <para>1. (中等概率)首次启动时列表 UI 不显示，触摸列表区域上下滑动即可正常显示，属于 UI Bug，网络请求数据正常</para>
    /// <para>2. (较小概率)首次启动时列表项间距不正确，与 VerticalItemDecoration2 实现相关</para>
    /// <para>解决方案：</para>
    /// <para>使用 MainActivity3 后在进行观察或使用 Xamarin.Forms/MAUI/Avalonia 重写</para>
    /// </summary>
    [Register(JavaPackageConstants.Fragments + nameof(CommunityFixFragment))]
    internal sealed class CommunityFixFragment : BaseFragment<fragment_community_fix, CommunityProxyPageViewModel>, SwipeRefreshLayout.IOnRefreshListener
    {
        protected override int? LayoutResource => Resource.Layout.fragment_community_fix;

        protected sealed override CommunityProxyPageViewModel? OnCreateViewModel()
        {
            return IViewModelManager.Instance.GetMainPageViewModel<CommunityProxyPageViewModel>();
        }

        public override void OnCreateView(View view)
        {
            base.OnCreateView(view);

            var proxyS = ProxyService.Current;
            void SetProxyModeText()
            {
                var proxyMode = ProxySettings.ProxyModeValue;
                binding!.tvProxyMode.Text = proxyMode switch
                {
                    ProxyMode.ProxyOnly => $"{AppResources.CommunityFix_ProxyModeTip}{ProxySettings.ToStringByProxyMode(proxyMode)}{Environment.NewLine}IPEndPoint: {proxyS.ProxyIp}:{proxyS.ProxyPort}",
                    _ => $"{AppResources.CommunityFix_ProxyModeTip}{ProxySettings.ToStringByProxyMode(proxyMode)}",
                };
            }

            R.Subscribe(() =>
            {
                if (binding == null) return;
                binding.btnStartProxyService.Text = AppResources.CommunityFix_StartProxy;
                binding.btnStopProxyService.Text = AppResources.CommunityFix_StopProxy;
                SetProxyModeText();
                binding.tvAccelerationsEnable.Text = AppResources.CommunityFix_AccelerationsEnable;
                binding.tvScriptsEnable.Text = AppResources.CommunityFix_ScriptsEnable;
                SetMenuTitle();
            }).AddTo(this);

            proxyS.WhenAnyValue(x => x.AccelerateTime).SubscribeInMainThread(value =>
            {
                if (binding == null) return;
                binding.tvAccelerateTime.Text = AppResources.CommunityFix_AlreadyProxy + value.ToString(@"hh\:mm\:ss");
            }).AddTo(this);
            proxyS.WhenAnyValue(x => x.ProxyStatus).SubscribeInMainThread(value =>
            {
                if (binding == null) return;
                binding.layoutRootCommunityFixContentReady.Visibility = !value ? ViewStates.Visible : ViewStates.Gone;
                binding.layoutRootCommunityFixContentStarting.Visibility = value ? ViewStates.Visible : ViewStates.Gone;
                if (value)
                {
                    SetProxyModeText();
                    StringBuilder s = new();
                    var enableProxyDomains = proxyS.GetEnableProxyDomains();
                    if (enableProxyDomains != null)
                    {
                        foreach (var item in enableProxyDomains)
                        {
                            s.AppendLine(item.Name);
                        }
                    }
                    binding.tvAccelerationsEnableContent.Text = s.ToString();
                    if (proxyS.IsEnableScript)
                    {
                        s.Clear();
                        SetScriptsEnableContentText(s);
                    }
                }
                if (menuBuilder != null)
                {
                    var menu_settings_proxy = menuBuilder.FindItem(Resource.Id.menu_settings_proxy);
                    if (menu_settings_proxy != null)
                    {
                        menu_settings_proxy.SetVisible(!value);
                    }
                }
            }).AddTo(this);
            proxyS.WhenAnyValue(x => x.IsEnableScript).SubscribeInMainThread(value =>
            {
                if (binding == null) return;
                if (value)
                {
                    SetScriptsEnableContentText();
                }
                binding.cardScriptsEnable.Visibility = value ? ViewStates.Visible : ViewStates.Gone;
            }).AddTo(this);

            void SetScriptsEnableContentText(StringBuilder? s = null)
            {
                s ??= new();
                var enableProxyScripts = proxyS.GetEnableProxyScripts();
                if (enableProxyScripts != null)
                {
                    foreach (var item in enableProxyScripts)
                    {
                        s.AppendLine(item.Name);
                    }
                }
                binding!.tvScriptsEnableContent.Text = s.ToString();
            }

            var adapter = new AccelerateProjectGroupAdapter();
            adapter.ItemClick += (_, e) =>
            {
                //var value = e.Current.ThreeStateEnable;
                //e.Current.ThreeStateEnable = value == null || !value.Value;

                if (e is IPlatformItemClickEventArgs e2 && e2.ViewHolder is AccelerateProjectGroupViewHolder vh)
                {
                    vh.IsOpen = !vh.IsOpen;
                }
            };
            binding!.rvAccelerateProjectGroup.SetLinearLayoutManager();
            binding.rvAccelerateProjectGroup.AddVerticalItemDecorationRes(Resource.Dimension.activity_vertical_margin, Resource.Dimension.fab_height_with_margin_top_bottom);
            binding.rvAccelerateProjectGroup.SetAdapter(adapter);

            binding.swipeRefreshLayout.InitDefaultStyles();
            binding.swipeRefreshLayout.SetOnRefreshListener(this);

            SetOnClickListener(binding.btnStartProxyService, binding.btnStopProxyService);
        }

        public override void OnStop()
        {
            base.OnStop();

            if (ProxyService.IsChangeSupportProxyServicesStatus)
            {
                ProxyService.IsChangeSupportProxyServicesStatus = false;
                SettingsHost.Save();
#if DEBUG
                Toast.Show("[DEBUG] 已保存勾选状态");
#endif
            }
        }

        async void StartProxyButton_Click(bool start/*, bool ignoreVPNCheck = false*/)
        {
            var a = RequireActivity();
            if (start)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    const string textCertificateTrustTip =
                        "因 Android 7(Nougat API 24) 之后的版本不在信任用户证书，所以此功能已放弃继续开发，" +
                        "如仍想使用需要自行导入证书到系统目录，使用 adb 工具或 Magisk 之类的软件操作，" +
                        "未来会使用不需要证书的加速功能替换此功能";
                    await MessageBox.ShowAsync(textCertificateTrustTip, "已知问题",
                        rememberChooseKey: MessageBox.DontPromptType.AndroidCertificateTrustTip);
                }

                Intent? intent = null;
                if (/*!ignoreVPNCheck &&*/ ProxySettings.ProxyModeValue == ProxyMode.VPN) // 当启用 VPN 模式时
                {
                    intent = VpnService.Prepare(a);
                    if (intent != null)
                    {
                        // 需要授予 VPN 权限
                        intent = new Intent(a, typeof(GuideVPNActivity));
                        try
                        {
                            intent = await IntermediateActivity.StartAsync(intent, NextRequestCode());
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }

                if (!IHttpProxyService.Instance.IsCurrentCertificateInstalled) // 检查 CA 证书是否已安装
                {
                    // 当未安装证书时
                    intent = new Intent(a, typeof(GuideCACertActivity));
                    try
                    {
                        intent = await IntermediateActivity.StartAsync(intent, NextRequestCode());
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                if (intent != null)
                {
                    var result = IntermediateActivity.GetResult(intent);
                    if (result != Result.Ok)
                    {
                        // 从引导页面返回结果不为 Ok 时则不启动加速
                        return;
                    }
                }
            }
            AndroidPlatformServiceImpl.StartOrStopForegroundService(a, nameof(ProxyService), start);
        }

        protected override bool OnClick(View view)
        {
#if DEBUG
            Toast.Show($"[DEBUG] Ready: {binding!.layoutRootCommunityFixContentReady.Visibility}, Starting: {binding!.layoutRootCommunityFixContentStarting.Visibility}, rv: {binding.rvAccelerateProjectGroup.Visibility}");
#endif
            if (view.Id == Resource.Id.btnStartProxyService)
            {
                StartProxyButton_Click(true);
                return true;
            }
            else if (view.Id == Resource.Id.btnStopProxyService)
            {
                StartProxyButton_Click(false);
                return true;
            }
            return base.OnClick(view);
        }

        void SwipeRefreshLayout.IOnRefreshListener.OnRefresh()
        {
            binding!.swipeRefreshLayout.Refreshing = false;
            ViewModel!.RefreshButton_Click();
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HasOptionsMenu = true;
        }

        MenuBuilder? menuBuilder;

        public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
        {
            inflater.Inflate(Resource.Menu.community_fix_toolbar_menu, menu);
            menuBuilder = menu.SetOptionalIconsVisible();
            if (menuBuilder != null)
            {
                SetMenuTitle();
            }
        }

        void SetMenuTitle() => menuBuilder.SetMenuTitle(ToString2, MenuIdResToEnum);

        /// <summary>
        /// 移除证书弹窗提示
        /// </summary>
        /// <param name="context"></param>
        internal static async void UninstallCertificateShowTips(Context context)
        {
            string title = AppResources.CommunityFix_DeleteCertificateTipTitle;
            string text = AppResources.CommunityFix_DeleteCertificateTipText_.Format(IHttpProxyService.RootCertificateName); ;
            var r = await MessageBox.ShowAsync(text, title, MessageBox.Button.OKCancel);
            if (r.IsOK())
            {
                GoToPlatformPages.SystemSettingsSecurity(context);
            }
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            var actionItem = MenuIdResToEnum(item.ItemId);
            if (actionItem.IsDefined())
            {
                switch (actionItem)
                {
                    case ActionItem.CertificateExport:
                        ViewModel!.ExportCertificateFile();
                        return true;
                    case ActionItem.GoToSystemSecuritySettings:
                        GoToPlatformPages.SystemSettingsSecurity(RequireContext());
                        return true;
                    case ActionItem.CertificateInstall:
                        InstallCertificate();
                        return true;
                    case ActionItem.CertificateUninstall:
                        UninstallCertificateShowTips(RequireContext());
                        return true;
                    case ActionItem.CertificateStatus:
                        GoToPlatformPages.StartActivity<CACertStatusActivity>(RequireActivity());
                        return true;
                }
                ViewModel!.MenuItemClick(actionItem);
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        static ActionItem MenuIdResToEnum(int resId)
        {
            if (resId == Resource.Id.menu_settings_proxy)
            {
                return ActionItem.ProxySettings;
            }
            else if (resId == Resource.Id.menu_export_certificate_file)
            {
                return ActionItem.CertificateExport;
            }
            else if (resId == Resource.Id.menu_settings_security)
            {
                return ActionItem.GoToSystemSecuritySettings;
            }
            else if (resId == Resource.Id.menu_install_certificate_file)
            {
                return ActionItem.CertificateInstall;
            }
            else if (resId == Resource.Id.menu_uninstall_certificate_file)
            {
                return ActionItem.CertificateUninstall;
            }
            else if (resId == Resource.Id.menu_certificate_status)
            {
                return ActionItem.CertificateStatus;
            }
            return default;
        }

        internal static void InstallCertificate(Context context, CommunityProxyPageViewModel vm) => vm.ExportCertificateFile(cefFilePath =>
        {
            GoToPlatformPages.InstallCertificate(context, cefFilePath, IHttpProxyService.RootCertificateName);
        });

        void InstallCertificate() => InstallCertificate(RequireContext(), ViewModel!);

#if DEBUG
        async void Test()
        {
            if (!ProxyService.Current.ProxyStatus) return;
            var s = IHttpProxyService.Instance;
            Xamarin.Android.Net.AndroidClientHandler handler = new();
            handler.Proxy = new WebProxy(s.ProxyIp.ToString(), s.ProxyPort);
            var certFilePath = s.GetCerFilePathGeneratedWhenNoFileExists();
            if (certFilePath != null)
                handler.AddTrustedCert(File.OpenRead(certFilePath));
            using var client = new HttpClient(handler);
            using var rsp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get,
                "https://steamcommunity.com"));
            var html = await rsp.Content.ReadAsStringAsync();
            Toast.Show($"StatusCode: {rsp.StatusCode}, Length: {rsp.Content.Headers.ContentLength}");
        }
#endif
    }
}