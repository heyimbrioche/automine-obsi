using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AutoMinePactify.Services;

namespace AutoMinePactify.Views;

[SupportedOSPlatform("windows")]
public partial class SplashWindow : Window
{
    private readonly Border _loadingBar;
    private readonly TextBlock _loadingText;
    private readonly TextBlock _versionText;

    public UpdateChecker.UpdateResult? UpdateResult { get; private set; }
    public LicenseResult? LicenseCheckResult { get; private set; }

    private readonly string[] _loadingMessages = new[]
    {
        "Initialisation...",
        "Verification de la licence...",
        "Chargement des modules...",
        "Verification des mises a jour...",
        "Preparation du minage...",
        "C'est parti !"
    };

    private readonly LicenseService _licenseService;

    public SplashWindow() : this(new LicenseService()) { }

    public SplashWindow(LicenseService licenseService)
    {
        InitializeComponent();
        _licenseService = licenseService;

        _loadingBar = this.FindControl<Border>("LoadingBar")!;
        _loadingText = this.FindControl<TextBlock>("LoadingText")!;
        _versionText = this.FindControl<TextBlock>("VersionText")!;
    }

    public async Task RunLoadingAnimation()
    {
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

            if (step == 1)
            {
                try
                {
                    LicenseCheckResult = await _licenseService.ValidateCachedAsync();
                }
                catch
                {
                    LicenseCheckResult = new LicenseResult
                    {
                        Status = LicenseStatus.InvalidKey,
                        Message = "Erreur lors de la verification."
                    };
                }
                await Task.Delay(100);
            }
            else if (step == 3)
            {
                try
                {
                    UpdateResult = await UpdateChecker.CheckForUpdate();
                }
                catch { }
                await Task.Delay(100);
            }
            else
            {
                await Task.Delay(step < totalSteps - 1 ? 180 : 300);
            }
        }
    }
}
