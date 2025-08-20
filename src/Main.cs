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

public static class Main
{


    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        new Harmony("com.polibraryteam.polibrary").PatchAll();
        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
        PolyMod.Loader.AddPatchDataType("cityRewardData", typeof(CityReward)); //casual fapingvin carry
        PolyMod.Loader.AddPatchDataType("unitEffectData", typeof(UnitEffect)); //casual fapingvin carry... ...again
        PolyMod.Loader.AddPatchDataType("unitAbilityData", typeof(UnitAbility.Type)); //...casual...      ...fapingvin carry...       ...again
    }

    
    //                    ██                   
    //                  ██████                 
    //                 ███  ███                
    //               ███      ███              
    //              ███   ██   ███             
    //            ███     ██     ███           
    //           ███      ██      ███          
    //          ██        ██        ██         
    //        ███         ██         ███       
    //       ██           ██           ██      
    //     ███            ██            ███    
    //    ██                              ██   
    //  ███               ██               ███ 
    // ██                                    ██
    // ████████████████████████████████████████
    //                                         
    // ░█░█░░░█░█░█▀█░█▀▄░█▀█░▀█▀░█▀█░█▀▀░░░█░█
    // ░▀░▀░░░█▄█░█▀█░█▀▄░█░█░░█░░█░█░█░█░░░▀░▀
    // ░▀░▀░░░▀░▀░▀░▀░▀░▀░▀░▀░▀▀▀░▀░▀░▀▀▀░░░▀░▀
    //
    // Beyond lies the code graveyard
    // Enter with caution!
    

    

    
    /*
    #region Intercept
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static bool Intercept(AttackCommand __instance, GameState gameState)
    {
        UnitState aggressor;
        gameState.TryGetUnit(__instance.UnitId, out aggressor);
        TileData tile = gameState.Map.GetTile(__instance.Target);
        UnitState unit = tile.unit;
        PlayerState playerState;
        gameState.TryGetPlayer(__instance.PlayerId, out playerState);
        PlayerState defender;
        gameState.TryGetPlayer(unit.owner, out defender);
        BattleResults battleResults = BattleHelpers.GetBattleResults(gameState, aggressor, unit);

        if (battleResults.retaliationDamage > 0 && unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState))
        {

            if (battleResults.attackDamage > 0)
            {
                gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, __instance.Target, battleResults.attackDamage, battleResults.shouldMoveToDefeatedEnemyTile, AttackAction.AnimationType.Normal, 250));
            }
            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Target, __instance.Origin, battleResults.retaliationDamage, false, AttackAction.AnimationType.Normal, 100));

            if (battleResults.shouldMoveToDefeatedEnemyTile)
            {
                gameState.CommandStack.Add(new MoveCommand(__instance.PlayerId, aggressor, __instance.Target));
            }
            aggressor.moved = !aggressor.HasAbility(UnitAbility.Type.Escape, gameState);
            aggressor.attacked = true;

            return false;
        }

        return true;
    }

    [HarmonyPostfix] //retaliation calculation is BUUULLSHIT!
    [HarmonyPatch(typeof(BattleHelpers), nameof(BattleHelpers.GetBattleResults))] //yeah, it is, like many things in poly source code
    public static void InterceptRetaliationDamage(GameState gameState, UnitState attackingUnit, UnitState defendingUnit, ref BattleResults __result)
    {
        bool flag = (int)attackingUnit.health <= __result.retaliationDamage;

        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag)
        {
            __result.attackDamage = 0;
        }
        bool flag2 = (int)defendingUnit.health <= __result.attackDamage;
        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag2)
        {

            // FUUUUUUUUUUCK is this really the only way??
            // if damage calculator is ever changed then this is worthless!!!
            // But afaik only battlehelpers calculates damage, but it sets it to 0 if the unit would die
            long num = 45L; //o7 fap
            long num2 = (long)attackingUnit.GetMaxHealth(gameState);
            long num3 = (long)defendingUnit.GetMaxHealth(gameState);
            long num4 = (long)(attackingUnit.GetAttack(gameState) * (int)attackingUnit.health * 100) / num2;
            long num5 = (long)(defendingUnit.GetDefence(gameState) * (int)defendingUnit.health * 100) / num3;
            long num6 = num4 + num5;
            long num7 = (long)defendingUnit.GetDefenceBonus(gameState);

            __result.retaliationDamage = BattleHelpers.Round((int)(num5 * (long)defendingUnit.GetDefence(gameState) * num * 100L / (1000L * num6 * num7))); ;
        }

        if (defendingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_intercept"), gameState) && flag)
        {
            __result.attackDamage = 0;
        }

        if (__result.attackDamage >= defendingUnit.health && attackingUnit.GetRange(gameState) == 1 && !attackingUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("polib_lazy")))
        {
            __result.shouldMoveToDefeatedEnemyTile = true;
        }
    }
    #endregion*/

    

    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameStateUtils), nameof(GameStateUtils.SetPlayerNames))]
    public static void GameStateUtils_SetPlayerNames(GameState gameState)
    {
        foreach (PlayerState playerState in gameState.PlayerStates)
        {
            TribeData tribeData;
            gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData);
            if ((playerState.GetNameInternal == null || playerState.GetNameInternal() == "") && leaderNameDict.TryGetValue(tribeData.type, out var name))
            {
                playerState.UserName = name;
                LogMan1997?.LogInfo($"Named {tribeData.type} as {name}, their current name: {playerState.UserName}");
            }
        }
        LogMan1997?.LogInfo($"Tried naming tribes");
    }*/

    

    
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetSightRange))]
    public static void UnitDataExtensions_GetSightRange(this UnitData unitData, ref int __result)
    {
        int num = 1;
        foreach (UnitAbility.Type abilityType in unitData.unitAbilities)
        {
            if (unitAbilityDataDict.TryGetValue(abilityType, out var data))
            {
                num = (data.visionRadius <= num) ? data.visionRadius : num;
            }
        }
        __result = num;
    } 

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.CheckStepOnPoison))]
    public static void ActionUtils_CheckStepOnPoison(TileData targetTile, UnitState unit, GameState gameState)
    {
        ImprovementData improvementData;
        PlayerState player;
        bool b = false;
        foreach (UnitAbility.Type abilityType in gameState.GameLogicData.GetUnitData(unit.type).unitAbilities)
        {
            if (unitAbilityDataDict.TryGetValue(abilityType, out var data))
            {
                b = data.allowsFly ? true : b; //FUCKING FUCK FUCK THIS SHIT FUCKING FUCK SHIT FUCK
            }
        }
        if (targetTile.improvement != null && gameState.GameLogicData.TryGetData(targetTile.improvement.type, out improvementData) && improvementData != null && improvementData.HasAbility(ImprovementAbility.Type.Poison) && gameState.TryGetPlayer(unit.owner, out player) && !player.HasTribeAbility(TribeAbility.Type.PoisonResist, gameState) && !b)
            {
                gameState.ActionStack.Add(new PoisonUnitAction(unit.owner, targetTile.coordinates, targetTile.coordinates));
                if (gameState.Version < 83)
                {
                    gameState.ActionStack.Add(new AttackAction(unit.owner, targetTile.coordinates, targetTile.coordinates, 20, false, AttackAction.AnimationType.None, 0));
                }
            }
    } */
    /*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.PerformAttackDefault))]
    private static void ActionUtils_PerformAttackDefault(GameState gameState, byte playerId, WorldCoordinates origin, WorldCoordinates target, int damage)
    {
        TileData tile = gameState.Map.GetTile(origin);
        TileData tile2 = gameState.Map.GetTile(target);
        UnitState? usAttacker = (tile != null) ? tile.unit : null;
        UnitState usDefender = tile2.unit;
        if (usAttacker != null)
        {
            usAttacker.SetUnitDirection(origin, target);
        }
        usDefender.SetUnitDirection(target, origin);
        UnitState usDefender2 = usDefender;
        usDefender2.health -= (ushort)Math.Min(damage, usDefender.health);
        if (usAttacker != null)
        {
            foreach (UnitEffect effect in usAttacker.effects)
            {
                if (unitEffectDataDict.TryGetValue(effect, out var effectData) && effectData != null && effectData.removal != null)
                {
                    foreach (string str in effectData.removal)
                    {
                        if (str == "attack")
                        {
                            usAttacker.RemoveEffect(effect);
                        }
                    }
                }
            }
        }
        foreach (UnitEffect effect in usDefender.effects)
        {
            if (unitEffectDataDict.TryGetValue(effect, out var effectData) && effectData != null && effectData.removal != null)
            {
                foreach (string str in effectData.removal)
                {
                    if (str == "hurt")
                    {
                        usDefender.RemoveEffect(effect);
                    }
                }
            }
        }
        byte playerId2 = (origin == target || usAttacker == null) ? playerId : usAttacker.owner;
        PlayerState playerState;
        gameState.TryGetPlayer(playerId2, out playerState);
        if (usDefender.health == 0)
        {
            if (usDefender.owner != 255)
            {
                playerState.kills += 1U;
                if (origin != WorldCoordinates.NULL_COORDINATES && usAttacker != null)
                {
                    UnitData unitData;
                    gameState.GameLogicData.TryGetData(usAttacker.type, out unitData);
                    if (!unitData.IsVehicle() && !unitData.hidden)
                    {
                        UnitState unitState3 = usAttacker;
                        unitState3.xp += 1;
                    }
                }
                ActionUtils.EnableTask(gameState, playerState, TaskData.Type.Killer);
                TaskBase taskBase;
                if (gameState.TryGetTask(playerState, TaskData.Type.Killer, out taskBase) && taskBase.Bump(gameState, 1))
                {
                    gameState.CheckTask(playerState, taskBase);
                }
            }
            PlayerState playerState2;
            gameState.TryGetPlayer(usDefender.owner, out playerState2);
            playerState2.casualities += 1U;
            gameState.ActionStack.Add(new KillUnitAction(playerId, target));
            return;
        }
        if (usAttacker != null && usAttacker.HasAbility(UnitAbility.Type.Poison, gameState))
        {
            gameState.ActionStack.Add(new PoisonUnitAction(playerId, origin, target));
        }
    } */

    /* i cant keep developing this forever, gotta get it out to get feedback on shit
    //melee splash
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    public static bool AttackCommand_ExecuteDefault(GameState gameState, AttackCommand __instance)
    {
        bool poisonSplashDealsDamage = false;
        bool freezeSplashDealsDamage = true;
        bool freezeSplashFreezesTile = true;
        bool drenchSplashFloodsTiles = true;
        bool drenchSplashDealsDamage = true;
        double splashDamageMultiplier = 0.5;

        UnitState unitState = gameState.Map.GetTile(__instance.Origin).unit;
        if (!gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState))
        {
            playerState = new PlayerState();
        }

        if (unitState.HasAbility(UnitAbility.Type.Splash, gameState))
        {
            foreach (TileData tileData in gameState.Map.GetTileNeighbors(__instance.Target))
            {
                if (tileData.unit != null && !tileData.unit.HasActivePeaceTreaty(gameState, playerState))
                {
                    if (unitState.HasAbility(UnitAbility.Type.Poison, gameState))
                    {
                        gameState.ActionStack.Add(new PoisonUnitAction(__instance.PlayerId, __instance.Origin, tileData.coordinates));
                        if (poisonSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else if (unitState.HasAbility(UnitAbility.Type.Freeze, gameState))
                    {
                        gameState.ActionStack.Add(new FreezeUnitAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, 0));
                        if (freezeSplashFreezesTile)
                        {
                            gameState.ActionStack.Add(new FreezeTileAction(__instance.PlayerId, tileData.coordinates));
                        }
                        if (freezeSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else if (unitState.HasAbility(UnitAbility.Type.Drench, gameState))
                    {
                        if (drenchSplashFloodsTiles)
                        {
                            gameState.ActionStack.Add(new FloodTileAction(__instance.PlayerId, tileData.coordinates));
                        }
                        if (drenchSplashDealsDamage)
                        {
                            BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                            gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                        }
                    }
                    else
                    {
                        BattleResults battleResults2 = BattleHelpers.GetBattleResults(gameState, unitState, tileData.unit);
                        gameState.ActionStack.Add(new AttackAction(__instance.PlayerId, __instance.Origin, tileData.coordinates, Convert.ToInt32(Math.Round(battleResults2.attackDamage * splashDamageMultiplier)), false, AttackAction.AnimationType.Splash, 20));
                    }
                }
            }
        }
        return false;
    }*/

    













    

    

    
    








