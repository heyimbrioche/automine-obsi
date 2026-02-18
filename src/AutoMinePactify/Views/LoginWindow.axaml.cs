using System;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AutoMinePactify.Services;

namespace AutoMinePactify.Views;

[SupportedOSPlatform("windows")]
public partial class LoginWindow : Window
{
    private readonly LicenseService _licenseService;
    private readonly TextBox _keyInput;
    private readonly Button _activateButton;
    private readonly TextBlock _errorText;
    private readonly TextBlock _hwidHint;

    public bool IsLicenseValid { get; private set; }

    public LoginWindow() : this(new LicenseService()) { }

    public LoginWindow(LicenseService licenseService)
    {
        InitializeComponent();
        _licenseService = licenseService;

        _keyInput = this.FindControl<TextBox>("KeyInput")!;
        _activateButton = this.FindControl<Button>("ActivateButton")!;
        _errorText = this.FindControl<TextBlock>("ErrorText")!;
        _hwidHint = this.FindControl<TextBlock>("HwidHint")!;
    }

    private async void OnActivateClick(object? sender, RoutedEventArgs e)
    {
        string key = _keyInput.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(key))
        {
            _errorText.Text = "Entre ta cle de licence.";
            return;
        }

        _activateButton.IsEnabled = false;
        _activateButton.Content = "Verification...";
        _errorText.Text = "";
        _hwidHint.Text = "";

        var result = await _licenseService.ValidateAsync(key);

        if (result.Status == LicenseStatus.Valid)
        {
            IsLicenseValid = true;
            Close();
            return;
        }

        _activateButton.IsEnabled = true;
        _activateButton.Content = "Activer";
        _errorText.Text = result.Message;

        if (result.Status == LicenseStatus.HwidMismatch)
        {
            _hwidHint.Text = "Contacte le vendeur pour demander un reset HWID.";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        IsLicenseValid = false;
        Close();
    }
}
