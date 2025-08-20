using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Linq.Expressions.Interpreter;
using JetBrains.Annotations;
using Polytopia.Data;
using PolytopiaBackendBase.Auth;
using PolytopiaBackendBase.Game;
using SevenZip.Compression.LZMA;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements.UIR;
using System.Reflection;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;


namespace Polibrary;

public static class ImprovementManager
{
    private static ManualLogSource GabrielLogOfHell;
    public static void Load(ManualLogSource logger)
    {
        GabrielLogOfHell = logger;
        GabrielLogOfHell.LogInfo("MACHINE, WHERE AM I?");
        GabrielLogOfHell.LogInfo("WHY ARE THERE MULTIPLE SCROLLS SCATTERED EVERYWHERE?");
        GabrielLogOfHell.LogInfo("MACHINE, IS THIS SOME KIND OF POLY-SCRIPT?");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void NewRequirers(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (!__result) return;
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_woundedbuilder")))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.health == tile.unit.GetMaxHealth(gameState))
            {
                __result = false;
                return;
            }
        }
        if (improvement.HasAbility(EnumCache<ImprovementAbility.Type>.GetType("polib_fullhealthbuilder")))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.health != tile.unit.GetMaxHealth(gameState))
            {
                __result = false;
                return;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltBySpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (Parse.BuildersDict.TryGetValue(improvement.type, out string ability))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (!tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType(ability)))
            {
                __result = false;
            }
        }
        if (Parse.NoBuildersDict.TryGetValue(improvement.type, out string ability2))
        {
            if (tile.unit == null)
            {
                __result = false;
                return;
            }
            if (tile.unit.HasAbility(EnumCache<UnitAbility.Type>.GetType(ability2)))
            {
                __result = false;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void mBuiltOnSpecific(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (Parse.ImpBuildersDict.TryGetValue(improvement.type, out string ability))
        {
            if (tile.improvement == null)
            {
                __result = false;
                return;
            }
            if (!Main.DataFromState(tile.improvement, gameState).HasAbility(EnumCache<ImprovementAbility.Type>.GetType(ability)))
            {
                __result = false;
                return;
            }
        }
    }
}