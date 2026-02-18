using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
            // Empecher Avalonia de fermer l'app quand une fenetre se ferme
            // (on gere manuellement les transitions Splash -> Login -> Main)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
                        // Licence pas en cache ou invalide : afficher la fenetre login
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

                    // Licence valide : continuer
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
        // Si une mise a jour est dispo, bloquer le programme
        if (updateResult?.UpdateAvailable == true && updateResult.Info != null)
        {
            var updateWindow = new UpdateRequiredWindow(updateResult.Info);
            desktop.MainWindow = updateWindow;
            // Fermer l'app quand la fenetre d'update se ferme
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            updateWindow.Show();
            return;
        }

        // Ouvrir l'app normalement
        var vm = new MainWindowViewModel();
        var mainWindow = new MainWindow { DataContext = vm };

        // Demarrer la revalidation periodique de la licence (toutes les 2h)
        _licenseService?.StartPeriodicCheck();

        desktop.MainWindow = mainWindow;
        // Maintenant on peut revenir au mode normal : fermer l'app quand MainWindow ferme
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
