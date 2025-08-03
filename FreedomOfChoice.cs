using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMicrosoft.Win32;
using Il2CppSystem;
using Il2CppSystem.Linq.Expressions.Interpreter;
using Polytopia.Data;
using PolytopiaBackendBase.Game;
using Unity.Collections;
using Unity.Jobs;
using Unity.Properties;
using Unity.Services.Analytics.Internal;
using UnityEngine;
using UnityEngine.UIElements.UIR;

namespace focmod;

/* With comments */

public static class Main
{
    public const int bundleSize = 3;
    private static ManualLogSource? modLogger;
    public static void Load(ManualLogSource logger)
    {
        //PolyMod.Loader.AddPatchDataType("unitEffect", typeof(UnitEffect));
        PolyMod.Registry.autoidx++;
        PolyMod.Loader.AddPatchDataType("customstuff", typeof(CityReward));
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;
        logger.LogMessage("City Rewards dll loaded.");
    }

    /*[HarmonyPostfix]
    [HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetImage))]
    public static void CustomImage(ref UnityEngine.UI.Image __result, string id)
    {
        Sprite? sprite;
        int? idx = null;
        List<string> idparts = id.Split("_").ToList();
        int minimumidx = 1000;
        foreach (string item in idparts)
        {
            if (int.TryParse(item, out int parsedIdx))
            {
                if (parsedIdx >= minimumidx)
                {
                    idx = parsedIdx;
                }
            }
        }

        if (idx != null)
        {
            foreach (var targetType in PolyMod.Loader.typeMappings.Values)
            {
                
            }
        }
    }*/




    //AI will always choose SuperUnit in Domination, Might and Sandbox
    //Otherwise, whatever the original code for choosing was, this keeps it 2/3 of the time
    //1/3 of the time it chooses my custom reward
    //Nevermind, this is broken for some reason, can't even result a cityreward.superunit
    /*[HarmonyPostfix]
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static void AIChoosesReward(ref CityReward __result, GameState gameState, TileData tile, CityReward[] rewards)
    {

        CityReward thirdoption = EnumCache<CityReward>.GetType("customfour");
        switch (tile.improvement.level)
        {
            case 1:
                thirdoption = EnumCache<CityReward>.GetType("customone");
                break;
            case 2:
                thirdoption = EnumCache<CityReward>.GetType("customtwo");
                break;
            case 3:
                thirdoption = EnumCache<CityReward>.GetType("customthree");
                break;
            case 4:
                thirdoption = EnumCache<CityReward>.GetType("customfour");
                break;
        }

        int randomnum;
        System.Random RNG = new System.Random();
        randomnum = RNG.Next(0, 3);

        int num = System.Array.IndexOf<CityReward>(rewards, CityReward.SuperUnit);

        var settings = gameState.Settings;

        if ((num != -1) && ((settings.BaseGameMode == GameMode.Might) || (settings.BaseGameMode == GameMode.Domination) || (settings.BaseGameMode == GameMode.Sandbox)))
        {
            __result = CityReward.SuperUnit;
        }
        else
        {
            if (randomnum == 0)
            {
                gameState.GameLogicData.TryGetData(ImprovementData.Type.City, out ImprovementData improvementData);
                //__result = ImprovementDataExtensions.GetCityRewardsForLevel(improvementData, tile.improvement.level)[2];
                __result = thirdoption;
            }
        }
    }*/


    /*[HarmonyPrefix]
    [HarmonyPatch(typeof(CommandBase), nameof(CommandBase.IsValid), typeof(GameState))]
    public static bool ByPassValidation(ref bool __result, CommandBase __instance, GameState state)
    {
        if (state.Settings.GameType == GameType.Competitive || state.Settings.GameType == GameType.Multiplayer)
        {
            return true;
        }
        else if (__instance.GetCommandType() == CommandType.CityReward)
        {
            __result = true;
            return false;
        }
        return true;
    }*/

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static bool AI_ChooseCityReward(GameState gameState, TileData tile, CityReward[] rewards, ref CityReward __result)
    {
        GameLogicData gld = gameState.GameLogicData;
        CityReward[] rewardarray = GetRewardsForLevel(gld.GetImprovementData(tile.improvement.type), tile.improvement.level -1);



        System.Random random = new System.Random();
        int num = random.Next(0, rewardarray.Length);


        __result = rewardarray[num];

        return false;
    }

    public static CityReward[] GetRewardsForLevel(ImprovementData data, int level)
    {
        return data.GetCityRewardsForLevel(level);
    }

