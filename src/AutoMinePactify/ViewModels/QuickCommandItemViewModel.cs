using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoMinePactify.Models;

namespace AutoMinePactify.ViewModels;

/// <summary>
/// ViewModel pour une seule commande rapide dans la liste.
/// </summary>
public partial class QuickCommandItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "Commande";

    [ObservableProperty]
    private string _key = "F7";

    [ObservableProperty]
    private string _command = "/feed";

    [ObservableProperty]
    private QuickCommandSpeed _speed = QuickCommandSpeed.Fast;

    [ObservableProperty]
    private bool _enabled = true;

    // ─── Speed helpers ───────────────────────────────────────────────

    public bool IsSpeedSlow => Speed == QuickCommandSpeed.Slow;
    public bool IsSpeedNormal => Speed == QuickCommandSpeed.Normal;
    public bool IsSpeedFast => Speed == QuickCommandSpeed.Fast;
    public bool IsSpeedUltra => Speed == QuickCommandSpeed.Ultra;

    public string SpeedLabel => Speed switch
    {
        QuickCommandSpeed.Slow => "Lent",
        QuickCommandSpeed.Normal => "Normal",
        QuickCommandSpeed.Fast => "Rapide",
        QuickCommandSpeed.Ultra => "Ultra",
        _ => "?"
    };

    partial void OnSpeedChanged(QuickCommandSpeed value)
    {
        OnPropertyChanged(nameof(IsSpeedSlow));
        OnPropertyChanged(nameof(IsSpeedNormal));
        OnPropertyChanged(nameof(IsSpeedFast));
        OnPropertyChanged(nameof(IsSpeedUltra));
        OnPropertyChanged(nameof(SpeedLabel));
    }

    [RelayCommand]
    private void SetSpeed(string s)
    {
        Speed = s switch
        {
            "0" => QuickCommandSpeed.Slow,
            "1" => QuickCommandSpeed.Normal,
            "2" => QuickCommandSpeed.Fast,
            "3" => QuickCommandSpeed.Ultra,
            _ => QuickCommandSpeed.Fast
        };
    }

    // ─── Conversion ─────────────────────────────────────────────────

    public QuickCommandEntry ToEntry() => new()
    {
        Name = Name,
        Key = Key,
        Command = Command,
        Speed = Speed,
        Enabled = Enabled
    };

    public static QuickCommandItemViewModel FromEntry(QuickCommandEntry entry) => new()
    {
        Name = entry.Name,
        Key = entry.Key,
        Command = entry.Command,
        Speed = entry.Speed,
        Enabled = entry.Enabled
    };
}
