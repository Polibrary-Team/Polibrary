using BepInEx.Logging;
using HarmonyLib;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polytopia.Data;
using Polytopia.IO;
using PolytopiaBackendBase.Common;


namespace Polibrary;

public class PolibGameState
{
    [JsonInclude]
    public Dictionary<UnitData.Type, int> rewardBoostDict = new Dictionary<UnitData.Type, int>();

    [JsonInclude]
    public Dictionary<string, object> globalVariables = new Dictionary<string, object>();
}

public class WorldCoordinates2Json : JsonConverter<WorldCoordinates>
{
    public override WorldCoordinates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        List<int> values = new();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.Number) throw new JsonException();
                values.Add(reader.GetInt32());
            }
        }
        if (values.Count != 2) throw new JsonException();
        return new(values[0], values[1]);
    }

    public override void Write(Utf8JsonWriter writer, WorldCoordinates value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteEndArray();
    }
}

public static class PolibSave
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(PolibSave));
        modLogger = logger;
    }

    internal static readonly string DATA_PATH = Path.Combine(PolyMod.Plugin.BASE_PATH, "PolibraryData");

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameState), nameof(GameState.Serialize))]
    private static void Save(GameState __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        if (Main.polibGameState == null)
        {
            Main.polibGameState = new PolibGameState();
        }

        SaveToCurrentState(Main.polibGameState, __instance.Seed);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameState), nameof(GameState.Deserialize))]
    private static void Load(GameState __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        Main.polibGameState = LoadCurrentState(__instance.Seed);
    }


    public static void SaveToCurrentState(PolibGameState data, int Seed)
    {
        string filePath = Path.Combine(DATA_PATH, $"State{Seed}.json");

        if (data == null)
        {
            modLogger.LogInfo("data is null");
            return;
        }
        
        File.WriteAllText(
            filePath,
            JsonSerializer.Serialize
            (
                data,
                new JsonSerializerOptions 
                {
                    WriteIndented = true,
                    Converters = 
                    { 
                        new WorldCoordinates2Json(),
                        new PolyMod.Json.EnumCacheJson<UnitData.Type>(),
                        new PolyMod.Json.EnumCacheJson<ImprovementData.Type>(),
                        new PolyMod.Json.EnumCacheJson<TribeType>(),
                        new PolyMod.Json.EnumCacheJson<TechData.Type>(),
                        new PolyMod.Json.EnumCacheJson<CityReward>(),
                        new PolyMod.Json.EnumCacheJson<TileData.EffectType>(),
                        new PolyMod.Json.EnumCacheJson<UnitEffect>()
                    }
                }
            )
        );
    }

    public static PolibGameState LoadCurrentState(int Seed)
    {
        string filePath = Path.Combine(DATA_PATH, $"State{Seed}.json");

        if (!File.Exists(filePath))
        {
            return null;
        }
        PolibGameState data = null;
        string json = File.ReadAllText(filePath);
        try
        {
            data = JsonSerializer.Deserialize<PolibGameState>
            (
                json, new JsonSerializerOptions()
                {
                    Converters = 
                    {
                        new WorldCoordinates2Json(),
                        new PolyMod.Json.EnumCacheJson<UnitData.Type>(),
                        new PolyMod.Json.EnumCacheJson<ImprovementData.Type>(),
                        new PolyMod.Json.EnumCacheJson<TribeType>(),
                        new PolyMod.Json.EnumCacheJson<TechData.Type>(),
                        new PolyMod.Json.EnumCacheJson<CityReward>(),
                        new PolyMod.Json.EnumCacheJson<TileData.EffectType>(),
                        new PolyMod.Json.EnumCacheJson<UnitEffect>()
                    },
                }
            );
        }
        catch (Exception ex)
        {
            modLogger.LogInfo($"shat my pants while loading polib gamestate: {ex.Message}");
        }

        return data;
    }

    /*
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager))]*/
    private static void DeleteUnnecessaryShit()
    {
        string saveDirectoryPath = Paths.GetSaveDirectoryPath("Singleplayer");
        if (!PolytopiaDirectory.Exists(saveDirectoryPath))
        {
            return;
        }
        string[] files = PolytopiaDirectory.GetFiles(saveDirectoryPath, "*.state");

        List<string> filesToIgnore = new List<string>();

        foreach (string file in files)
        {
            byte[] data = PolytopiaFile.ReadAllBytes(file);
            SerializationHelpers.FromByteArray<GameState>(data, out var state);
            if (state == null)
            {
                modLogger.LogInfo("state is null uh oh");
                continue;
            }
            string filePath = Path.Combine(DATA_PATH, $"State{state.Seed}.json");
            if (!File.Exists(filePath))
            {
                modLogger.LogInfo("state file doesnt exist");
                continue;
            }
            
        }
    }
}