using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Application.UI.ViewModels;

namespace System.Application.UI.Views.Windows
{
    public class IdleAppWindow : FluentWindow<IdleAppWindowViewModel>
    {
        public IdleAppWindow() : base()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools2();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
