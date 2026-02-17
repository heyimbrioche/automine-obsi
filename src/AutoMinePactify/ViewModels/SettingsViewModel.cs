using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoMinePactify.Models;
using AutoMinePactify.Services;

namespace AutoMinePactify.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Charge les settings depuis le fichier JSON.
    /// </summary>
    public void LoadFromDisk()
    {
        var s = ConfigService.Load();
        SelectedPattern = s.SelectedPattern;
        MiningDurationMs = s.MiningDurationMs;
        PresetName = s.PresetName;
        PickaxeSlot = s.PickaxeSlot;
        ActionDelayMs = s.ActionDelayMs;
        AntiChute = s.AntiChute;
        HumanizeDelays = s.HumanizeDelays;
        BlockCount = s.BlockCount;
        WallWidth = s.WallWidth;
        WallHeight = s.WallHeight;
        FloorWidth = s.FloorWidth;
        FloorDepth = s.FloorDepth;
        ColumnWidth = s.ColumnWidth;
        ColumnLength = s.ColumnLength;
        ColumnLayers = s.ColumnLayers;
        HomeName = s.HomeName;
        ColumnMovement = s.ColumnMovement;
        PlayerSafetyEnabled = s.PlayerSafetyEnabled;
        ScanRadius = s.ScanRadius;
    }

    /// <summary>
    /// Sauvegarde les settings dans le fichier JSON.
    /// </summary>
    public void SaveToDisk()
    {
        ConfigService.Save(new ConfigService.SavedSettings
        {
            SelectedPattern = SelectedPattern,
            MiningDurationMs = MiningDurationMs,
            PresetName = PresetName,
            PickaxeSlot = PickaxeSlot,
            ActionDelayMs = ActionDelayMs,
            AntiChute = AntiChute,
            HumanizeDelays = HumanizeDelays,
            BlockCount = BlockCount,
            WallWidth = WallWidth,
            WallHeight = WallHeight,
            FloorWidth = FloorWidth,
            FloorDepth = FloorDepth,
            ColumnWidth = ColumnWidth,
            ColumnLength = ColumnLength,
            ColumnLayers = ColumnLayers,
            HomeName = HomeName,
            ColumnMovement = ColumnMovement,
            PlayerSafetyEnabled = PlayerSafetyEnabled,
            ScanRadius = ScanRadius
        });
    }

    // ─── Choix du mode ──────────────────────────────────────────────

    [ObservableProperty]
    private MiningPatternType _selectedPattern = MiningPatternType.AutoClick;

    public int SelectedPatternIndex
    {
        get => (int)SelectedPattern;
        set
        {
            if (SelectedPattern != (MiningPatternType)value)
            {
                SelectedPattern = (MiningPatternType)value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void SelectMode(string index)
    {
        SelectedPatternIndex = int.Parse(index);
    }

    partial void OnSelectedPatternChanged(MiningPatternType value)
    {
        OnPropertyChanged(nameof(SelectedPatternIndex));
        OnPropertyChanged(nameof(IsAutoClick));
        OnPropertyChanged(nameof(IsWallBreaker));
        OnPropertyChanged(nameof(IsFloorMining));
        OnPropertyChanged(nameof(IsSmartMining));
        OnPropertyChanged(nameof(IsColumnMining));
        OnPropertyChanged(nameof(PatternDescription));
        OnPropertyChanged(nameof(SetupInstructions));
        OnPropertyChanged(nameof(TotalBlocks));
    }

    public bool IsAutoClick => SelectedPattern == MiningPatternType.AutoClick;
    public bool IsWallBreaker => SelectedPattern == MiningPatternType.WallBreaker;
    public bool IsFloorMining => SelectedPattern == MiningPatternType.FloorMining;
    public bool IsSmartMining => SelectedPattern == MiningPatternType.SmartMining;
    public bool IsColumnMining => SelectedPattern == MiningPatternType.ColumnMining;

    public string PatternDescription => SelectedPattern switch
    {
        MiningPatternType.AutoClick => "Tu te mets devant l'obsidienne et il casse tout seul, un bloc apres l'autre.",
        MiningPatternType.WallBreaker => "Il casse un mur entier d'obsidienne tout seul, de haut en bas.",
        MiningPatternType.FloorMining => "Il casse le sol d'obsidienne sous tes pieds en avancant tout seul.",
        MiningPatternType.ColumnMining => "Il mine une colonne position par position vers le bas, recule avec /home et decale avec /sethome.",
        MiningPatternType.SmartMining => "Il detecte l'obsidienne a l'ecran tout seul et la mine. Si un joueur s'approche, il te deco.",
        _ => ""
    };

    public string SetupInstructions => SelectedPattern switch
    {
        MiningPatternType.AutoClick =>
            "1. Mets ta pioche dans la bonne case de ta barre (en bas)\n" +
            "2. Va dans Minecraft et mets-toi DEVANT un bloc d'obsidienne\n" +
            "3. Mets ta croix (le viseur) bien sur le bloc\n" +
            "4. Appuie sur F6 pour lancer (t'as 3 secondes pour aller sur MC)\n" +
            "5. Il va cliquer tout seul pour casser les blocs un par un",
        MiningPatternType.WallBreaker =>
            "1. Mets ta pioche dans la bonne case de ta barre (en bas)\n" +
            "2. Va dans Minecraft et mets-toi DEVANT le mur d'obsidienne\n" +
            "3. Mets ta croix sur le bloc tout en HAUT A GAUCHE du mur\n" +
            "4. Reste proche du mur (1-2 blocs de distance)\n" +
            "5. Appuie sur F6 pour lancer (t'as 3 secondes pour aller sur MC)\n" +
            "6. Il casse de gauche a droite, puis descend a la ligne suivante",
        MiningPatternType.FloorMining =>
            "1. Mets ta pioche dans la bonne case de ta barre (en bas)\n" +
            "2. Va dans Minecraft et mets-toi dans un COIN du sol en obsidienne\n" +
            "3. Regarde droit devant toi (pas en bas, il le fait tout seul)\n" +
            "4. Appuie sur F6 pour lancer (t'as 3 secondes pour aller sur MC)\n" +
            "5. Il regarde en bas, casse le bloc, avance, et fait la ligne suivante",
        MiningPatternType.ColumnMining =>
            "1. Mets ta pioche dans la bonne case de ta barre (en bas)\n" +
            "2. Va sur la colonne d'obsidienne que tu veux miner\n" +
            "3. Place-toi AU DESSUS, dans un COIN, camera 90/90 (droit vers le bas)\n" +
            "4. Fais /sethome [nom] a cette position (ex: /sethome mine)\n" +
            "5. Mets le MEME nom de home dans les reglages\n" +
            "6. Choisis la profondeur, largeur, longueur et le mode de deplacement\n" +
            "7. Appuie sur F6 pour lancer (t'as 3 secondes)\n" +
            "8. Il recule, mine en profondeur, /home, et recommence\n" +
            "9. Il se decale a gauche avec /sethome pour les bandes suivantes",
        MiningPatternType.SmartMining =>
            "1. Mets ta pioche dans la bonne case de ta barre (en bas)\n" +
            "2. Minecraft doit etre en mode FENETRE (pas plein ecran)\n" +
            "3. Mets-toi devant de l'obsidienne et regarde un bloc\n" +
            "4. Clique sur \"Apprendre la couleur\" dans les reglages\n" +
            "5. Appuie sur F6 pour lancer (t'as 3 secondes)\n" +
            "6. Il trouve l'obsidienne tout seul et la mine !\n" +
            "7. Il s'arrete si ta pioche casse ou s'il y en a plus",
        _ => ""
    };

    // ─── Temps pour casser un bloc ──────────────────────────────────

    [ObservableProperty]
    private int _miningDurationMs = 2550;

    [ObservableProperty]
    private string _presetName = "Diamant Eff. V";

    // ─── Boutons rapides pour choisir ta pioche ─────────────────────

    [RelayCommand]
    private void PresetDiamond()
    {
        MiningDurationMs = 9400;
        PresetName = "Diamant sans enchant";
    }

    [RelayCommand]
    private void PresetDiamondEff()
    {
        MiningDurationMs = 2550;
        PresetName = "Diamant Eff. V";
    }

    [RelayCommand]
    private void PresetEmeraldEff()
    {
        MiningDurationMs = 2350;
        PresetName = "Emeraude Eff. V";
    }

    // ─── Auto-Clic : combien de blocs ───────────────────────────────

    [ObservableProperty]
    private int _blockCount = 20;

    // ─── Casse-Mur : taille du mur ──────────────────────────────────

    [ObservableProperty]
    private int _wallWidth = 5;

    [ObservableProperty]
    private int _wallHeight = 3;

    // ─── Sol : taille de la zone ────────────────────────────────────

    [ObservableProperty]
    private int _floorWidth = 5;

    [ObservableProperty]
    private int _floorDepth = 5;

    // ─── Colonne : dimensions + home ────────────────────────────────

    [ObservableProperty]
    private int _columnWidth = 2;

    [ObservableProperty]
    private int _columnLength = 2;

    [ObservableProperty]
    private int _columnLayers = 10;

    [ObservableProperty]
    private string _homeName = "mine";

    [ObservableProperty]
    private ColumnMoveMode _columnMovement = ColumnMoveMode.Walk;

    partial void OnColumnMovementChanged(ColumnMoveMode value)
    {
        OnPropertyChanged(nameof(IsColumnWalk));
        OnPropertyChanged(nameof(IsColumnSprint));
        OnPropertyChanged(nameof(IsColumnSneak));
    }

    public bool IsColumnWalk => ColumnMovement == ColumnMoveMode.Walk;
    public bool IsColumnSprint => ColumnMovement == ColumnMoveMode.Sprint;
    public bool IsColumnSneak => ColumnMovement == ColumnMoveMode.Sneak;

    [RelayCommand]
    private void SelectColumnMoveMode(string mode)
    {
        ColumnMovement = mode switch
        {
            "0" => ColumnMoveMode.Walk,
            "1" => ColumnMoveMode.Sprint,
            "2" => ColumnMoveMode.Sneak,
            _ => ColumnMoveMode.Walk
        };
    }

    // ─── Calcul du total de blocs ───────────────────────────────────

    partial void OnBlockCountChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnWallWidthChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnWallHeightChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnFloorWidthChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnFloorDepthChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnColumnWidthChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnColumnLengthChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));
    partial void OnColumnLayersChanged(int value) => OnPropertyChanged(nameof(TotalBlocks));

    public int TotalBlocks => SelectedPattern switch
    {
        MiningPatternType.AutoClick => BlockCount,
        MiningPatternType.WallBreaker => WallWidth * WallHeight,
        MiningPatternType.FloorMining => FloorWidth * FloorDepth,
        MiningPatternType.ColumnMining => ColumnWidth * ColumnLength * ColumnLayers,
        MiningPatternType.SmartMining => 999, // pas de limite, il s'arrete tout seul
        _ => 0
    };

    // ─── Full Auto : reglages ───────────────────────────────────────

    [ObservableProperty]
    private bool _playerSafetyEnabled = true;

    [ObservableProperty]
    private bool _isCalibrated;

    [ObservableProperty]
    private int _scanRadius = 300;

    // ─── Reglages communs ───────────────────────────────────────────

    [ObservableProperty]
    private int _pickaxeSlot = 1;

    [ObservableProperty]
    private int _actionDelayMs = 200;

    [ObservableProperty]
    private bool _antiChute = true;

    [ObservableProperty]
    private bool _humanizeDelays = true;

    // ─── Conversion ─────────────────────────────────────────────────

    public MiningConfig ToConfig() => new()
    {
        PatternType = SelectedPattern,
        MiningDurationMs = MiningDurationMs,
        ActionDelayMs = ActionDelayMs,
        PickaxeSlot = PickaxeSlot,
        AntiChute = AntiChute,
        HumanizeDelays = HumanizeDelays,
        BlockCount = BlockCount,
        WallWidth = WallWidth,
        WallHeight = WallHeight,
        FloorWidth = FloorWidth,
        FloorDepth = FloorDepth,
        ColumnWidth = ColumnWidth,
        ColumnLength = ColumnLength,
        ColumnLayers = ColumnLayers,
        HomeName = HomeName,
        ColumnMovement = ColumnMovement,
        PlayerSafetyEnabled = PlayerSafetyEnabled,
        ScanRadiusPixels = ScanRadius
    };
}
