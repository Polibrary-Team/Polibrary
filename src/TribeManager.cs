using BepInEx.Logging;
using HarmonyLib;
using Polibrary.Parsing;
using Polibrary.PolyScript;
using Polytopia.Data;
using PolytopiaBackendBase.Common;


namespace Polibrary;

public static class TribeManager
{
    #region AI Leader Name
    // Simpler than it seems, and it seems very simple (Fapingvin, 2025)
    [HarmonyPrefix] //na azt jól megmondtad
    [HarmonyPatch(typeof(GameStateUtils), nameof(GameStateUtils.SetPlayerNames))]
    public static void OverridePlayerNames(GameState gameState)
    {
        foreach (PlayerState playerState in gameState.PlayerStates)
        {
            TribeData tribeData;
            gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData);

            
            if (
                string.IsNullOrEmpty(playerState.GetNameInternal()) &&
                PolibData.TryGetValue<PolibTribeData, TribeType, string>(Parse.polibTribeDatas, tribeData.type, "leaderName", out var name)
                )
            {
                playerState.UserName = name;
            }
        }
    }
    #endregion
    
    #region Climate Overrides

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateInternal))]
    public static void OverrideMapPass(MapData __result, int seed, GameState gameState, MapGeneratorSettings settings)
    {
        foreach (TileData tile in __result.tiles)
        {
            if (PolibData.TryFindData(Parse.polibTribeDatas, tile.climate, out var data))
            {
                if (data.terrainOverrides != null)
                {
                    if (data.terrainOverrides.TryGetValue(tile.terrain, out var newTerrain))
                    {
                        tile.terrain = newTerrain;
                    }
                }
                if (data.resourceOverrides != null)
                {
                    if (data.resourceOverrides.TryGetValue(tile.resource.type, out var newResource))
                    {
                        tile.resource = new ResourceState()
                        {
                            type = newResource
                        };
                    }
                }
            }
        }

        __result.GenerateShoreLines();
    }

    #endregion
    /* would need PreActionGameState
    [HarmonyPrefix] //fix for custom tribe spread and alienclimate waits so it works like polaris
    [HarmonyPatch(typeof(ClimateChangeAction), nameof(ClimateChangeAction.Execute))]
    private static bool ClimateChangeActionFix(ClimateChangeAction __instance, GameState gameState)
    {
        return __instance.Climate != gameState.Map.GetTile(__instance.Coordinates).climate;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClimateChangeReaction), nameof(ClimateChangeReaction.Execute))]
    private static bool ClimateChangeReactionFix(ClimateChangeReaction __instance, Il2CppSystem.Action onComplete)
    {
        if  (__instance.action.Climate == PolibActionManager.PreActionGameState.Map.GetTile(__instance.action.Coordinates).climate)
        {
            Main.modLogger.LogInfo("did");
            onComplete.Invoke();
            return false;
        }
        Main.modLogger.LogInfo("didnt");
        return true;
    }*/
}