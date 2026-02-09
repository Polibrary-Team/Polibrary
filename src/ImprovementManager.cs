using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;

using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;
using Polibrary.Parsing;


namespace Polibrary;

// imp = improvement

public static class ImprovementManager
{
    private static ManualLogSource GabrielLogOfHell;
    public static void Load(ManualLogSource logger)
    {
        GabrielLogOfHell = logger;
        Harmony.CreateAndPatchAll(typeof(ImprovementManager));

    }

    #region Can build imp?

    // check for every single prebuilt ability once to reduce number of patches, loops and LoC
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void OnePatchToRuleThemAll(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (!improvementFlag(gameState, tile, playerState, improvement))
        {
            __result = false;
        }
    }

    public static bool improvementFlag(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement)
    {

        if (tile.unit != null)
        {
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_woundedbuilder")) && tile.unit.health == tile.unit.GetMaxHealth(gameState))
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_fullhealthbuilder")) && tile.unit.health != tile.unit.GetMaxHealth(gameState))
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_veteranbuilder")) && tile.unit.promotionLevel < 1)
                return false;
            if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_nobuilder")))
                return false;
        }

        if(impNeighborCheckers(gameState, tile, playerState, improvement)) return false;

        if(PolibUtils.IsTileNative(playerState, tile, gameState))
        {
            if(improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_foreign")))
                return false;
        }
        else if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_native"))) return false;

        return true;
    }

    public static bool impNeighborCheckers(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement) // true iff imp fails
    {
        // Next the check-neighboring-tiles requirements
        // the flags indicate their success
        bool needsfriendly_success = false;
        bool needsenemy_success = false;
        bool isolated_success = true;

        foreach(var tile1 in gameState.Map.GetTileNeighbors(tile.coordinates))
        {
            if(tile1 != null && tile1.coordinates != WorldCoordinates.NULL_COORDINATES)
            {
                if(tile1.unit != null)
                {
                    if(tile1.unit.owner == playerState.Id) needsfriendly_success = true;
                    else needsenemy_success = true;
                }
                if(tile1.improvement != null)
                {
                    if(tile1.improvement.type == improvement.type) isolated_success = false;
                }
            }
        }

        return (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsfriendly")) && !needsfriendly_success) || (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_needsenemy")) && !needsenemy_success) || (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_isolated")) && !isolated_success);
    }
    #endregion



    #region GLD Builders

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CheckImprovementPlaceability(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        int idx = PolibData.FindData<PolibImprovementData, ImprovementData.Type>(Parse.polibImprovementDatas, improvement.type);
        if(idx < 0) return; // It is cleaner here to use index rather than PD.TryGetValue()

        var allowAbilityList = Parse.polibImprovementDatas[idx].unitAbilityWhitelist;
        var denyAbilityList = Parse.polibImprovementDatas[idx].unitAbilityBlacklist;
        var allowList = Parse.polibImprovementDatas[idx].unitWhitelist;
        var denyList = Parse.polibImprovementDatas[idx].unitBlacklist;
        
        if (allowAbilityList == null && denyAbilityList == null && allowList == null && denyList == null) return;
        if (tile.unit == null)
        {
            return;
        }

        bool value = false;
        if (denyAbilityList != null || denyList != null) value = true;
        if (allowAbilityList != null || allowList != null) value = false;

        foreach (UnitAbility.Type type in tile.unit.UnitData.unitAbilities)
        {
            if (allowAbilityList != null && allowAbilityList.Contains(type)) value = true;
            if (denyAbilityList != null && denyAbilityList.Contains(type)) value = false;
        }
        if (allowList != null && allowList.Contains(tile.unit.type)) value = true;
        if (denyList != null && denyList.Contains(tile.unit.type)) value = false;

        __result = value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltOnSpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if(PolibData.TryGetValue(Parse.polibImprovementDatas, improvement.type, nameof(PolibImprovementData.builtOnSpecific), out string ability))
        {
            if (tile.improvement == null)
            {
                __result = false;
                return;
            }
            if (!PolibUtils.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType(ability)))
            {
                __result = false;
                return;
            }
        }
    }

    #endregion

    #region Demolishable
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandUtils), nameof(CommandUtils.GetImprovementAbilities))]
    public static void Can_Demolish_This_Actually(ref Il2CppSystem.Collections.Generic.List<CommandBase> __result, GameState gameState, PlayerState player, TileData tile, bool includeUnavailable = false)
    {
        if (player.Id != gameState.CurrentPlayer)
        {
            return;
        }
        if (tile == null || tile.improvement == null)
        {
            return;
        }

        if (tile.CanDestroy(gameState, player) && gameState.GameLogicData.TryGetData(tile.improvement.type, out ImprovementData improvementData))
        {
            if (!player.HasAbility(PlayerAbility.Type.Destroy, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe != pbb.TribeType.Cymanti)
            {
                __result.Add(new DestroyCommand(player.Id, tile.coordinates));
            }
            if (!tile.improvement.HasEffect(ImprovementEffect.decomposing) && !player.HasAbility(PlayerAbility.Type.Decompose, gameState) && improvementData.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_demolishable")) && player.tribe == pbb.TribeType.Cymanti)
            {
                __result.Add(new DecomposeCommand(player.Id, tile.coordinates));
            }
        }
    }
    #endregion

    #region OnBuild Actions

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void BuildActionnaire(BuildAction __instance, GameState gameState)
    {
        var data = PolibUtils.DataFromType(__instance.Type);
        TileData tile = gameState.Map.GetTile(__instance.Coordinates);
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState player);

        if (tile == null)
        {
            return;
        }
        if (data == null || player == null)
        {
            return;
        }

        if (tile.unit != null)
        {
            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_healonce")))
            {
                PolibUtils.HealUnit(gameState, tile.unit, 40);
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_cleanseonce")))
            {
                PolibUtils.CleanseUnit(tile.unit);
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_gainxp")))
            {
                tile.unit.xp += 3;
            }

            if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_killunit")))
            {
                gameState.ActionStack.Add(new KillUnitAction(tile.unit.owner, tile.coordinates));
            }
        }

        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_research")))
        {

            var unlockableTech = PolibUtils.polibGetUnlockableTech(player);
            if (unlockableTech != null && unlockableTech.Count != 0)
            {
                var tech = unlockableTech[gameState.RandomHash.Range(0, unlockableTech.Count, tile.coordinates.X, tile.coordinates.Y)];
                gameState.ActionStack.Add(new ResearchAction(player.Id, tech.type, 0));
            }
        }
        if (data.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_reveal")))
        {
            foreach (var tile2 in gameState.Map.GetArea(tile.coordinates, 2, true, true))
            {
                if (tile2 != null && tile2.coordinates != WorldCoordinates.NULL_COORDINATES)
                {
                    gameState.ActionStack.Add(new ExploreAction(player.Id, tile2.coordinates));
                }
            }
        }

    }

    #endregion

    #region AI

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AI), nameof(AI.GetImprovementScore))]
    public static void AIBoost(ref float __result, GameState gameState, ImprovementData improvementData, TileData tileData, PlayerState player)
    {
        if (!gameState.GameLogicData.CanBuild(gameState, tileData, player, improvementData))
            return;
        if(PolibData.TryGetValue(Parse.polibImprovementDatas, improvementData.type, nameof(PolibImprovementData.aiScore), out float? result))
            __result += (float)result;
    }


    #endregion
}