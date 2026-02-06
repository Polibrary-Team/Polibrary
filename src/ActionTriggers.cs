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
using Il2CppSystem.Text.Json;
using pbb = PolytopiaBackendBase.Common;
using UnityEngine.Rendering.Universal.Internal;
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;


namespace Polibrary;

public class ActionData
{
    public string name;
    public WorldCoordinates origin;
    public byte owner;
    public ActionData(string name1, WorldCoordinates origin1, byte owner1)
    {
        name = name1;
        origin = origin1;
        owner = owner1;
    }
}

public static class ActionTriggers
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(ActionTriggers));
        modLogger = logger;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void BuildTriggers(BuildAction __instance, GameState gameState)
    {
        if (Parsing.Parse.improvementTriggers.TryGetValue(__instance.Type, out var dict))
        {
            if (dict.TryGetValue("onBuild", out string name))
            {
                PolibUtils.RunAction(name, __instance.Coordinates, __instance.PlayerId);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveAction), nameof(MoveAction.ExecuteDefault))]
    private static void MoveTriggers(MoveAction __instance, GameState gameState)
    {
        if (!gameState.TryGetUnit(__instance.UnitId, out UnitState unit))
        {
            modLogger.LogInfo("shits fucked");
            return;
        }

        List<ActionData> stack = new List<ActionData>();

        if (Parsing.Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onMove", out string name))
            {
                stack.Add(new ActionData(name, __instance.Path[0], __instance.PlayerId));
            }
            if (unitdict.TryGetValue("onMove_AtOrigin", out string name1))
            {
                stack.Add(new ActionData(name1, __instance.Path[__instance.Path.Count - 1], __instance.PlayerId));
            }
            foreach (WorldCoordinates coords in __instance.Path)
            {
                if (unitdict.TryGetValue("onMove_Path", out string name2))
                {
                    stack.Add(new ActionData(name2, coords, __instance.PlayerId));
                }
                if (unitdict.TryGetValue("onMove_Trail", out string name3) && coords.X != __instance.Path[0].X && coords.Y != __instance.Path[0].Y)
                {
                    stack.Add(new ActionData(name3, coords, __instance.PlayerId));
                }
            }
        }
        
        foreach (UnitAbility.Type type in unit.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(type, out var abilityDict))
            {
                if (abilityDict.TryGetValue("onMove", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Path[0], __instance.PlayerId));
                }
                if (abilityDict.TryGetValue("onMove_AtOrigin", out string name1))
                {
                    stack.Add(new ActionData(name1, __instance.Path[__instance.Path.Count - 1], __instance.PlayerId));
                }
                foreach (WorldCoordinates coords in __instance.Path)
                {
                    if (abilityDict.TryGetValue("onMove_Path", out string name2))
                    {
                        stack.Add(new ActionData(name2, coords, __instance.PlayerId));
                    }
                    if (abilityDict.TryGetValue("onMove_Trail", out string name3) && coords.X != __instance.Path[0].X && coords.Y != __instance.Path[0].Y)
                    {
                        stack.Add(new ActionData(name3, coords, __instance.PlayerId));
                    }
                }
            }
        }

        foreach (UnitEffect type in unit.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(type, out var effectDict))
            {
                if (effectDict.TryGetValue("onMove", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Path[0], __instance.PlayerId));
                }
                if (effectDict.TryGetValue("onMove_AtOrigin", out string name1))
                {
                    stack.Add(new ActionData(name1, __instance.Path[__instance.Path.Count - 1], __instance.PlayerId));
                }
                foreach (WorldCoordinates coords in __instance.Path)
                {
                    if (effectDict.TryGetValue("onMove_Path", out string name2))
                    {
                        stack.Add(new ActionData(name2, coords, __instance.PlayerId));
                    }
                    if (effectDict.TryGetValue("onMove_Trail", out string name3) && coords.X != __instance.Path[0].X && coords.Y != __instance.Path[0].Y)
                    {
                        stack.Add(new ActionData(name3, coords, __instance.PlayerId));
                    }
                }
            }
        }

        gameState.TryGetPlayer(unit.owner, out var player);
        TribeData tribe = gameState.GameLogicData.GetTribeData(player.tribe);
        foreach (TribeAbility.Type ability in tribe.tribeAbilities)
        {
            if (Parsing.Parse.tribeAbilityTriggers.TryGetValue(ability, out var tribeAbilitydict))
            {
                if (tribeAbilitydict.TryGetValue("onMove", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Path[0], __instance.PlayerId));
                }
                if (tribeAbilitydict.TryGetValue("onMove_AtOrigin", out string name1))
                {
                    stack.Add(new ActionData(name1, __instance.Path[__instance.Path.Count - 1], __instance.PlayerId));
                }
                foreach (WorldCoordinates coords in __instance.Path)
                {
                    if (tribeAbilitydict.TryGetValue("onMove_Path", out string name2))
                    {
                        stack.Add(new ActionData(name2, coords, __instance.PlayerId));
                    }
                    if (tribeAbilitydict.TryGetValue("onMove_Trail", out string name3) && coords.X != __instance.Path[0].X && coords.Y != __instance.Path[0].Y)
                    {
                        stack.Add(new ActionData(name3, coords, __instance.PlayerId));
                    }
                }
            }
        }

        foreach (WorldCoordinates coords in __instance.Path)
        {
            if (GameManager.GameState.Map.GetTile(coords).improvement == null) continue;
            
            if (Parsing.Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(coords).improvement.type, out var impdict))
            {
                if (impdict.TryGetValue("onStep", out string name))
                {
                    stack.Add(new ActionData(name, unit.coordinates, __instance.PlayerId));
                }
            }
        }

        if (GameManager.GameState.Map.GetTile(__instance.Path[0]).improvement != null)
        {
            if (Parsing.Parse.improvementTriggers.TryGetValue(GameManager.GameState.Map.GetTile(__instance.Path[0]).improvement.type, out var impdict1))
            {
                if (impdict1.TryGetValue("onLand", out string name))
                {
                    stack.Add(new ActionData(name, unit.coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackAction), nameof(AttackAction.Execute))]
    private static void AttackTriggers(AttackAction __instance, GameState state)
    {
        UnitState attacker = GameManager.GameState.Map.GetTile(__instance.Origin).unit;
        UnitState defender = GameManager.GameState.Map.GetTile(__instance.Target).unit;

        List<ActionData> stack = new List<ActionData>();

        foreach (UnitEffect effect in attacker.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onAttack_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (effectDict.TryGetValue("onAttack_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (UnitEffect effect in defender.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onAttacked_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (effectDict.TryGetValue("onAttacked_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }

        if (Parsing.Parse.unitTriggers.TryGetValue(attacker.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onAttack_AtDefender", out string name1))
            {
                stack.Add(new ActionData(name1, defender.coordinates, __instance.PlayerId));
            }
            if (unitdict.TryGetValue("onAttack_AtAttacker", out string name))
            {
                stack.Add(new ActionData(name, attacker.coordinates, __instance.PlayerId));
            }
        }

        if (Parsing.Parse.unitTriggers.TryGetValue(defender.type, out var unitdict1))
        {
            if (unitdict1.TryGetValue("onAttacked_AtDefender", out string name1))
            {
                stack.Add(new ActionData(name1, defender.coordinates, __instance.PlayerId));
            }
            if (unitdict1.TryGetValue("onAttacked_AtAttacker", out string name))
            {
                stack.Add(new ActionData(name, attacker.coordinates, __instance.PlayerId));
            }
        }

        foreach (UnitAbility.Type unitAbility in attacker.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(unitAbility, out var unitAbilitydict))
            {
                if (unitAbilitydict.TryGetValue("onAttack_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (unitAbilitydict.TryGetValue("onAttack_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (UnitAbility.Type unitAbility in defender.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(unitAbility, out var unitAbilitydict))
            {
                if (unitAbilitydict.TryGetValue("onAttacked_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (unitAbilitydict.TryGetValue("onAttacked_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }


        state.TryGetPlayer(attacker.owner, out var attackPlayer);
        TribeData atkTribe = state.GameLogicData.GetTribeData(attackPlayer.tribe);
        foreach (TribeAbility.Type ability in atkTribe.tribeAbilities)
        {
            if (Parsing.Parse.tribeAbilityTriggers.TryGetValue(ability, out var tribeAbilitydict))
            {
                if (tribeAbilitydict.TryGetValue("onAttack_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (tribeAbilitydict.TryGetValue("onAttack_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }

        state.TryGetPlayer(attacker.owner, out var defendPlayer);
        TribeData defTribe = state.GameLogicData.GetTribeData(defendPlayer.tribe);
        foreach (TribeAbility.Type ability in defTribe.tribeAbilities)
        {
            if (Parsing.Parse.tribeAbilityTriggers.TryGetValue(ability, out var tribeAbilitydict))
            {
                if (tribeAbilitydict.TryGetValue("onAttack_AtDefender", out string name3))
                {
                    stack.Add(new ActionData(name3, defender.coordinates, __instance.PlayerId));
                }
                if (tribeAbilitydict.TryGetValue("onAttack_AtAttacker", out string name2))
                {
                    stack.Add(new ActionData(name2, attacker.coordinates, __instance.PlayerId));
                }
            }
        }
        
        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.KillUnit))]
    private static void KillTriggers(GameState gameState, TileData tile)
    {
        if (tile.unit == null)
        {
            return;
        }
            
        List<ActionData> stack = new List<ActionData>();

        UnitState unit = tile.unit;

        if (Parsing.Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onKilled", out string name))
            {
                stack.Add(new ActionData(name, tile.coordinates, unit.owner));
            }
        }
        
        foreach (UnitAbility.Type type in unit.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(type, out var abilityDict))
            {
                if (abilityDict.TryGetValue("onKilled", out string name))
                {
                    stack.Add(new ActionData(name, tile.coordinates, unit.owner));
                }
            }
        }

        foreach (UnitEffect effect in unit.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onKilled", out string name))
                {
                    stack.Add(new ActionData(name, tile.coordinates, unit.owner));
                }
            }
        }

        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    private static void MoveCommandTriggers(MoveCommand __instance, GameState gameState)
    {
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);

        if (unit == null)
        {
            modLogger!.LogInfo("unit is null");
            return;
        }

        List<ActionData> stack = new List<ActionData>();

        if (Parsing.Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onMoveCommand", out string name))
            {
                stack.Add(new ActionData(name, unit.coordinates, __instance.PlayerId));
            }
        }

        foreach (UnitAbility.Type unitAbility in unit.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(unitAbility, out var unitAbilitydict))
            {
                if (unitAbilitydict.TryGetValue("onMoveCommand", out string name))
                {
                    stack.Add(new ActionData(name, unit.coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (UnitEffect effect in unit.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onMoveCommand", out string name))
                {
                    stack.Add(new ActionData(name, unit.coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ExpandCityAction), nameof(ExpandCityAction.ExecuteDefault))]
    private static void ExpandCityTriggers(ExpandCityAction __instance, GameState state)
    {
        TileData tile = state.Map.GetTile(__instance.Coordinates);
        state.TryGetPlayer(__instance.PlayerId, out var playerState);
        Il2Gen.List<TileData> cityAreaSorted = ActionUtils.GetCityAreaSorted(state, tile);
        cityAreaSorted.Reverse();

        foreach (TileData tile1 in cityAreaSorted)
        {
            if (tile1.improvement == null) continue;

            if (Parsing.Parse.improvementTriggers.TryGetValue(tile1.improvement.type, out var impdict))
            {
                if (impdict.TryGetValue("onExpand", out string name))
                {
                    PolibUtils.RunAction(name, tile1.coordinates, __instance.PlayerId);
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityRewardAction), nameof(CityRewardAction.Execute))]
    private static void CityRewardTriggers(CityRewardAction __instance, GameState state)
    {
        if (Parsing.Parse.rewardTriggers.TryGetValue(__instance.Reward, out var rewarddict))
        {
            if (rewarddict.TryGetValue("onRewardChosen", out string name))
            {
                PolibUtils.RunAction(name, __instance.Coordinates, __instance.PlayerId);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CaptureCityAction), nameof(CaptureCityAction.Execute))]
    private static void CaptureCityTriggers(CaptureCityAction __instance, GameState state)
    {
        List<ActionData> stack = new List<ActionData>();

        UnitState unit = state.Map.GetTile(__instance.Coordinates).unit;
        if (unit == null) return;
        
        if (Parsing.Parse.unitTriggers.TryGetValue(unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onCapture", out string name))
            {
                stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
            }
        }

        foreach (UnitAbility.Type unitAbility in unit.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(unitAbility, out var unitAbilitydict))
            {
                if (unitAbilitydict.TryGetValue("onCapture", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (UnitEffect effect in unit.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onCapture", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                }
            }
        }

        state.TryGetPlayer(unit.owner, out var player);
        TribeData tribe = state.GameLogicData.GetTribeData(player.tribe);
        foreach (TribeAbility.Type ability in tribe.tribeAbilities)
        {
            if (Parsing.Parse.tribeAbilityTriggers.TryGetValue(ability, out var tribeAbilitydict))
            {
                if (tribeAbilitydict.TryGetValue("onCapture", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                }
            }
        }

        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CaptureCityAction), nameof(CaptureCityAction.Execute))]
    private static bool CaptureCityTriggersPre(CaptureCityAction __instance, GameState state)
    {
        bool flag = true;
        
        List<ActionData> stack = new List<ActionData>();

        UnitState unit = state.Map.GetTile(__instance.Coordinates).unit;
        if (unit == null) return true;

        if (Parsing.Parse.unitTriggers.TryGetValue(state.Map.GetTile(__instance.Coordinates).unit.type, out var unitdict))
        {
            if (unitdict.TryGetValue("onCapture_Override", out string name))
            {
                stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                flag = false;
            }
        }

        foreach (UnitAbility.Type unitAbility in state.Map.GetTile(__instance.Coordinates).unit.UnitData.unitAbilities)
        {
            if (Parsing.Parse.unitAbilityTriggers.TryGetValue(unitAbility, out var unitAbilitydict))
            {
                if (unitAbilitydict.TryGetValue("onCapture_Override", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                    flag = false;
                }
            }
        }

        foreach (UnitEffect effect in state.Map.GetTile(__instance.Coordinates).unit.effects)
        {
            if (Parsing.Parse.unitEffectTriggers.TryGetValue(effect, out var effectDict))
            {
                if (effectDict.TryGetValue("onCapture_Override", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                    flag = false;
                }
            }
        }

        state.TryGetPlayer(unit.owner, out var player);
        TribeData tribe = state.GameLogicData.GetTribeData(player.tribe);
        foreach (TribeAbility.Type ability in tribe.tribeAbilities)
        {
            if (Parsing.Parse.tribeAbilityTriggers.TryGetValue(ability, out var tribeAbilitydict))
            {
                if (tribeAbilitydict.TryGetValue("onCapture_Override", out string name))
                {
                    stack.Add(new ActionData(name, __instance.Coordinates, __instance.PlayerId));
                    flag = false;
                }
            }
        }

        foreach (ActionData a in stack)
        {
            PolibUtils.RunAction(a.name, a.origin, a.owner);
        }
        
        return flag;
    }
}