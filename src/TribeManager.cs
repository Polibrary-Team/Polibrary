using BepInEx.Logging;
using HarmonyLib;
using Polibrary.Parsing;
using Polibrary.PolyScript;
using Polytopia.Data;


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
            if (string.IsNullOrEmpty(playerState.GetNameInternal()) && Parsing.Parse.leaderNameDict.TryGetValue(tribeData.type, out string name))
            {
                playerState.UserName = name;
            }
        }
    }
    #endregion
    
    #region Resource Overrides

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.AddResources))]
    public static void OverrideResourcesPass(MapData map, GameState gameState, float richness = 1)
    {
        foreach (TileData tile in map.tiles)
        {
            if (Parse.resourceOverrides.TryGetValue(tile.climate, out var overlist))
            {
                foreach (Parse.ResourceOverride o in overlist)
                {
                    if (tile.resource != null && tile.resource.type == o.og)
                    {
                        tile.resource = new ResourceState
                        {
                            type = o.neu  
                        };
                        break;
                    }
                }
            }
        }
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