    /*[HarmonyPrefix]
    [HarmonyPatch(typeof(AI), nameof(AI.ChooseCityReward))]
    public static bool AI_ChooseCityReward(GameState gameState, TileData tile, CityReward[] rewards, ref CityReward __result)
    {
        modLogger.LogMessage("Trying to select reward for AI");
        GameLogicData gld = gameState.GameLogicData;

        CityReward thirdoption = EnumCache<CityReward>.GetType("customfour");
        switch (tile.improvement.level)
        {
            case 1:
                thirdoption = EnumCache<CityReward>.GetType("customone");
                break;
            case 2:
                thirdoption = EnumCache<CityReward>.GetType("customtwo");
                break;
            case 3:
                thirdoption = EnumCache<CityReward>.GetType("customthree");
                break;
            case 4:
                thirdoption = EnumCache<CityReward>.GetType("customfour");
                break;
        }


        System.Random random = new System.Random();
        int num = random.Next(0, 3);
        modLogger.LogMessage("Rolled:" + num);

        if (num != 0)
        {
            modLogger.LogWarning("Chosen thirdoption");
            __result = thirdoption;
            return false;
        }
        else return true;
    }*/

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityRenderer), nameof(CityRenderer.RefreshCity))]
    public static void insertObelisk(CityRenderer __instance)
    {
        if (__instance.dataChanged)
        {
            return;
        }
        var a = GameManager.GameState.Map.GetTile(__instance.Coordinates);
        a.improvement.HasReward(EnumCache<CityReward>.GetType("customtwo"));
        bool cityhasobelisk = a.improvement.HasReward(EnumCache<CityReward>.GetType("customtwo"));
        int num = 100;
        int num2 = 9;

        if (cityhasobelisk)
        {
            TribeData.Type tribe = __instance.Tribe;
            SkinType skinType = __instance.SkinType;
            CityPlot nextRandomPlot = __instance.GetNextRandomPlot(ref num, num2);
            //int.TryParse(EnumCache<CityReward>.GetType("customtwo").ToString().Split("_")[2], out int enumnum);
            PolytopiaSpriteRenderer house = __instance.GetHouse(tribe, 1555, skinType);
            int n = nextRandomPlot.houses.Count;
            bool hasObelisk = false;
            for (int i = 0; i < n; i++)
            {
                if (nextRandomPlot.houses[i].sprite == PolyMod.Registry.GetSprite("healobelisk"))
                {
                    hasObelisk = true;
                    break;
                }
            }
            if (!hasObelisk)
            {
                nextRandomPlot.AddHouse(house);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityRenderer), nameof(CityRenderer.GetHouse))]
    public static void GetObelisk(ref PolytopiaSpriteRenderer __result, CityRenderer __instance, TribeData.Type tribe = TribeData.Type.Xinxi, int type = 1, SkinType skinType = SkinType.Default)
    {
        //int.TryParse(EnumCache<CityReward>.GetType("customtwo").ToString().Split("_")[2], out int enumnum);
        if (type == 1555)
        {
            __result.sprite = PolyMod.Registry.GetSprite("healobelisk");
        }
    }



    [HarmonyPostfix]
    [HarmonyPatch(typeof(ImprovementDataExtensions), nameof(ImprovementDataExtensions.GetCityRewardsForLevel))]
    public static void GetCityRewardsForLevelOverwrite(ref Il2CppStructArray<CityReward> __result, ImprovementData data, int level)
    {
        int num = System.Math.Min(level - 1, CityRewardData.cityRewards.Length / 2 - 1) * 2;
        CityReward thirdoption = EnumCache<CityReward>.GetType("customfour");
        switch (level)
        {
            case 1:
                thirdoption = EnumCache<CityReward>.GetType("customone");
                break;
            case 2:
                thirdoption = EnumCache<CityReward>.GetType("customtwo");
                break;
            case 3:
                thirdoption = EnumCache<CityReward>.GetType("customthree");
                break;
            case 4:
                thirdoption = EnumCache<CityReward>.GetType("customfour");
                break;
        }


        //We can't actually define a new cityRewards and do that[num+2] (even though that would make sense)
        __result = new CityReward[]
        {
        CityRewardData.cityRewards[num],
        CityRewardData.cityRewards[num + 1],
        thirdoption
        };

    }

    public static bool isCustomReward(string s)
    {
        //Is it even a city reward?
        string[] words = s.Split("_");
        if (words[1] != "rewards")
        {
            return false;
        }


        if (int.TryParse(words[2], out int whatever))
        {
            return true;
        }

        return false;
    }

    public static CityReward getEnum(string s)
    {
        int a = int.Parse(s.Split("_")[2]);
        return (CityReward)a;
    }


    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetSprite))]
    public static void Override(UIIconData __instance, ref Sprite __result, string id)
    {
        if (Main.isCustomReward(id))
        {
            __result = PolyMod.Registry.GetSprite(EnumCache<CityReward>.GetName(Main.getEnum(id)));
        }
    }

    public static void Populate(GameState state, TileData tile, int FruitsToSpawn)
    {
        var citytiles = ActionUtils.GetCityAreaSorted(state, tile);
        citytiles.Reverse();
        int counter = FruitsToSpawn;
        for (int i = 0; i < citytiles.Count; i++)
        {
            if (citytiles[i].terrain == Polytopia.Data.TerrainData.Type.Field && citytiles[i].resource == null && citytiles[i].improvement == null)
            {
                if (counter > 0)
                {
                    Tile tilerender = MapRenderer.Current.GetTileInstance(tile.coordinates);
                    tilerender.SpawnSparkles();
                    state.ActionStack.Add(new BuildAction(tile.owner, EnumCache<ImprovementData.Type>.GetType("createfruit"), citytiles[i].coordinates, false));
                    counter--;
                }
            }
        }
        state.ActionStack.Add(new IncreaseCurrencyAction(tile.owner, tile.coordinates, 1 * counter, 0));
    }

    public static List<TechData> FOCGetUnlockableTech(PlayerState player)
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityRewardAction), nameof(CityRewardAction.Execute))]
    public static void CustomRewards(CityRewardAction __instance, GameState state)
    {
        TileData tile = state.Map.GetTile(__instance.Coordinates);
        if (tile == null || tile.improvement == null) //Safety first
        {
            return;
        }
        PlayerState playerState;
        if (!state.TryGetPlayer(tile.owner, out playerState))
        {
            return;
        }
        if (__instance.Reward == EnumCache<CityReward>.GetType("customone"))
        {
            state.ActionStack.Add(new TrainAction(playerState.Id, EnumCache<UnitData.Type>.GetType("rewardwarrior"), __instance.Coordinates, 0));
            return;
        }
        if (__instance.Reward == EnumCache<CityReward>.GetType("customtwo"))
        {
            return;
        }
        if (__instance.Reward == EnumCache<CityReward>.GetType("customthree"))
        {
            Main.Populate(state, tile, 5);
            return;
        }
        if (__instance.Reward == EnumCache<CityReward>.GetType("customfour"))
        {
            var unlockableTech = Main.FOCGetUnlockableTech(playerState);
            if (unlockableTech == null || unlockableTech.Count == 0)
            {
                state.ActionStack.Add(new IncreaseCurrencyAction(playerState.Id, tile.coordinates, 10, 0));
                return;
            }
            var tech = unlockableTech[state.RandomHash.Range(0, unlockableTech.Count, tile.coordinates.X, tile.coordinates.Y)];
            TechData.Type techtype = tech.type;
            state.ActionStack.Add(new ResearchAction(playerState.Id, techtype, 0));
            return;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EndTurnCommand), nameof(EndTurnCommand.ExecuteDefault))]
    private static void EndTurnCommand_ExecuteDefault(EndTurnCommand __instance, GameState state)
    {

        for (int i = 0; i < state.Map.Tiles.Length; i++)
        {
            TileData tileData = state.Map.Tiles[i];
            if (tileData.unit != null && tileData.unit.owner == __instance.PlayerId && tileData.improvement != null)
            {
                if (!tileData.IsBeingCaptured(state) && tileData.improvement.type == ImprovementData.Type.City)
                {
                    if (tileData.improvement.HasReward(EnumCache<CityReward>.GetType("customtwo")) && tileData.unit.health < tileData.unit.GetMaxHealth(state))
                    {
                        Tile tile = MapRenderer.Current.GetTileInstance(tileData.coordinates);
                        UnitState unit = tileData.unit;
                        int hpincrease = 20;
                        if (unit.health + 20 >= unit.GetMaxHealth(state))
                        {
                            hpincrease = (unit.GetMaxHealth(state) - unit.health);
                        }
                        if (unit.HasEffect(UnitEffect.Poisoned))
                        {
                            unit.RemoveEffect(UnitEffect.Poisoned);
                        }
                        tileData.unit.health += (ushort)hpincrease;
                        tile.Heal(hpincrease);
                    }
                }
            }
        }
    }

}


