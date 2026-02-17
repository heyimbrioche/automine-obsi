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

            // Lancer l'animation puis verifier les mises a jour
            Task.Run(async () =>
            {
                await splash.RunLoadingAnimation();
                var updateResult = splash.UpdateResult;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Si une mise a jour est dispo, bloquer le programme
                    if (updateResult?.UpdateAvailable == true && updateResult.Info != null)
                    {
                        var updateWindow = new UpdateRequiredWindow(updateResult.Info);
                        desktop.MainWindow = updateWindow;
                        updateWindow.Show();
                        splash.Close();
                        return;
                    }

                    // Sinon, ouvrir normalement
                    var vm = new MainWindowViewModel();
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
