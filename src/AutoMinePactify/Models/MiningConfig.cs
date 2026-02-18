namespace AutoMinePactify.Models;

public enum MiningPatternType
{
    AutoClick,
    WallBreaker,
    FloorMining,
    ColumnMining,
    SmartMining
}

/// <summary>
/// Mode de deplacement pour le minage en colonne.
/// Determine la vitesse a laquelle le bot recule et se decale.
/// </summary>
public enum ColumnMoveMode
{
    /// <summary>Marche normale (~4.3 blocs/s, ~232ms/bloc)</summary>
    Walk,
    /// <summary>Sprint (~5.6 blocs/s, ~178ms/bloc)</summary>
    Sprint,
    /// <summary>Accroupi (~1.3 blocs/s, ~772ms/bloc)</summary>
    Sneak
}

/// <summary>
/// Vitesse d'execution des commandes rapides (hotkey chat).
/// </summary>
public enum QuickCommandSpeed
{
    /// <summary>Lent : delais confortables, tres fiable</summary>
    Slow,
    /// <summary>Normal : bon equilibre vitesse/fiabilite</summary>
    Normal,
    /// <summary>Rapide : delais reduits</summary>
    Fast,
    /// <summary>Ultra : quasi instantane</summary>
    Ultra
}

public class MiningConfig
{
    public MiningPatternType PatternType { get; set; } = MiningPatternType.AutoClick;

    // ── Durée de minage par bloc (ms) ──
    // Défaut : Diamant Efficacité V (~2.55s)
    public int MiningDurationMs { get; set; } = 2550;

    // ── Délai entre actions (ms) ──
    public int ActionDelayMs { get; set; } = 200;

    // ── Slot pioche (1-9) ──
    public int PickaxeSlot { get; set; } = 1;

    // ── Sécurité ──
    public bool AntiChute { get; set; } = true;
    public bool HumanizeDelays { get; set; } = true;

    // ── Auto-Clic : nombre de blocs à miner ──
    public int BlockCount { get; set; } = 20;

    // ── Casse-Mur : dimensions du mur ──
    public int WallWidth { get; set; } = 5;
    public int WallHeight { get; set; } = 3;

    // ── Minage de Sol : dimensions de la zone ──
    public int FloorWidth { get; set; } = 5;
    public int FloorDepth { get; set; } = 5;

    // ── Colonne : dimensions de la colonne + home ──
    public int ColumnWidth { get; set; } = 2;
    public int ColumnLength { get; set; } = 2;
    public int ColumnLayers { get; set; } = 10;
    public string HomeName { get; set; } = "mine";
    public ColumnMoveMode ColumnMovement { get; set; } = ColumnMoveMode.Walk;

    // ── Full Auto : detection joueurs + rayon de scan ──
    public bool PlayerSafetyEnabled { get; set; } = true;
    public int ScanRadiusPixels { get; set; } = 300;

}
