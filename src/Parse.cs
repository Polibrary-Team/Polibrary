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
using MS.Internal.Xml.XPath;
using pbb = PolytopiaBackendBase.Common;
using UnityEngine.InputSystem;


namespace Polibrary;

public static class Parse
{
    private static ManualLogSource LogMan1997;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Parse));
        LogMan1997 = logger;
    }

    public enum UnitEffectIntention
    {
        Positive,
        Neutral,
        Negative
    }
    public static Dictionary<ImprovementData.Type, string> BuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> NoBuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> ImpBuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> ImpCustomLocKey = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<pbb.TribeType, string> leaderNameDict = new Dictionary<pbb.TribeType, string>();
    public static Dictionary<ImprovementData.Type, int> defenceBoostDict = new Dictionary<ImprovementData.Type, int>();
    public static Dictionary<ImprovementData.Type, float> AIScoreDict = new Dictionary<ImprovementData.Type, float>();
    public static Dictionary<pbb.TribeType, List<(ResourceData.Type, int)>> startingResources = new Dictionary<pbb.TribeType, List<(ResourceData.Type, int)>>();
    public class PolibCityRewardData //oh boy its time to bake some lights, except its not lights and we're not baking anything and flowey undertale
    {
        public int addProduction { get; set; }
        public int currencyReward { get; set; }
        public int populationReward { get; set; }
        public int scoreReward { get; set; }
        public int defenceBoost { get; set; }
        public int scoutSpawnAmount { get; set; }
        public int scoutMoveAmount { get; set; } = 15;
        public int borderGrowthAmount { get; set; } //yay now its useful
        public UnitData.Type unitType { get; set; }
        public int level { get; set; } = -1;
        public string persistence { get; set; } = "none";
        public int order { get; set; } = 0;
        public bool hidden { get; set; } = false;
        public int boostAttackOverSpawn { get; set; }
        public int boostDefenceOverSpawn { get; set; }
        public int boostMaxHpOverSpawn { get; set; }
        public int boostMovementOverSpawn { get; set; }
        public bool healUnitOverSpawn { get; set; } = false;
    }
    public class CityRewardOverrideClass
    {
        public CityReward og { get; set; }
        public CityReward neu { get; set; }
    }
    public class PolibUnitEffectData //So I haveth a Laser Pointre...
    {
        public Dictionary<string, int> additives { get; set; }
        public Dictionary<string, int> multiplicatives { get; set; }
        public static UnitEffectIntention intention { get; set; } 
        public int defenceMult { get; set; }
        public int attackMult { get; set; }
        public int attackAdd { get; set; }
        public int movementMult { get; set; }
        public int movementAdd { get; set; }
        public string color { get; set; }
        public List<string> removal { get; set; }
        public bool freezing { get; set; }
    }
    public static Dictionary<CityReward, PolibCityRewardData> cityRewardDict = new Dictionary<CityReward, PolibCityRewardData>();
    public static Dictionary<pbb.TribeType, List<CityRewardOverrideClass>> cityRewardOverrideDict = new Dictionary<pbb.TribeType, List<CityRewardOverrideClass>>();
    public static List<CityReward> rewardList = PolibUtils.MakeSystemList<CityReward>(CityRewardData.cityRewards);
    public static Dictionary<UnitEffect, PolibUnitEffectData> unitEffectDataDict = new Dictionary<UnitEffect, PolibUnitEffectData>();
    public class PolibUnitAbilityData
    {
        public int visionRadius { get; set; }
        public bool allowsFly { get; set; }
        public UnitEffect effect { get; set; }
        public string effectApplication { get; set; }
        public string effectApplicationActionTarget { get; set; }
    }
    public static Dictionary<UnitAbility.Type, PolibUnitAbilityData> unitAbilityDataDict = new Dictionary<UnitAbility.Type, PolibUnitAbilityData>();
    public static UnitEffect[] vanillaUnitEffects = new UnitEffect[] { UnitEffect.Boosted, UnitEffect.Bubble, UnitEffect.Frozen, UnitEffect.Invisible, UnitEffect.Petrified, UnitEffect.Poisoned };
    
    
    public static Dictionary<string, pAction> actions = new Dictionary<string, pAction>();
    public static Dictionary<ImprovementData.Type, Dictionary<string/*trigger*/, string/*action*/>> improvementTriggers = new Dictionary<ImprovementData.Type, Dictionary<string, string>>();
    
    
    public static Dictionary<ImprovementData.Type, List<UnitAbility.Type>> unitAbilityWhitelist = new Dictionary<ImprovementData.Type, List<UnitAbility.Type>>();
    public static Dictionary<ImprovementData.Type, List<UnitAbility.Type>> unitAbilityBlacklist = new Dictionary<ImprovementData.Type, List<UnitAbility.Type>>();
    public static Dictionary<ImprovementData.Type, List<UnitData.Type>> unitWhitelist = new Dictionary<ImprovementData.Type, List<UnitData.Type>>();
    public static Dictionary<ImprovementData.Type, List<UnitData.Type>> unitBlacklist = new Dictionary<ImprovementData.Type, List<UnitData.Type>>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))] //dude why tf do you have 176387126 different patches for ts??
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();

            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltBySpecific"] != null ? "BuiltBySpecific" : "builtBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        BuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in BuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse5(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltOnSpecific"] != null ? "BuiltOnSpecific" : "builtOnSpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        ImpBuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in ImpBuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse4(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["NotBuiltBySpecific"] != null ? "NotBuiltBySpecific" : "notBuiltBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        NoBuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in NoBuildersDict");
                    }
                }
            }
        }
    }

    public static Dictionary<ImprovementData.Type, string> UnblockDict = new Dictionary<ImprovementData.Type, string>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse3(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["Unblock"] != null ? "Unblock" : "unblock";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        UnblockDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in UnblockDict");
                    }
                }
            }
        }
    }

    //thanks exploit
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse6(GameLogicData __instance, JObject rootObject) //in this world, its analfuck, or be analfucked
    {
        PolibUtils.ParsePerEach<pbb.TribeType, string>(rootObject, "tribeData", "leaderName", leaderNameDict);
        PolibUtils.ParsePerEach<ImprovementData.Type, int>(rootObject, "improvementData", "defenceBoost", defenceBoostDict);
        PolibUtils.ParsePerEach<ImprovementData.Type, float>(rootObject, "improvementData", "aiScore", AIScoreDict);
        
        foreach (JToken jtoken in rootObject.SelectTokens("$.tribeData.*").ToList()) // "// tribeData!" -exploit, 2025
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<pbb.TribeType>.TryGetType(token.Path.Split('.').Last(), out var tribeType))
                {
                    List<CityRewardOverrideClass> overlist = new List<CityRewardOverrideClass>();
                    foreach (JToken overtoken in token.SelectTokens("$.cityRewardOverrides.*").ToList())
                    {
                        if (EnumCache<CityReward>.TryGetType(overtoken.Path.Split('.').Last(), out var reward))
                        {
                            if (EnumCache<CityReward>.TryGetType(overtoken!.ToObject<string>(), out var overreward))
                            {
                                CityRewardOverrideClass overrideClass = new CityRewardOverrideClass
                                {
                                    og = reward,
                                    neu = overreward
                                };
                                overlist.Add(overrideClass);
                            }
                        }
                    }
                    cityRewardOverrideDict[tribeType] = overlist;
                }
            }
        }
        
        #region pActions

        foreach (JToken jtoken in rootObject.SelectTokens("$.pActions.*").ToList())
        {
            JArray token = jtoken.TryCast<JArray>();
            if (token != null)
            {
                string name = token.Path.Split('.').Last();
                pAction action = new pAction();
                action.lines = token.Values<string>().ToArray();
                actions[name] = action;
            }
        }

        #endregion pActions

        #region Improvements

        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    if (token["unitAbilityWhitelist"] != null)
                    {
                        List<UnitAbility.Type> types = new List<UnitAbility.Type>();

                        foreach (string s in token["unitAbilityWhitelist"].Values<string>().ToList())
                        {
                            EnumCache<UnitAbility.Type>.TryGetType(s, out var type);
                            types.Add(type);
                        }

                        unitAbilityWhitelist[impType] = types;
                    }
                    
                    if (token["unitAbilityBlacklist"] != null)
                    {
                        List<UnitAbility.Type> types = new List<UnitAbility.Type>();

                        foreach (string s in token["unitAbilityBlacklist"].Values<string>().ToList())
                        {
                            EnumCache<UnitAbility.Type>.TryGetType(s, out var type);
                            types.Add(type);
                        }
                        unitAbilityBlacklist[impType] = types;
                        
                    }

                    if (token["unitWhitelist"] != null)
                    {
                        List<UnitData.Type> types = new List<UnitData.Type>();

                        foreach (string s in token["unitWhitelist"].Values<string>().ToList())
                        {
                            EnumCache<UnitData.Type>.TryGetType(s, out var type);
                            types.Add(type);
                        }
                        unitWhitelist[impType] = types;
                    }
                    
                    if (token["unitBlacklist"] != null)
                    {
                        List<UnitData.Type> types = new List<UnitData.Type>();

                        foreach (string s in token["unitBlacklist"].Values<string>().ToList())
                        {
                            EnumCache<UnitData.Type>.TryGetType(s, out var type);
                            types.Add(type);
                        }
                        unitBlacklist[impType] = types;
                    }

                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], impType, improvementTriggers);
                    }
                }
            }
        }

        #endregion Improvements

        #region City Rewards

        foreach (CityReward reward in CityRewardData.cityRewards) //default for vanilla cityRewards
        {
            cityRewardDict[reward] = PolibUtils.SetVanillaCityRewardDefaults(reward);
        }

        
        foreach (JToken jtoken in rootObject.SelectTokens("$.cityRewardData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<CityReward>.TryGetType(token.Path.Split('.').Last(), out var cityReward))
                {
                    PolibCityRewardData cityRewardData = new PolibCityRewardData();

                    cityRewardData.addProduction = PolibUtils.ParseToken<int>(token, "addProduction");
                    cityRewardData.currencyReward = PolibUtils.ParseToken<int>(token, "currencyReward");
                    cityRewardData.populationReward = PolibUtils.ParseToken<int>(token, "populationReward");
                    cityRewardData.scoreReward = PolibUtils.ParseToken<int>(token, "scoreReward");
                    cityRewardData.defenceBoost = PolibUtils.ParseToken<int>(token, "defenceBoost");
                    cityRewardData.scoutSpawnAmount = PolibUtils.ParseToken<int>(token, "scoutSpawnAmount");
                    cityRewardData.scoutMoveAmount = PolibUtils.ParseToken<int>(token, "scoutMoveAmount");
                    cityRewardData.borderGrowthAmount = PolibUtils.ParseToken<int>(token, "borderGrowthAmount");
                    if (token["spawnUnit"] != null)
                    {
                        if (EnumCache<UnitData.Type>.TryGetType(token["spawnUnit"]!.ToObject<string>(), out var type))
                        {
                            cityRewardData.unitType = type;
                        }
                        token.Remove("spawnUnit");
                    }
                    cityRewardData.level = PolibUtils.ParseToken<int>(token, "level");
                    cityRewardData.persistence = PolibUtils.ParseToken<string>(token, "persistence");
                    cityRewardData.order = PolibUtils.ParseToken<int>(token, "order");
                    cityRewardData.hidden = PolibUtils.ParseToken<bool>(token, "hidden");
                    cityRewardData.boostAttackOverSpawn = PolibUtils.ParseToken<int>(token, "boostAttackOverSpawn");
                    cityRewardData.boostDefenceOverSpawn = PolibUtils.ParseToken<int>(token, "boostDefenceOverSpawn");
                    cityRewardData.boostMaxHpOverSpawn = PolibUtils.ParseToken<int>(token, "boostMaxHpOverSpawn");
                    cityRewardData.boostMovementOverSpawn = PolibUtils.ParseToken<int>(token, "boostMovementOverSpawn");
                    cityRewardData.healUnitOverSpawn = PolibUtils.ParseToken<bool>(token, "healUnitOverSpawn");
                    if (!rewardList.Contains(cityReward))
                    {
                        rewardList.Add(cityReward);
                    }
                    cityRewardDict[cityReward] = cityRewardData;
                }
            }
        }

        #endregion City Rewards

        foreach (UnitEffect effect in vanillaUnitEffects)
        {
            unitEffectDataDict[effect] = PolibUtils.SetVanillaUnitEffectDefaults(effect);
        }

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitEffectData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitEffect>.TryGetType(token.Path.Split('.').Last(), out var unitEffect))
                {
                    PolibUnitEffectData unitEffectData = new PolibUnitEffectData();

                    //will redo

                    unitEffectDataDict[unitEffect] = unitEffectData;
                }
            }
        }
    }
}