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
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;


namespace Polibrary;

public static class PolibUtils
{
    private static ManualLogSource utilGuy;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(PolibUtils));
        utilGuy = logger;
        //utilGuy.LogInfo("I ran out of ideas");
    }
    public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(System.Collections.Generic.List<T> sysList)
    {
        var il2cppList = new Il2CppSystem.Collections.Generic.List<T>();
        for (int i = 0; i < sysList.Count; i++)
        {
            il2cppList.Add(sysList[i]);
        }
        return il2cppList;
    }
    public static List<T> ToSystemList<T>(Il2Gen.List<T> il2cppList)
    {
        var sysList = new List<T>(il2cppList.Count);
        for (int i = 0; i < il2cppList.Count; i++)
        {
            sysList.Add(il2cppList[i]);
        }
        return sysList;
    }
    public static T[] ArrayFromListIl2Cpp<T>(Il2Gen.List<T> il2cppList)
    {
        T[] array = new T[il2cppList.Count];
        for (int i = 0; i < il2cppList.Count; i++)
        {
            array[i] = il2cppList[i];
        }
        return array;
    }
    public static T[] ArrayFromListSystem<T>(List<T> sysList)
    {
        T[] array = new T[sysList.Count];
        for (int i = 0; i < sysList.Count; i++)
        {
            array[i] = sysList[i];
        }
        return array;
    }
    public static T[] MakeSystemArray<T>(T value)
    {
        return new T[] { value };
    }
    public static List<T> MakeSystemList<T>(T[] array)
    {
        return new List<T>(array);
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward[] targetRewards)
    {
        GameManager.GameState.TryGetPlayer(playerId, out var playerState);
        Il2Gen.List<TileData> tiles = playerState.GetCityTiles(GameManager.GameState);
        int num = 0;
        foreach (TileData tile in tiles)
        {
            Il2Gen.List<CityReward> rewards = tile.improvement.rewards;
            foreach (CityReward checkedReward in rewards)
            {
                if (targetRewards != null)
                {
                    foreach (CityReward compareToThisReward in targetRewards)
                    {
                        if (checkedReward == compareToThisReward)
                        {
                            num++;
                        }
                    }
                }
            }
        }
        return num;
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward targetReward)
    {
        return GetRewardCountForPlayer(playerId, MakeSystemArray(targetReward));
    }
    public static CityReward[] GetSpawningRewardsForUnit(UnitData.Type unit)
    {
        List<CityReward> list = new List<CityReward>();
        foreach (CityReward reward in Parse.rewardList)
        {
            if (Parse.cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if (cityRewardData.unitType == unit)
                {
                    list.Add(reward);
                }
            }
        }
        return ArrayFromListSystem(list);
    }
    public static Parse.PolibCityRewardData GetRewardData(CityReward reward)
    {
        Parse.cityRewardDict.TryGetValue(reward, out var data);
        return data;
    }

    private static void ApplyEffect(GameState gameState, WorldCoordinates Origin, WorldCoordinates Target, UnitEffect effect)
    {
        TileData tile = gameState.Map.GetTile(Origin);
        TileData tile2 = gameState.Map.GetTile(Target);
        UnitState unit = tile.unit;
        UnitState unit2 = tile2.unit;
        if (unit2 == null)
        {
            return;
        }
        unit2.AddEffect(effect);
        if (unit2.passengerUnit != null)
        {
            unit2.passengerUnit.AddEffect(effect);
        }
    }

    public static void CleanseUnit(GameState gameState, UnitState unit)
    {
        unit.effects = new Il2Gen.List<UnitEffect>();
    }

    public static void HealUnit(GameState gameState, UnitState unit, int amount)
    {
        var maxhp = unit.GetMaxHealth(gameState);
        var currhp = unit.health;
        if (currhp >= maxhp)
        {
            return;
        }
        var diff = maxhp - currhp;
        if (diff < amount)
        {
            amount = diff;
        }
        if (unit.HasEffect(UnitEffect.Poisoned))
        {
            amount = 0;
            unit.RemoveEffect(UnitEffect.Poisoned);
        }
        unit.health += (ushort)amount;
        Tile tile = MapRenderer.Current.GetTileInstance(unit.coordinates);
        tile.Heal(amount);
    }


    public static List<TechData> polibGetUnlockableTech(PlayerState player) //Broken in beta so that's why I made this btw (fapingvin)
    {
        var gld = GameManager.GameState.GameLogicData;
        if (player.tribe == TribeData.Type.None)
        {
            return null;
        }
        TribeData tribe;
        if (GameManager.GameState.GameLogicData.TryGetData(player.tribe, out tribe))
        {
            List<TechData> list = new List<TechData>();
            for (int i = 0; i < player.availableTech.Count; i++)
            {
                TechData @override;
                if (gld.TryGetData(player.availableTech[i], out @override))
                {
                    @override = gld.GetOverride(@override, tribe);
                    foreach (TechData techData in @override.techUnlocks)
                    {
                        TechData override2 = gld.GetOverride(techData, tribe);
                        if (!player.HasTech(override2.type) && !list.Contains(override2))
                        {
                            list.Add(override2);
                        }
                    }
                }
            }
            return list;
        }
        return null;
    }

    public static Parse.PolibCityRewardData SetVanillaCityRewardDefaults(CityReward reward) //dont laugh // Wtf??? I will laugh >:)
    {
        Parse.PolibCityRewardData rewardData = new Parse.PolibCityRewardData();
        switch (reward)
        {
            case CityReward.Workshop:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        productionModifier = 1,
                        level = 1,
                        order = 0
                    };
                    break;
                }
            case CityReward.Explorer:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        scoutSpawnAmount = 1,
                        scoutMoveAmount = 15,
                        level = 1,
                        order = 1
                    };
                    break;
                }
            case CityReward.Resources:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        currencyReward = 5,
                        level = 2,
                        order = 1
                    };
                    break;
                }
            case CityReward.CityWall:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        defenceBoostReward = 40,
                        level = 2,
                        order = 0
                    };
                    break;
                }
            case CityReward.PopulationGrowth:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        populationReward = 3,
                        level = 3,
                        order = 0
                    };
                    break;
                }
            case CityReward.BorderGrowth:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        borderGrowthAmount = 1,
                        level = 3,
                        order = 1
                    };
                    break;
                }
            case CityReward.Park:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        productionModifier = 1,
                        scoreReward = 250,
                        level = 4,
                        persistence = "post",
                        order = 0
                    };
                    break;
                }
            case CityReward.SuperUnit:
                {
                    rewardData = new Parse.PolibCityRewardData
                    {
                        unitType = UnitData.Type.Giant, //i really like that I dont have to account for unitOverride
                        level = 4,
                        persistence = "post",
                        order = 1
                    };
                    break;
                }
        }
        return rewardData;
    }
    public static Parse.PolibUnitEffectData SetVanillaUnitEffectDefaults(UnitEffect effect)
    {
        Parse.PolibUnitEffectData effectData = new Parse.PolibUnitEffectData();
        switch (effect)
        {
            case UnitEffect.Boosted:
                {
                    effectData = new Parse.PolibUnitEffectData
                    {
                        movementAdd = 1,
                        attackAdd = 5,
                        removal = new List<string> { "action", "attack", "hurt" }
                    };
                    break;
                }
            case UnitEffect.Poisoned:
                {
                    effectData = new Parse.PolibUnitEffectData
                    {
                        defenceMult = 7,
                        removal = new List<string> { "heal" }
                    };
                    break;
                }
            case UnitEffect.Bubble:
                {
                    effectData = new Parse.PolibUnitEffectData
                    {
                        movementAdd = 1,
                        removal = new List<string> { "nonflooded", "hurt" }
                    };
                    break;
                }
            case UnitEffect.Frozen:
                {
                    effectData = new Parse.PolibUnitEffectData
                    {
                        freezing = true,
                        removal = new List<string> { "endturn" }
                    };
                    break;
                }
            case UnitEffect.Petrified:
                {
                    effectData = new Parse.PolibUnitEffectData
                    {
                        freezing = true,
                        removal = new List<string> { "endturn" }
                    };
                    break;
                }
        }
        return effectData;

    }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AI), nameof(AI.CheckForTechNeeds))]
    public static bool Logshit(AI __instance, GameState gameState, PlayerState player, Il2Gen.List<TileData> playerEmpire, Il2Gen.Dictionary<TechData.Type, int> neededTech)
    {
        int num = 0;
        int num2 = 0;
        for (int i = 0; i < gameState.Map.Tiles.Length; i++)
        {
            TileData tileData = gameState.Map.Tiles[i];
            bool explored = tileData.GetExplored(player.Id);
            if (explored)
            {
                bool flag = tileData.owner == player.Id;
                if (flag)
                {
                    bool flag2 = tileData.HasImprovement(ImprovementData.Type.City) && !tileData.IsConnected;
                    if (flag2)
                    {
                        num2++;
                    }
                    bool flag3 = tileData.terrain == Polytopia.Data.TerrainData.Type.Field || tileData.terrain == Polytopia.Data.TerrainData.Type.Forest;
                    if (flag3)
                    {
                        num++;
                    }
                }
                bool flag4 = !tileData.CanBeAccessedByPlayer(gameState, player);
                if (flag4)
                {
                    TechData techThatUnlocks = gameState.GameLogicData.GetTechThatUnlocks(tileData.terrain);
                    bool flag5 = techThatUnlocks != null;
                    if (flag5)
                    {
                        AI.AddTechNeed(neededTech, techThatUnlocks!.type, 1);
                    }
                    else
                    {
                        utilGuy?.LogInfo("HOTCHI MAMA, KRIS, [Slow Down] THERE! I JUST SAVED YOUR [$2.99] LIFE FROM A [Null Crash1997]!! ALSO, WHO IS [Lougg Kaard]??");
                    }
                }
                bool flag6 = tileData.resource != null && gameState.GameLogicData.IsResourceVisibleToPlayer(tileData.resource.type, player, gameState);
                if (flag6)
                {
                    Il2Gen.List<ImprovementData> improvementForResource = gameState.GameLogicData.GetImprovementForResource(tileData.resource!.type);
                    for (int j = 0; j < improvementForResource.Count; j++)
                    {
                        ImprovementData improvementData = improvementForResource[j];
                        bool flag7 = improvementData != null && improvementData.HasAbility(ImprovementAbility.Type.Freelance) && !gameState.GameLogicData.IsUnlocked(improvementData.type, player);
                        if (flag7)
                        {
                            TribeData tribeData = gameState.GameLogicData.GetTribeData(player.tribe);
                            TechData techThatUnlocks2 = gameState.GameLogicData.GetTechThatUnlocks(improvementData, tribeData);
                            AI.AddTechNeed(neededTech, techThatUnlocks2.type, 5);
                        }
                    }
                }
            }
        }
        bool flag8 = num > 0;
        if (flag8)
        {
            int num3 = num * (1 + num2);
            AI.AddTechNeed(neededTech, TechData.Type.Roads, num3);
        }
        return false;
    }

    #region Unnecessary stuff
    public static bool IsResourceVisibleToPlayer2ElectricBoogaloo(GameLogicData gld, ResourceData.Type resourceType, PlayerState player)
    {
        return gld.GetUnlockedImprovements(player).ContainsImprovementRequiredForResource(resourceType) || gld.GetUnlockableImprovements(player, GameManager.GameState).ContainsImprovementRequiredForResource(resourceType);
    }

    public static Il2Gen.List<ImprovementData> polibGetUnlockableImprovements(GameLogicData gld, PlayerState player)
    {
        var unlockedTech = gld.GetUnlockedTech(player);
        TribeData tribe;
        if (unlockedTech != null && gld.TryGetData(player.tribe, out tribe))
        {
            Il2Gen.List<ImprovementData> list = new Il2Gen.List<ImprovementData>();
            for (int i = 0; i < unlockedTech.Count; i++)
            {
                for (int j = 0; j < unlockedTech[i].improvementUnlocks.Count; j++)
                {
                    ImprovementData @override = gld.GetOverride(unlockedTech[i].improvementUnlocks[j], tribe);
                    if (!@override.hidden)
                    {
                        list.Add(@override);
                    }
                }
            }
            if (player.tasks != null && player.tasks.Count > 0)
            {
                for (int k = 0; k < player.tasks.Count; k++)
                {
                    TaskData taskData;
                    if (player.tasks[k].IsCompleted && gld.TryGetData(player.tasks[k].GetTaskType(), out taskData) && taskData.improvementUnlocks != null && taskData.improvementUnlocks.Count != 0)
                    {
                        foreach (var unlock in taskData.improvementUnlocks)
                        {
                            list.Add(unlock);
                        }
                    }
                }
            }
            return list;
        }
        return null;
    }

    #endregion Unnecessary stuff

    public static ImprovementData DataFromState(ImprovementState improvement, GameState state)
    {
        return state.GameLogicData.GetImprovementData(improvement.type);
    }


    #region ParseUtils

    public static void ParsePerEach<targetType, T>(JObject rootObject, string categoryName, string fieldName, Dictionary<targetType, T> dict)
        where targetType : struct, System.IConvertible
    {
        foreach (JToken jtoken in rootObject.SelectTokens($"$.{categoryName}.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<targetType>.TryGetType(token.Path.Split('.').Last(), out var type))
                {
                    if (token[fieldName] != null)
                    {
                        T v = token[fieldName]!.ToObject<T>();
                        dict[type] = v;
                        token.Remove(fieldName);
                        utilGuy!.LogInfo($"Parsed variable {v} into dict {dict} with key {type}");
                    }
                }
            }
        }
    }
    
    

    #endregion ParseUtils
}