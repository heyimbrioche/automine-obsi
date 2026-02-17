using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMinePactify.Models;

namespace AutoMinePactify.Services;

/// <summary>
/// Sauvegarde et charge les settings dans un fichier JSON
/// a cote de l'exe (settings.json).
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoMinePactify");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Donnees sauvegardees dans le fichier JSON.
    /// </summary>
    public class SavedSettings
    {
        // Mode
        public MiningPatternType SelectedPattern { get; set; } = MiningPatternType.AutoClick;

        // Pioche
        public int MiningDurationMs { get; set; } = 2550;
        public string PresetName { get; set; } = "Diamant Eff. V";
        public int PickaxeSlot { get; set; } = 1;

        // Commun
        public int ActionDelayMs { get; set; } = 200;
        public bool AntiChute { get; set; } = true;
        public bool HumanizeDelays { get; set; } = true;

        // Auto-Clic
        public int BlockCount { get; set; } = 20;

        // Mur
        public int WallWidth { get; set; } = 5;
        public int WallHeight { get; set; } = 3;

        // Sol
        public int FloorWidth { get; set; } = 5;
        public int FloorDepth { get; set; } = 5;

        // Colonne
        public int ColumnWidth { get; set; } = 2;
        public int ColumnLength { get; set; } = 2;
        public int ColumnLayers { get; set; } = 10;
        public string HomeName { get; set; } = "mine";
        public ColumnMoveMode ColumnMovement { get; set; } = ColumnMoveMode.Walk;

        // Full Auto
        public bool PlayerSafetyEnabled { get; set; } = true;
        public int ScanRadius { get; set; } = 300;
    }

    /// <summary>
    /// Charge les settings depuis le fichier JSON.
    /// Retourne les valeurs par defaut si le fichier n'existe pas.
    /// </summary>
    public static SavedSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new SavedSettings();

            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<SavedSettings>(json, JsonOptions) ?? new SavedSettings();
        }
        catch
        {
            return new SavedSettings();
        }
    }

    /// <summary>
    /// Sauvegarde les settings dans le fichier JSON.
    /// </summary>
    public static void Save(SavedSettings settings)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Silencieux si on ne peut pas sauvegarder
        }
    }
}
