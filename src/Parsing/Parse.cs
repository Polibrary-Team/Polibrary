using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;

using pbb = PolytopiaBackendBase.Common;
using Steamworks.Data;
using MS.Internal.Xml.XPath;


namespace Polibrary.Parsing;

public static class Parse
{
    private static ManualLogSource LogMan1997;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Parse));
        LogMan1997 = logger;
    }
    public static List<PolibImprovementData> polibImprovementDatas = new();
    public static Dictionary<pbb.TribeType, string> leaderNameDict = new Dictionary<pbb.TribeType, string>();
    public class PolibCityRewardData //oh boy its time to bake some lights, except its not lights and we're not baking anything and flowey undertale
    {
        public int addProduction { get; set; }
        public int currencyReward { get; set; }
        public int populationReward { get; set; }
        public int scoreReward { get; set; }
        public int defenceBoost { get; set; } = -1;
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
        public Dictionary<string, int> additives = new Dictionary<string, int>();
        public Dictionary<string, double> multiplicatives = new Dictionary<string, double>();
        public UnityEngine.Color color { get; set; }
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
    public static Dictionary<UnitData.Type, Dictionary<string/*trigger*/, string/*action*/>> unitTriggers = new Dictionary<UnitData.Type, Dictionary<string, string>>();
    public static Dictionary<UnitAbility.Type, Dictionary<string/*trigger*/, string/*action*/>> unitAbilityTriggers = new Dictionary<UnitAbility.Type, Dictionary<string, string>>();
    public static Dictionary<TribeAbility.Type, Dictionary<string/*trigger*/, string/*action*/>> tribeAbilityTriggers = new Dictionary<TribeAbility.Type, Dictionary<string, string>>();
    public static Dictionary<UnitEffect, Dictionary<string/*trigger*/, string/*action*/>> unitEffectTriggers = new Dictionary<UnitEffect, Dictionary<string, string>>();
    public static Dictionary<CityReward, Dictionary<string/*trigger*/, string/*action*/>> rewardTriggers = new Dictionary<CityReward, Dictionary<string, string>>();
    public static Dictionary<UnitData.Type, List<string>> unitDataTargets = new Dictionary<UnitData.Type, List<string>>();
    public static Dictionary<UnitAbility.Type, List<string>> unitAbilityTargets = new Dictionary<UnitAbility.Type, List<string>>();
    public static Dictionary<UnitEffect, List<string>> unitEffectTargets = new Dictionary<UnitEffect, List<string>>();




    #region Parse
    #endregion
    //thanks exploit
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject) //in this world, its analfuck, or be analfucked
    {
        #region Improvements

        System.Func<PolibImprovementData> impDataFactory = () => new PolibImprovementData();

        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, int, PolibImprovementData>(rootObject, "improvementData", "defenceBoost", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, int, PolibImprovementData>(rootObject, "improvementData", "defenceBoost_Neutral", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, float, PolibImprovementData>(rootObject, "improvementData", "aiScore", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, string, PolibImprovementData>(rootObject, "improvementData", "builtOnSpecific", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, string, PolibImprovementData>(rootObject, "improvementData", "unblock", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerEach<ImprovementData.Type, string, PolibImprovementData>(rootObject, "improvementData", "infoOverride", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(rootObject, "improvementData", "unitAbilityWhitelist", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(rootObject, "improvementData", "unitAbilityBlacklist", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(rootObject, "improvementData", "unitWhitelist", polibImprovementDatas, impDataFactory);
        PolibUtils.ParseIntoClassPerArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(rootObject, "improvementData", "unitBlacklist", polibImprovementDatas, impDataFactory);

        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {

                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], impType, improvementTriggers);
                    }
                }
            }
        }
        #endregion

        PolibUtils.ParsePerEach(rootObject, "tribeData", "leaderName", leaderNameDict);
        PolibUtils.ParseListPerEach(rootObject, "unitData", "targets", unitDataTargets);
        PolibUtils.ParseListPerEach(rootObject, "unitAbility", "targets", unitAbilityTargets);
        PolibUtils.ParseListPerEach(rootObject, "unitEffectData", "targets", unitEffectTargets);
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





        #region Units

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitData.Type>.TryGetType(token.Path.Split('.').Last(), out var unitType))
                {
                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], unitType, unitTriggers);
                    }
                }
            }
        }

        #endregion Units

        #region Unit Ablities

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitAbility.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitAbility.Type>.TryGetType(token.Path.Split('.').Last(), out var abilityType))
                {
                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], abilityType, unitAbilityTriggers);
                    }
                }
            }
        }

        #endregion Unit Abilities

        #region Tribe Ablities

        foreach (JToken jtoken in rootObject.SelectTokens("$.tribeAbility.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<TribeAbility.Type>.TryGetType(token.Path.Split('.').Last(), out var abilityType))
                {
                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], abilityType, tribeAbilityTriggers);
                    }
                }
            }
        }

        #endregion Tribe Abilities

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

                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], cityReward, rewardTriggers);
                    }
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

                    if (token["add"] != null)
                    {
                        unitEffectData.additives = PolibUtils.ParseStringDict<int>(token["add"]);
                        token.Remove("add");
                    }
                    if (token["mult"] != null)
                    {
                        unitEffectData.multiplicatives = PolibUtils.ParseStringDict<double>(token["mult"]);
                        token.Remove("mult");
                    }

                    if (token["color"] != null)
                    {
                        string val = token["color"]!.ToObject<string>();
                        string[] vals = val.Split(',');

                        float r = 0;
                        float g = 0;
                        float b = 0;
                        float a = 1;

                        float.TryParse(vals[0], out r);
                        float.TryParse(vals[1], out g);
                        float.TryParse(vals[2], out b);
                        float.TryParse(vals[3], out a);

                        unitEffectData.color = new UnityEngine.Color(r, g, b, a);
                        token.Remove("color");
                    }

                    if (token["triggers"] != null)
                    {
                        PolibUtils.ParseToNestedStringDict(token["triggers"], unitEffect, unitEffectTriggers);
                    }

                    unitEffectDataDict[unitEffect] = unitEffectData;
                }
            }
        }
    }
}