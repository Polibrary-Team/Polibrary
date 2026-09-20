using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Polytopia.Data;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using PolytopiaBackendBase.Common;
using Polibrary.Parsing;


namespace Polibrary;

public static class CityRewardManager
{
    [HarmonyPrefix] // DO NOT DELETE!!!!!!!!!!!!! Somehow this is required.
    [HarmonyPatch(typeof(RewardPopup), nameof(RewardPopup.SetRewards))] //HA ITS KLIPIS FAULT GUYS POLIB IS STABLE I SWEAR!!!! //no its not
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
        int idx = PolibData.FindData(Parse.polibCityRewardDatas, reward);
        if (idx < 0) return true;
        PolibCityRewardData data = Parse.polibCityRewardDatas[idx];

        if (data.addProduction != null)
        {
            state.ActionStack.Add(new ModifyProductionAction(__instance.PlayerId, (short)data.addProduction, __instance.Coordinates));
        }
        if (data.currencyReward != null)
        {
            state.ActionStack.Add(new IncreaseCurrencyAction(playerId, tile.coordinates, (int)data.currencyReward, 40));
        }
        if (data.populationReward != null)
        {
            for (int i = 0; i < data.populationReward; i++)
            {
                state.ActionStack.Add(new IncreasePopulationAction(playerId, tile.coordinates, tile.coordinates, 40));
            }
        }
        if (data.scoreReward != null)
        {
            state.ActionStack.Add(new IncreaseScoreAction(playerId, (int)data.scoreReward, tile.coordinates, 0));
        }
        if (data.scoutSpawnAmount != null)
        {
            for (int i = 0; i < data.scoutSpawnAmount; i++)
            {
                state.ActionStack.Add(new ScoutMoveAction(playerId, state.GetNextUnitId(), (uint)data.scoutMoveAmount, state.RandomHash.GetHash(tile.coordinates.X, tile.coordinates.Y), tile.coordinates, new Il2Gen.List<WorldCoordinates>()));
            }
        }
        if (data.borderGrowthAmount != null)
        {
            for (int i = 0; i < data.borderGrowthAmount; i++)
            {
                __instance.AddBorderGrowthActions(state, tile);
            }
        }
        if (data.unitType != null)
        {
            ActionUtils.TrainUnitOnOccupiedSpace(state, playerId, (UnitData.Type)data.unitType, tile);
        }
        tile.improvement.AddReward(reward);
        return false;
    }



    [HarmonyPrefix]
    [HarmonyPatch(typeof(ImprovementDataExtensions), nameof(ImprovementDataExtensions.GetCityRewardsForLevel))] //this is the polyscript equivalent of the pear of anguish (idk what the name is yk that iron shit that they shove up your ass and then they extend it and it opens and it mighty fucks up you arsehole)
    public static bool ImprovementDataExtentions_GetCityRewardsForLevel(ref Il2CppStructArray<CityReward> __result, ImprovementData data, int level)
    {
        Il2Gen.List<CityReward> list = new Il2Gen.List<CityReward>();
        GameState state = GameManager.GameState;

        PlayerState playerState;
        TribeType tribeType;
        if (state.TryGetPlayer(state.CurrentPlayer, out playerState))
        {
            tribeType = playerState.tribe;
        }
        else return true;

        foreach (CityReward reward in Parsing.Parse.rewardList)
        {
            if (PolibData.TryFindData(Parse.polibCityRewardDatas, reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (PolibData.TryGetValue<PolibTribeData, TribeType, Dictionary<CityReward, CityReward>>(Parse.polibTribeDatas, tribeType, "cityRewardOverrides", out var dict))
                    {
                        if (dict.TryGetValue(reward, out var newReward))
                        {
                            list.Add(newReward);
                            continue;
                        }
                    }
                    list.Add(reward);
                }
            }
        }

        List<CityReward> orderedlist = PolibUtils.ToSysList(list);
        System.Comparison<CityReward> comparison = (a, b) => 
        {
            int orderA = 0;
            int orderB = 0;

            if (PolibData.TryFindData(Parse.polibCityRewardDatas, a, out var dataA))
            {
                orderA = dataA.order;
            }
            if (PolibData.TryFindData(Parse.polibCityRewardDatas, b, out var dataB))
            {
                orderB = dataB.order;
            }
            return orderA.CompareTo(orderB);
        };

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = orderedlist.ToIl2List().ToSysArray();

        if (array != null || array.Length != 0)
        {
            __result = array;
            return false;
        }
        else { return true; }

    }

    public static bool isCustomReward(string s) //fapingvin came in clutch with this one
    {
        //Is it even a city reward?
        //idk fap you tell me
        //Main.modLogger.LogDebug("Reward? " + s);
        string[] words = s.Split("_");
        if (words.Length < 2 || words[1] != "rewards")
        {
            return false;
        }

        if(words.Length < 3) return false;
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

    [HarmonyPostfix] //I HATE THIS I HATE I HATE I HATE FUCK THIS SHIT FUCKING HELL I HATE THIS SO MUCH WHY DOESNT IT WORK WHY WHY WHYX WHY WHY WHY HWY HWY HWYH WHH A FUCK
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static void AI_ChooseCityReward(GameState gameState, TileData tile, CityReward[] rewards, ref CityReward __result)
    {
        GameLogicData gld = gameState.GameLogicData;
        CityReward[] rewardarray = AIIsFuckingWithMe_GetCityRewardsForLevel(gld.GetImprovementData(tile.improvement.type), tile.improvement.level - 1);



        System.Random random = new System.Random();
        int num = random.Next(0, rewardarray.Length);



        __result = rewardarray[num];
    }

    public static CityReward[] AIIsFuckingWithMe_GetCityRewardsForLevel(ImprovementData data, int level) //c# waterboarding. i'm not gonna elaborate. leave.
    {
        Il2Gen.List<CityReward> list = new Il2Gen.List<CityReward>();
        GameState state = GameManager.GameState;

        PlayerState playerState;
        state.TryGetPlayer(state.CurrentPlayer, out playerState);
        if (!state.TryGetPlayer(state.CurrentPlayer, out playerState)) Main.modLogger.LogInfo($"KRIS SHIT IS SERIOUSLY FUCKED");
        TribeType tribeType = playerState.tribe;

        foreach (CityReward reward in Parsing.Parse.rewardList)
        {
            if (PolibData.TryFindData(Parse.polibCityRewardDatas, reward, out var cityRewardData))
            {
                if ((cityRewardData.level == level || (cityRewardData.persistence == "post" && cityRewardData.level <= level) || (cityRewardData.persistence == "pre" && cityRewardData.level >= level)) && !cityRewardData.hidden)
                {
                    if (PolibData.TryGetValue<PolibTribeData, TribeType, Dictionary<CityReward, CityReward>>(Parse.polibTribeDatas, tribeType, "cityRewardOverrides", out var dict))
                    {
                        if (dict.TryGetValue(reward, out var newReward))
                        {
                            list.Add(newReward);
                            continue;
                        }
                    }
                    list.Add(reward);
                }
            }
        }

        List<CityReward> orderedlist = PolibUtils.ToSysList(list);
        System.Comparison<CityReward> comparison = (a, b) => 
        {
            int orderA = 0;
            int orderB = 0;

            if (PolibData.TryFindData(Parse.polibCityRewardDatas, a, out var dataA))
            {
                orderA = dataA.order;
            }
            if (PolibData.TryFindData(Parse.polibCityRewardDatas, b, out var dataB))
            {
                orderB = dataB.order;
            }
            return orderA.CompareTo(orderB);
        };

        orderedlist.Sort(comparison);

        Il2CppStructArray<CityReward> array = orderedlist.ToIl2List().ToSysArray();

        if (array != null || array.Length != 0)
        {
            return array;

        }
        else { Main.modLogger.LogInfo($"KRIS WTF HAPPENED?? AI [GetCityRewardsForLevel] COULDN'T FUCKING FIND A DAMN [CityReward[]]!!"); return new CityReward[2]; }

    }
}