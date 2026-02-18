namespace AutoMinePactify.Models;

/// <summary>
/// Represente une commande rapide configurable (hotkey → commande chat).
/// </summary>
public class QuickCommandEntry
{
    /// <summary>Nom affichable (ex: "Feed", "Heal", "Fly").</summary>
    public string Name { get; set; } = "Commande";

    /// <summary>Nom de la touche (ex: "F7", "G").</summary>
    public string Key { get; set; } = "F7";

    /// <summary>Commande a executer dans le chat (ex: "/feed").</summary>
    public string Command { get; set; } = "/feed";

    /// <summary>Vitesse d'execution.</summary>
    public QuickCommandSpeed Speed { get; set; } = QuickCommandSpeed.Fast;

    /// <summary>Active ou non.</summary>
    public bool Enabled { get; set; } = true;
}
