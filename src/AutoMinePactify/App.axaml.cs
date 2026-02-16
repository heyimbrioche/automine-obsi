using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoMinePactify.ViewModels;
using AutoMinePactify.Views;

namespace AutoMinePactify;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();

            // Lancer l'animation puis ouvrir la fenetre principale
            Task.Run(async () =>
            {
                await splash.RunLoadingAnimation();
                var updateResult = splash.UpdateResult;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var vm = new MainWindowViewModel();

                    // Notifier si une mise a jour est disponible
                    if (updateResult?.UpdateAvailable == true && updateResult.Info != null)
                    {
                        vm.ShowUpdateNotification(updateResult.Info);
                    }

                    var mainWindow = new MainWindow
                    {
                        DataContext = vm
                    };

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    splash.Close();
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
