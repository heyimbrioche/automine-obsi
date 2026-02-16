using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AutoMinePactify.Services;

namespace AutoMinePactify.Views;

public partial class SplashWindow : Window
{
    private readonly Border _loadingBar;
    private readonly TextBlock _loadingText;
    private readonly TextBlock _versionText;

    /// <summary>
    /// Resultat de la verification de mise a jour (dispo apres RunLoadingAnimation).
    /// </summary>
    public UpdateChecker.UpdateResult? UpdateResult { get; private set; }

    private readonly string[] _loadingMessages = new[]
    {
        "Initialisation...",
        "Chargement des modules...",
        "Verification des mises a jour...",
        "Preparation du minage...",
        "C'est parti !"
    };

    public SplashWindow()
    {
        InitializeComponent();

        _loadingBar = this.FindControl<Border>("LoadingBar")!;
        _loadingText = this.FindControl<TextBlock>("LoadingText")!;
        _versionText = this.FindControl<TextBlock>("VersionText")!;
    }

    public async Task RunLoadingAnimation()
    {
        // Afficher la version
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _versionText.Text = $"v{UpdateChecker.CurrentVersion}";
        });

        double maxWidth = 480 - 120;
        int totalSteps = _loadingMessages.Length;

        for (int i = 0; i < totalSteps; i++)
        {
            int step = i;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _loadingText.Text = _loadingMessages[step];
                double progress = (double)(step + 1) / totalSteps;
                _loadingBar.Width = maxWidth * progress;
            });

            // A l'etape "Verification des mises a jour", on verifie vraiment
            if (i == 2)
            {
                try
                {
                    UpdateResult = await UpdateChecker.CheckForUpdate();
                }
                catch
                {
                    // Pas grave si ca marche pas
                }
                await Task.Delay(100);
            }
            else
            {
                await Task.Delay(step < totalSteps - 1 ? 180 : 300);
            }
        }
    }
}