//I snuck this in, and he didn't even realize! I'm so sneaky! He'll never figure it out!
    /*



                                                                                ███████        ████████                                                                                       
                                                                           ████                   ██████                                                                                      
                                                                       ████                   █████                                                                                           
                                                                     ███                   ███                                                                                                
                                                                   ███                  ████                                                                                                  
                                                                 ███                  ███                                                                                                     
                                                               ███                 ████                                                                                                       
                                                              ███                 ██                                                                                                          
                                                             ███                 ██                                                                                                           
                                                            ██                  ██                                                                                                            
                                                           ██                   ██               █████                                                                                        
                                                          ██                      ███   ████████████████████                                                                                  
                                                         ███                        █████████████████████████████                                                                             
                                                         ██                     ███████████      █████████████████████                              ██                                        
                                                         ██                █████████████                ████████████████████                       ███                                        
                                                         ██            █████████████                        ████████████████████████  ██████     ███ ██                                       
                                                          ██        ██████████████                              ████████████████████████████   ███   ██                                       
                                                        ████    █████████████████                    ██            ██████████████████████   ████     ██                                       
                                                       ███████████████████████                         ██            ███████████████████████         ██                                       
                                                       ██████████████████████        ███████             ██           █████████████████              ██                                       
                                                       █████████████████████            ██████            ██            ████████████████             ██                                       
                                                       ████████████████████                ████            ██            ███████████████            ██                                        
                                                       ██████████████████                    ███            ████          ██████████████           ██                                         
                                                     ███████████████████              ███████████                          █████████████          ██                                          
                                                  █████████████████████            ██ █  ██████████                        ██████████████       ██                                            
                                                 █████████████████████                  █████    ███                        █████████████      ██                                             
                                               ██████████████████████                          █   ██       █                ███████████     ███                                              
                                               █████████████████████                                     ███        ███████  ███████████   ███                                                
                                              █████████████████████         ██                              ███████████      ██████████████                                                   
                                             █████████████████████          ███                                  ████ ██     ████████████                                                     
                                             ████████████████████          █████                                         █  ████████████                                                      
                                            █████████████████████          ███████                                          █████████                                                         
                                            ████████████████████          ███████████          ███    ██                   █████████                                                          
                                            ███████████████████        ███████████████████               ██                ████████                                                           
                                    ███████████████████████████       █████████████████████                  ██           ████████                                                            
                      █████████████████     ██████████████████        ████████████████████████                            ████████                                                            
              ████████████                 ██████████████████        █████████    ████████████                            ████████                                                            
        █████████                         ███████████████████       █████████    ██       ████   ██                       ████████                                                            
                                        ██  ████████████████        █████████     ██         ███  ██              ███     █████████                                                           
                                      ███   ███   ██████████       ██████████      █           ███                 ████   █████████                                                           
                                     ██             ████████      █████████         █           █████      █        ████  █████████                                                           
                                    ██               ███████      ████████           ███          ██████          █████████████████   ██                                                      
                                  ███                ████████    ███████                ███           ███████████       ████████████████                                                      
                                 ██                  █████████   ██████     █              ██                              ███████████                                                        
                                ██                  ███████████████████ █████          █      ████                          ██████████                                                        
                               ██                ██████████████████████████             ███       █████                   █  ███████████                                                      
                             ███             █████████████████████████████                 ███           █████        █████   ███████████                                                     
                             ██               ███████████████████████████                      █                               ██████████                                                     
                           ███                 ██████████████████████████                                                       █████████                                                     
                          ██                     ████████████████████████                                                       ███   ██                                                      
                          ██                       ████████████████████████                                         ██         ███     ██                                                     
                         ██                          ██████████████████████                                      ███           ██      ██                                                     
                        ██                             █████████████████████                                  ███              ██       ██                                                    
                        ██                               ███████████████████                             █████                ██        ██                                                    
                       ██                                  ██████████████████                                                ██          █                                                    
                       ██                                   █████████████████                                               ██           ██                                                   
                      ██                                  ██ ██████████████████                                            ██             ██                                                  
                     ██                                  ███  ████████████████████            ██                          █               ███                                                 
                     ██                                 ██     █████████████████████         ██                          ██               █████                                               
                     ██                                ██       ███████████████████████    █████     ██                 ██                ██  ███                                             
                     ██                               ██         ████████████████████████████████   ████     ███       ██                 █     ███                                           
                     █                              ██           ████████████████████████████████████████████████    ███                 ██       ██                                          
                    ██                            ███             █████████████████████████████████████████████████████                  █         ███                                        
                    ██                           ███               ███████████████████████████████████████████████████                  ██          ███                                       
                     █                          ██                  ████████████████████████████████████████████████                   ██             ██                                      
                     ██                        ██                   ███████████████████████████████████████████████                  ██                ███                                    
                     ██                      ██                      ████████████████████████████████████████████████             ███                   ███                                   
                      ██                   ███                        █████████████████████████████████████████      ███████  █████                      ███                                  
                       ██                 ██                           ███████████████████████████████████████                                            ███                                 
        */
}


