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
using pbb = PolytopiaBackendBase.Common;
using PolytopiaBackendBase.Game.BindingModels;


namespace Polibrary;

public static class CityRewardManager
{
    private static ManualLogSource nubert;
    public static void Load(ManualLogSource logger)
    {
        nubert = logger;
        Harmony.CreateAndPatchAll(typeof(CityRewardManager));
    }


    [HarmonyPrefix] // DO NOT DELETE!!!!!!!!!!!!! Somehow this is required.
    [HarmonyPatch(typeof(RewardPopup), nameof(RewardPopup.SetRewards))] //HA ITS KLIPIS FAULT GUYS POLIB IS STABLE I SWEAR!!!!
    public static bool PopupFix(RewardPopup __instance, PlayerState playerState, Il2CppStructArray<CityReward> rewards, bool isReplay = false)
    {
        return true;
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(CityRewardAction), nameof(CityRewardAction.Execute))]
    public static bool CityRewardAction_Execute(GameState state, CityRewardAction __instance)
    {
        CityReward reward = __instance.Reward;
        byte playerId = __instance.PlayerId;
        TileData tile = state.Map.GetTile(__instance.Coordinates);

        if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
        {
            if (cityRewardData.addProduction != 0)
            {
                state.ActionStack.Add(new ModifyProductionAction(__instance.PlayerId, System.Convert.ToInt16(cityRewardData.addProduction), __instance.Coordinates));
            }
            if (cityRewardData.currencyReward != 0)
            {
                state.ActionStack.Add(new IncreaseCurrencyAction(playerId, tile.coordinates, cityRewardData.currencyReward, 40));
            }
            if (cityRewardData.populationReward != 0)
            {
                for (int i = 0; i < cityRewardData.populationReward; i++)
                {
                    state.ActionStack.Add(new IncreasePopulationAction(playerId, tile.coordinates, tile.coordinates, 40));
                }
            }
            if (cityRewardData.scoreReward != 0)
            {
                state.ActionStack.Add(new IncreaseScoreAction(playerId, cityRewardData.scoreReward, tile.coordinates, 0));
            }
            for (int i = 0; i < cityRewardData.scoutSpawnAmount; i++)
            {
                state.ActionStack.Add(new ScoutMoveAction(playerId, state.GetNextUnitId(), System.Convert.ToUInt32(cityRewardData.scoutMoveAmount), state.RandomHash.GetHash(tile.coordinates.X, tile.coordinates.Y), tile.coordinates, new Il2Gen.List<WorldCoordinates>()));
            }
            for (int i = 0; i < cityRewardData.borderGrowthAmount; i++)
            {
                __instance.AddBorderGrowthActions(state, tile);
            }
            if (cityRewardData.unitType != UnitData.Type.None)
            {
                if (cityRewardData.boostAttackOverSpawn != 0 || cityRewardData.boostDefenceOverSpawn != 0 || cityRewardData.boostMaxHpOverSpawn != 0 || cityRewardData.boostMovementOverSpawn != 0 || cityRewardData.healUnitOverSpawn)
                {
                    int num = 0;
                    MapData map = state.Map;
                    foreach (TileData tile1 in map.tiles)
                    {
                        if (tile1.unit != null && tile1.unit.type == cityRewardData.unitType)
                        {
                            num++;
                        }
                    }
                    if (num == 0)
                    {
                        ActionUtils.TrainUnitOnOccupiedSpace(state, playerId, cityRewardData.unitType, tile);
                    }
                    if (cityRewardData.healUnitOverSpawn)
                    {
                        foreach (TileData tile1 in map.tiles)
                        {
                            if (tile1.unit != null && tile1.unit.type == cityRewardData.unitType)
                            {
                                tile1.unit.health = (ushort)tile1.unit.GetMaxHealth(state);
                            }
                        }
                    }
                }
                else
                {
                    ActionUtils.TrainUnitOnOccupiedSpace(state, playerId, cityRewardData.unitType, tile);
                }
            }
            tile.improvement.AddReward(reward);
            return false;
        }
        else { return true; }
    }



    [HarmonyPrefix]
    [HarmonyPatch(typeof(ImprovementDataExtensions), nameof(ImprovementDataExtensions.GetCityRewardsForLevel))] //this is the polyscript equivalent of the pear of anguish (idk what the name is yk that iron shit that they shove up your ass and then they extend it and it opens and it mighty fucks up you arsehole)
    public static bool ImprovementDataExtentions_GetCityRewardsForLevel(ref Il2CppStructArray<CityReward> __result, ImprovementData data, int level)
    {
        if (GameManager.Client.GameState.Settings.GameType == GameType.Competitive || GameManager.Client.GameState.Settings.GameType == GameType.Multiplayer)
        {
            return true;
        }

        Il2Gen.List<CityReward> list = new Il2Gen.List<CityReward>();
        GameState state = GameManager.GameState;

        PlayerState playerState;
        pbb.TribeType tribeType = pbb.TribeType.Aimo;
        if (state.TryGetPlayer(state.CurrentPlayer, out playerState))
        {
            tribeType = playerState.tribe;
        }
        else { nubert!.LogInfo($"STUFF IS SERIOUSLY GNOMED"); } //fappy what?

        foreach (CityReward reward in Parse.rewardList)
        {
            if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (Parse.cityRewardOverrideDict.TryGetValue(tribeType, out var cityRewardOverrideClasses))
                    {
                        int num2 = 0;
                        foreach (Parse.CityRewardOverrideClass overrideClass in cityRewardOverrideClasses)
                        {
                            if (overrideClass != null)
                            {
                                if (overrideClass.og == reward)
                                {
                                    list.Add(overrideClass.neu);
                                }
                                else
                                {
                                    num2++;
                                }
                            }
                        }
                        if (num2 >= cityRewardOverrideClasses.Count)
                        {
                            list.Add(reward);
                        }
                    }
                    else
                    {
                        list.Add(reward);
                    }
                }
            }
        }

        List<CityReward> orderedlist = PolibUtils.ToSystemList(list);
        System.Comparison<CityReward> comparison = (a, b) => Parse.cityRewardDict[a].order.CompareTo(Parse.cityRewardDict[b].order);

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = PolibUtils.ArrayFromListIl2Cpp(PolibUtils.ToIl2CppList(orderedlist));

        if (array != null || array.Length != 0)
        {
            __result = array;
            //nubert!.LogInfo(array.Length);
            foreach (CityReward reward in array)
            {
                //nubert!.LogInfo(reward);
            }
            return false;
        }
        else { return true; }

    }

    public static bool isCustomReward(string s) //fapingvin came in clutch with this one
    {
        //Is it even a city reward?
        //idk fap you tell me
        Main.modLogger.LogDebug("Reward? " + s);
        string[] words = s.Split("_");
        if (words[1] != "rewards")
        {
            return false;
        }


        if (int.TryParse(words[2], out int whatever)) //parse? i hate parse! all my homies hate parse! fuck parse! yeah!
        {
            return true;
        }

        return false;
    }

    public static CityReward getEnum(string s)
    {
        Main.modLogger.LogDebug("GetEnum s: " + s);
        int a = int.Parse(s.Split("_")[2]);
        return (CityReward)a;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetSprite))] //idfk what this does but yeah
    public static void UIIconData_GetSprite(UIIconData __instance, ref Une.Sprite __result, string id)
    {
        if (isCustomReward(id))
        {
            __result = PolyMod.Registry.GetSprite(EnumCache<CityReward>.GetName(getEnum(id)))!;
        }
    }

    [HarmonyPrefix] //I HATE THIS I HATE I HATE I HATE FUCK THIS SHIT FUCKING HELL I HATE THIS SO MUCH WHY DOESNT IT WORK WHY WHY WHYX WHY WHY WHY HWY HWY HWYH WHH A FUCK
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static bool AI_ChooseCityReward(GameState gameState, TileData tile, CityReward[] rewards, ref CityReward __result)
    {
        GameLogicData gld = gameState.GameLogicData;
        CityReward[] rewardarray = AIIsFuckingWithMe_GetCityRewardsForLevel(gld.GetImprovementData(tile.improvement.type), tile.improvement.level - 1);



        System.Random random = new System.Random();
        int num = random.Next(0, rewardarray.Length);



        __result = rewardarray[num];

        return false;
    }

    public static CityReward[] AIIsFuckingWithMe_GetCityRewardsForLevel(ImprovementData data, int level) //c# waterboarding. i'm not gonna elaborate. leave.
    {
        Il2CppSystem.Collections.Generic.List<CityReward> list = new Il2CppSystem.Collections.Generic.List<CityReward>();

        GameState state = GameManager.GameState;

        PlayerState playerState;
        state.TryGetPlayer(state.CurrentPlayer, out playerState);
        pbb.TribeType tribeType = pbb.TribeType.Aimo;
        if (state.TryGetPlayer(state.CurrentPlayer, out playerState))
        {
            tribeType = playerState.tribe;
        }
        else { nubert!.LogInfo($"KRIS SHIT IS SERIOUSLY FUCKED"); }

        foreach (CityReward reward in Parse.rewardList)
        {
            if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (Parse.cityRewardOverrideDict.TryGetValue(tribeType, out var cityRewardOverrideClasses))
                    {
                        int num2 = 0;
                        foreach (Parse.CityRewardOverrideClass overrideClass in cityRewardOverrideClasses)
                        {
                            if (overrideClass != null)
                            {
                                if (overrideClass.og == reward)
                                {
                                    list.Add(overrideClass.neu);
                                }
                                else
                                {
                                    num2++;
                                }
                            }
                        }
                        if (num2 >= cityRewardOverrideClasses.Count)
                        {
                            list.Add(reward);
                        }
                    }
                    else
                    {
                        list.Add(reward);
                    }

                }
            }
        }
        List<CityReward> orderedlist = PolibUtils.ToSystemList(list);
        System.Comparison<CityReward> comparison = (a, b) => Parse.cityRewardDict[a].order.CompareTo(Parse.cityRewardDict[b].order);

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = PolibUtils.ArrayFromListIl2Cpp(PolibUtils.ToIl2CppList(orderedlist));

        if (array != null || array.Length != 0)
        {
            return array;

        }
        else { nubert!.LogInfo($"KRIS WTF HAPPENED?? AI [GetCityRewardsForLevel] COULDN'T FUCKING FIND A DAMN [CityReward[]]!!"); return new CityReward[2]; }

    }
}