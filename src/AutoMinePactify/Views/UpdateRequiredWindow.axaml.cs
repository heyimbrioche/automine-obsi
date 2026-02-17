using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AutoMinePactify.Services;

namespace AutoMinePactify.Views;

public partial class UpdateRequiredWindow : Window
{
    private readonly string _downloadUrl;
    private readonly Border _progressBar;
    private readonly TextBlock _progressText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _titleText;
    private readonly Button _retryButton;
    private readonly Button _quitButton;
    private readonly double _maxBarWidth;

    public UpdateRequiredWindow()
    {
        InitializeComponent();
        _downloadUrl = "";
        _progressBar = this.FindControl<Border>("ProgressBar")!;
        _progressText = this.FindControl<TextBlock>("ProgressText")!;
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _titleText = this.FindControl<TextBlock>("TitleText")!;
        _retryButton = this.FindControl<Button>("RetryButton")!;
        _quitButton = this.FindControl<Button>("QuitButton")!;
        _maxBarWidth = 500 - 100; // largeur fenetre - marges
    }

    public UpdateRequiredWindow(UpdateChecker.UpdateInfo info) : this()
    {
        _downloadUrl = info.DownloadUrl;

        var versionInfo = this.FindControl<TextBlock>("VersionInfo")!;
        var changelogText = this.FindControl<TextBlock>("ChangelogText")!;
        var currentVersionText = this.FindControl<TextBlock>("CurrentVersionText")!;

        versionInfo.Text = $"v{UpdateChecker.CurrentVersion}  →  v{info.Version}";

        if (!string.IsNullOrWhiteSpace(info.Changelog))
            changelogText.Text = info.Changelog;

        currentVersionText.Text = $"Version actuelle : v{UpdateChecker.CurrentVersion}";

        _retryButton.Click += OnRetryClick;
        _quitButton.Click += OnQuitClick;

        // Lancer le telechargement automatiquement
        StartAutoUpdate();
    }

    private void StartAutoUpdate()
    {
        _retryButton.IsVisible = false;
        _quitButton.IsVisible = false;

        AutoUpdater.OnProgressChanged += OnProgress;
        AutoUpdater.OnStatusChanged += OnStatus;

        Task.Run(async () =>
        {
            await Task.Delay(500); // petit delai pour que la fenetre s'affiche bien

            bool success = await AutoUpdater.DownloadAndUpdate(_downloadUrl);

            if (success)
            {
                // Le script batch va prendre le relais, on quitte
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _titleText.Text = "REDEMARRAGE...";
                    _statusText.Text = "Le programme va se relancer automatiquement.";
                });

                await Task.Delay(1000);
                Environment.Exit(0);
            }
            else
            {
                // Erreur : montrer les boutons
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _titleText.Text = "ERREUR DE MISE A JOUR";
                    _titleText.Foreground = Avalonia.Media.Brushes.Tomato;
                    _retryButton.IsVisible = true;
                    _quitButton.IsVisible = true;
                });
            }
        });
    }

    private void OnProgress(double progress)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _progressBar.Width = _maxBarWidth * (progress / 100.0);
            _progressText.Text = $"{progress:F0}%";
        });
    }

    private void OnStatus(string status)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _statusText.Text = status;
        });
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        _titleText.Text = "MISE A JOUR DISPONIBLE";
        _titleText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A855F7"));
        _progressBar.Width = 0;
        _progressText.Text = "0%";
        StartAutoUpdate();
    }

    private void OnQuitClick(object? sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    protected override void OnClosed(EventArgs e)
    {
        AutoUpdater.OnProgressChanged -= OnProgress;
        AutoUpdater.OnStatusChanged -= OnStatus;
        base.OnClosed(e);
    }
}
