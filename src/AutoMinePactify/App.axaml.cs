using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutoMinePactify.Services;
using AutoMinePactify.ViewModels;
using AutoMinePactify.Views;

namespace AutoMinePactify;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
    private LicenseService? _licenseService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _licenseService = new LicenseService();
            var splash = new SplashWindow(_licenseService);
            desktop.MainWindow = splash;
            splash.Show();

            Task.Run(async () =>
            {
                await splash.RunLoadingAnimation();
                var licenseResult = splash.LicenseCheckResult;
                var updateResult = splash.UpdateResult;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    bool licensed = licenseResult?.Status == LicenseStatus.Valid;

                    if (!licensed)
                    {
                        var loginWindow = new LoginWindow(_licenseService);
                        desktop.MainWindow = loginWindow;

                        loginWindow.Closed += (_, _) =>
                        {
                            if (!loginWindow.IsLicenseValid)
                            {
                                desktop.Shutdown();
                                return;
                            }
                            OpenMainOrUpdate(desktop, updateResult);
                        };

                        loginWindow.Show();
                        splash.Close();
                        return;
                    }

                    splash.Close();
                    OpenMainOrUpdate(desktop, updateResult);
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OpenMainOrUpdate(
        IClassicDesktopStyleApplicationLifetime desktop,
        UpdateChecker.UpdateResult? updateResult)
    {
        if (updateResult?.UpdateAvailable == true && updateResult.Info != null)
        {
            var updateWindow = new UpdateRequiredWindow(updateResult.Info);
            desktop.MainWindow = updateWindow;
            updateWindow.Show();
            return;
        }

        var vm = new MainWindowViewModel();
        var mainWindow = new MainWindow { DataContext = vm };

        _licenseService?.StartPeriodicCheck();

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
    }
}
