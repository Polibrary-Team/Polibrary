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

public static class TribeManager
{
    private static ManualLogSource wingLogster;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(TribeManager));
        wingLogster = logger;
        wingLogster.LogInfo("I'm wing gaster");
        //wingLogster.LogInfo("UHH I MEAN");
        //wingLogster.LogInfo("✋︎ ✂︎♌︎♏︎♐︎❒︎♓︎♏︎■︎♎︎♏︎♎︎✂︎ ⍓︎□︎◆︎❒︎ ❍︎□︎⧫︎♒︎♏︎❒︎ ●︎♋︎⬧︎⧫︎ ■︎♓︎♑︎♒︎⧫︎");
    }

    /*
    //my lil'  startingResourceGeneratorThingy rewrite
    //whatever you do, DO NOT TOUCH IT!!! IT ***WILL*** BREAK!!!
    //man I really thought this was one of my bigger features. And then I was introduced to cityRewards
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.addStartingResourcesToCapital))]
    public static bool MapGenerator_addStartingResourcesToCapital(MapGenerator __instance, MapData map, GameState gameState, PlayerState player, ResourceData startingResources, int minResourcesCount = 2)
    {
        TribeData tribeData;

        if (!PolytopiaDataManager.GetGameLogicData(VersionManager.GetGameLogicDataVersionFromGameVersion(gameState.Version)).TryGetData(player.tribe, out tribeData))
        {
            wingLogster!.LogInfo($"HEY     EVERY      !! WE DIDNT [Fifty Percent Off]ING GET THE [TribeData] OF TRIBE {player.tribe}!!");
        }

        if (tribeData != null && Parse.startingResources.TryGetValue(tribeData.type, out var list)) //if things are okay (we check so we dont get errors)
        {
            foreach (TileData tile in map.GetArea(player.startTile, 1, true, false)) //change climate of tiles (important for polaris and packed games)
            {
                tile.climate = gameState.GameLogicData.GetTribeData(player.tribe).climate;
            }
            foreach ((ResourceData.Type ListResType, int ListAmount) in list) //change resource amounts (basically, for some reason, I chose to do defaulting in here, not somewhere else)
            {

                int amount = ListAmount;
                ResourceData.Type restype = ListResType;

                if (ListAmount == -1 && restype == ResourceData.Type.Fruit)
                {
                    amount = 2;
                    if (tribeData.startingResource.Count != 0)
                    {
                        restype = tribeData.startingResource[0].type;
                    }
                    else
                    {
                        restype = ResourceData.Type.Fruit;
                        wingLogster!.LogInfo($"KRIS YOU LEFT YOUR [ResourceData.Type]s ON AISLE 3 [Lyeing Around]?? TF?");
                    }
                }

                Il2Gen.List<TileData> area = map.GetArea(player.startTile, 1, true, false);
                __instance.Shuffle(area);
                GameLogicData gld = gameState.GameLogicData;
                ResourceData resdata = gld.GetResourceData(restype); //poo
                Il2Gen.List<Polytopia.Data.TerrainData> terraindatalist = resdata.resourceTerrainRequirements; //I LOVE IL2CPP TO BITS
                int rgamount = 0;
                int terrainNotMatchCount = 0;
                Il2Gen.List<TileData> terrainNotMatchList = new Il2Gen.List<TileData>(); //I LOOOOVE WRITING IT OUT EVERY SINGLE FUCKING TIME (this is outdated as i have updated my code to use aliases, so I dont have to write a fucking novel each time I want-.. I mean HAVE to use Il2Cpp)
                Il2Gen.List<TileData> selectedTileList = new Il2Gen.List<TileData>(); //why


                foreach (TileData tile in area)
                {
                    if (tile != null)
                    {
                        if (rgamount >= amount) { }
                        else
                        if (tile.resource != null && tile.resource.type == restype)
                        {
                            rgamount++;
                        }
                        else
                        if (terraindatalist.Contains(tile.terrain))
                        {
                            selectedTileList.Add(tile);
                            rgamount++;
                        }
                        else
                        {
                            terrainNotMatchCount++;
                            terrainNotMatchList.Add(tile);
                        }
                    }
                    else { wingLogster!.LogInfo($"KRIS WHAT THE &#!@ [Tile] IS [Null]??? WHY?? [Y]?? [Yellow]??"); }

                }

                if (terrainNotMatchCount + rgamount == 8 && rgamount < amount)
                {
                    wingLogster!.LogInfo($"KRIS WE DONT @&!%ING HAVE ENOUGH [TerrainData.Type], FIXING IT NOW");
                    int necessaryAmount = amount - rgamount;
                    for (int i = 0; i < necessaryAmount; i++)
                    {
                        System.Random rng = new System.Random();
                        int result = rng.Next(0, terraindatalist.Count);

                        terrainNotMatchList[i].terrain = terraindatalist[result].type;
                        selectedTileList.Add(terrainNotMatchList[i]);
                    }
                }

                __instance.Shuffle(selectedTileList);
                foreach (TileData tile in selectedTileList)
                {
                    if (tile != null)
                    {
                        tile.resource = new ResourceState
                        {
                            type = restype
                        };
                    }

                }
            }
        }
        return false;
    }
    */

    // Simpler than it seems, and it seems very simple (Fapingvin, 2025)
    [HarmonyPrefix] //na azt jól megmondtad
    [HarmonyPatch(typeof(GameStateUtils), nameof(GameStateUtils.SetPlayerNames))]
    public static void OverridePlayerNames(GameState gameState)
    {
        foreach (PlayerState playerState in gameState.PlayerStates)
        {
            TribeData tribeData;
            gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData);
            if (string.IsNullOrEmpty(playerState.GetNameInternal()) && Parse.leaderNameDict.TryGetValue(tribeData.type, out string name))
            {
                playerState.UserName = name;
            }
        }
    }
}