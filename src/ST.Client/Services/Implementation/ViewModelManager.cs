using ReactiveUI;
using System.Application.Mvvm;
using System.Application.UI.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace System.Application.Services.Implementation
{
    internal sealed class ViewModelManager : ReactiveObject, IViewModelManager
    {
        MainWindowViewModel? mainWindow;
        AchievementWindowViewModel? achievementWindow;
        TaskBarWindowViewModel? taskbarWindow;
        readonly CompositeDisposable compositeDisposable = new();

        WindowViewModel? mMainWindow;

        public WindowViewModel MainWindow
        {
            get => mMainWindow ?? throw new NullReferenceException("MainWindowViewModel is null.");
        }

        public TaskBarWindowViewModel? TaskBarWindow => taskbarWindow;

        public void InitViewModels()
        {
            try
            {
                if (appidUnlockAchievementHasValue)
                {
                    achievementWindow = new AchievementWindowViewModel(appidUnlockAchievement);
                    mMainWindow = achievementWindow;
                }
                else
                {
                    mainWindow = new MainWindowViewModel();
                    mMainWindow = mainWindow;
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(ViewModelManager), ex, "Init WindowViewModel");
                throw;
            }
            finally
            {
                try
                {
                    if (OperatingSystem2.IsWindows && StartupOptions.Value.HasNotifyIcon)
                    {
                        taskbarWindow = new TaskBarWindowViewModel(mainWindow);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(nameof(ViewModelManager), ex, "Init TaskBarWindowViewModel");
                    throw;
                }
            }
        }

        int appidUnlockAchievement;
        bool appidUnlockAchievementHasValue;

        public void InitUnlockAchievement(int appid)
        {
            appidUnlockAchievement = appid;
            appidUnlockAchievementHasValue = true;
        }

        //public Window GetMainWindow()
        //{
        //    if (this.MainWindow == this.mainWindow)
        //    {
        //        this.MainWindow
        //                   .Subscribe(nameof(MainWindowViewModel.SelectedItem),
        //                   () => this.MainWindow.StatusBar = (this.MainWindow as MainWindowViewModel).SelectedItem)
        //                   .AddTo(this);
        //        return new MainWindow { DataContext = this.MainWindow, };
        //    }
        //    if (this.MainWindow == this.achievementWindow)
        //    {
        //        return new AchievementWindow { DataContext = this.MainWindow, };
        //    }
        //    throw new InvalidOperationException();
        //}

        public void ShowTaskBarWindow(int x = 0, int y = 0)
        {
            try
            {
                //if (!taskbarWindow.IsVisible)
                //{
                taskbarWindow?.Show(x, y);
                //}
                //else
                //{
                //    taskbarWindow.SetPosition(x, y);
                //}
            }
            catch (Exception ex)
            {
                // https://appcenter.ms/orgs/BeyondDimension/apps/Steam/crashes/errors/1377813613u/overview
                Log.Error("WindowService", ex,
                    "ShowTaskBarWindow, taskbarWindow: {0}", taskbarWindow?.ToString() ?? "null");
                throw;
            }
        }

        #region disposable members

        ICollection<IDisposable> IDisposableHolder.CompositeDisposable => compositeDisposable;

        bool disposedValue;

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                    compositeDisposable.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
