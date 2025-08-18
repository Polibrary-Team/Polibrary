using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AssetRipper.Primitives;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Linq.Expressions.Interpreter;
using Polytopia.Data;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Toporzol;

/* With comments */


public static class Main
{

    // didSac = did Sacrifice this turn
    
    public static bool didSac = false;

    public static bool didAbility = false;

    // swarmcalling cooldown: Last turn that the call was made!!!
    public static int callcooldown = -10;
    public static int latestcallidx = 0;

    public static Dictionary<uint, int> callers = new Dictionary<uint, int>();
    private static ManualLogSource? modLogger;
    public static void Load(ManualLogSource logger)
    {
        PolyMod.Loader.AddPatchDataType("unitEffect", typeof(UnitEffect));
        PolyMod.Registry.autoidx++;
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;
        logger.LogMessage("Toporzol.dll loaded.");
    }

    // reset variables at turn change
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EndTurnCommand), nameof(EndTurnCommand.Execute))]
    private static void updateSac()
    {
        didSac = false;

    }

    //Queen auto-heal
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    private static void HealAsGo(MoveCommand __instance, GameState gameState)
    {
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState);
        TileData asdftile = gameState.Map.GetTile(__instance.To);
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);
        if (unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("healasgo"), gameState) && (unit.health < unit.GetMaxHealth(gameState)))
        {
            unit.health = (ushort)(unit.health + 20);
            Tile tile = MapRenderer.Current.GetTileInstance(asdftile.coordinates);
            tile.Heal(20);
        }
        if (unit.health > unit.GetMaxHealth(gameState))
        {
            unit.health = (ushort)unit.GetMaxHealth(gameState);
        }
    }

    /* Toporzolian units moving onto an altar*/
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.Execute))]
    private static void AltarBless(MoveCommand __instance, GameState gameState)
    {
        gameState.TryGetPlayer(__instance.PlayerId, out PlayerState playerState);
        gameState.GameLogicData.TryGetData(playerState.tribe, out TribeData tribeData);
        gameState.TryGetUnit(__instance.UnitId, out UnitState unit);
        TileData asdftile = gameState.Map.GetTile(__instance.To);

        if (tribeData.tribeAbilities.Contains(EnumCache<TribeAbility.Type>.GetType("altarability")) && asdftile.HasImprovement(EnumCache<ImprovementData.Type>.GetType("toporzolaltar")))
        {
            unit.AddEffect(EnumCache<UnitEffect>.GetType("toporzolblessed"));
            unit.health = (ushort)(unit.health * 1.25);
            ImprovementState improvement = asdftile.improvement;

            if ((unit.HasAbility(EnumCache<UnitAbility.Type>.GetType("altarsurvive"), gameState) == false) && (didSac == false))
            {

                int pointgain = ScoreSheet.GetUnitScore(unit, gameState) * 2;

                //int healthloss = unit.health;
                //unit.health = (ushort)((healthloss / 4));

                didSac = true;
                //gameState.ActionStack.Add(new ModifyScoreAction(improvement.owner, unit.coordinates, (short)pointgain));
                gameState.ActionStack.Add(new IncreaseScoreAction(improvement.owner, pointgain, unit.coordinates, 0));
                if (unit.health >= 0)
                {
                    gameState.ActionStack.Add(new IncreasePopulationAction(improvement.owner, asdftile.coordinates, asdftile.rulingCityCoordinates, 0));
                }

                gameState.ActionStack.Add(new KillUnitAction(unit.owner, unit.coordinates));
                NotificationManager.Notify("A unit was sacrificed!", "Sacrifice");
            }

        }
    }

    //Toporzol starts with an extra spearman on turn 1 (not 0 cause that breaks the game)
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.ExecuteDefault))]
    private static void Swarming(StartTurnAction __instance, GameState gameState)
    {
        if (gameState.CurrentTurn == 0)
        {
            //initialize at the start of the game
            didAbility = false;
            latestcallidx = 0;
            callcooldown = -10;
        }

        if (gameState.CurrentTurn == 1 && !didAbility)
        {
            Il2CppSystem.Collections.Generic.List<PlayerState> allplayers = gameState.PlayerStates;

            var playerState = allplayers[0];

            for (int i = 0; i < allplayers.Count; i++)
            {
                if (allplayers[i].HasTribeAbility(EnumCache<TribeAbility.Type>.GetType("swarmability"), gameState))
                {


                    playerState = allplayers[i];
                    TileData tileData2 = gameState.Map.GetTile(playerState.startTile);


                    if (playerState.AutoPlay == false)
                    {
                        NotificationManager.Notify("A spearman from a nearby primitive clan appeared in your capital!", "Great Unification");
                    }

                    gameState.ActionStack.Add(new TrainAction(playerState.Id, EnumCache<UnitData.Type>.GetType("spearman"), tileData2.coordinates, 0));
                }
            }
            didAbility = true;
        }
    }

    /* Cursing */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    private static void ExecuteDefault(AttackCommand __instance, GameState gameState)
    {
        UnitState unitState;
        gameState.TryGetUnit(__instance.UnitId, out unitState);

        TileData targetTile = gameState.Map.GetTile(__instance.Target);
        gameState.TryGetUnit(targetTile.unit.id, out UnitState targetUnit);

        if (unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("toporzolcurse"), gameState))
        {
            targetUnit.AddEffect(EnumCache<UnitEffect>.GetType("toporzolcursed"));
        }

        //Retaliation Curse - spoiler: does not work :(
        // if (targetUnit.HasAbility(EnumCache<UnitAbility.Type>.GetType("toporzolcurse"), gameState))
        // {
        //    unitState.AddEffect(EnumCache<UnitEffect>.GetType("toporzolcursed"));
        // }
    }

    /* DarkWizard */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    private static void DarkWizardVoid(AttackCommand __instance, GameState gameState)
    {
        UnitState unitState;
        gameState.TryGetUnit(__instance.UnitId, out unitState);

        TileData targetTile = gameState.Map.GetTile(__instance.Target);
        gameState.TryGetUnit(targetTile.unit.id, out UnitState targetUnit);

        if (unitState.HasAbility(EnumCache<UnitAbility.Type>.GetType("darkwizard"), gameState))
        {
            /*System.Random random = new System.Random();
            int wizardaction = random.Next(0, 3);

            // Curse enemy
            if(wizardaction == 0){
            targetUnit.AddEffect(EnumCache<UnitEffect>.GetType("toporzolcursed"));
            }

            //Bless itself
            if(wizardaction == 1){
                var mh = UnitDataExtensions.GetMaxHealth(unitState, gameState);
                unitState.health = (ushort)(mh + 2);
                if(!unitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolblessed"))){
                unitState.AddEffect(EnumCache<UnitEffect>.GetType("toporzolblessed"));
                }
            }

            //+1 star
            if(wizardaction == 2){
                */
            gameState.ActionStack.Add(new IncreaseCurrencyAction(unitState.owner, unitState.coordinates, 1, 0));
            //}
        }
    }

    // Swarmcaller improvement

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CanCall(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (improvement.type != EnumCache<ImprovementData.Type>.GetType("swarmcall")) return;
        if (!tile.GetUnit(gameState, playerState.Id, false).HasAbility(EnumCache<UnitAbility.Type>.GetType("swarmcalling")))
        {
            __result = false;
        }
        if (callcooldown + 3 >= (int)gameState.CurrentTurn)
        {
            __result = false;
        }
        //if(tile.GetUnit(gameState, playerState.Id, false).HasEffect(EnumCache<UnitEffect>.GetType("toporzolcalled"))){
        //  __result = false;
        //}
    }



    //Can transform into a shaman?
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.CanBuild))]
    public static void CanTransform(GameState gameState, TileData tile, PlayerState playerState, ImprovementData improvement, ref bool __result)
    {
        if (__result == false) return;
        if (improvement.type != EnumCache<ImprovementData.Type>.GetType("shamantransformation")) return;
        if (!tile.GetUnit(gameState, playerState.Id, false).HasAbility(EnumCache<UnitAbility.Type>.GetType("transforminto")) || !(tile.HasImprovement(EnumCache<ImprovementData.Type>.GetType("toporzolaltar"))))
        {
            __result = false;
        }
    }



    //Swarmcalling action

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void SwarmBuilt(BuildAction __instance, GameState gameState)
    {

        if (__instance.Type == EnumCache<ImprovementData.Type>.GetType("swarmcall"))
        {
            TileData tile = gameState.Map.GetTile(__instance.Coordinates);

            UnitState unit = tile.unit;

            var player = unit.owner;

            //Define list of players
            Il2CppSystem.Collections.Generic.List<PlayerState> allplayers = gameState.PlayerStates;

            var playerState = allplayers[0];

            //Find the player that did the swarmcalling
            for (int i = 0; i < allplayers.Count; i++)
            {
                if (allplayers[i].Id == player)
                {
                    playerState = allplayers[i];
                }
            }

            //Define list of cities (TileData)
            Il2CppSystem.Collections.Generic.List<TileData> cities = PlayerExtensions.GetCityTiles(playerState, gameState);

            int calledunits = 0;
            for (int j = 0; j < cities.Count; j++)
            {
                TileData tileData2 = cities[j];
                //For testing
                //gameState.ActionStack.Add(new IncreaseCurrencyAction(player, tileData2.coordinates, 10, 0));

                //Disable the commented part if you don't want sieged cities to spawn spearmen
                if (tileData2.IsBeingCaptured(gameState) == false)
                {
                    gameState.ActionStack.Add(new TrainAction(playerState.Id, EnumCache<UnitData.Type>.GetType("spearman"), tileData2.coordinates, 0));
                    calledunits++;
                }
            }

            //kill unit and set cooldown to turn*2.5ish (bug I need to fix)
            //gameState.ActionStack.Add(new KillUnitAction(unit.owner, unit.coordinates));
            if (unit.HasEffect(EnumCache<UnitEffect>.GetType("toporzolcalled")))
            {
                int until = latestcallidx;
                if (until == 0)
                {
                    until = 100;
                }
                if (callers.TryGetValue(unit.id, out int amount))
                {
                    callers[unit.id] += calledunits;
                }
            }
            else
            {

                callers.Add(unit.id, calledunits);
                unit.AddEffect(EnumCache<UnitEffect>.GetType("toporzolcalled"));
            }


            callcooldown = (int)gameState.CurrentTurn;
        }
    }

    // Transformation
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildAction), nameof(BuildAction.ExecuteDefault))]
    private static void TransformInto(BuildAction __instance, GameState gameState)
    {

        if (__instance.Type == EnumCache<ImprovementData.Type>.GetType("shamantransformation"))
        {
            TileData tile = gameState.Map.GetTile(__instance.Coordinates);

            UnitState unit = tile.unit;

            var player = unit.owner;

            Il2CppSystem.Collections.Generic.List<PlayerState> allplayers = gameState.PlayerStates;

            var playerState = allplayers[0];

            for (int i = 0; i < allplayers.Count; i++)
            {
                if (allplayers[i].Id == player)
                {
                    playerState = allplayers[i];
                }
            }


            gameState.ActionStack.Add(new TrainAction(playerState.Id, EnumCache<UnitData.Type>.GetType("toporzolqueen"), tile.coordinates, 0));



            gameState.ActionStack.Add(new KillUnitAction(unit.owner, unit.coordinates));
        }
    }


    /*Cursedunit Attacking (remove effect)*/

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.ExecuteDefault))]
    private static void Cleanse(AttackCommand __instance, GameState gameState)
    {
        UnitState unitState;
        gameState.TryGetUnit(__instance.UnitId, out unitState);
        if (unitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolcursed")))
        {
            unitState.RemoveEffect(EnumCache<UnitEffect>.GetType("toporzolcursed"));
        }
    }


    // Cursed unit different color
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Unit), nameof(Unit.UpdateObject), typeof(SkinVisualsTransientData))]
    private static void Unit_UpdateObject_Postfix(Unit __instance, SkinVisualsTransientData transientSkinData)
    {
        if (__instance.UnitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolcursed")))
        {
            foreach (SkinVisualsReference.VisualPart visualPart in __instance.skinVisuals.visualParts)
            {
                if (visualPart != null)
                {
                    if (visualPart.renderer != null)
                    {
                        if (visualPart.renderer.spriteRenderer != null)
                        {
                            var materialBlock = new UnityEngine.MaterialPropertyBlock();
                            visualPart.renderer.spriteRenderer.GetPropertyBlock(materialBlock);
                            materialBlock.SetColor("_OverlayColor", new Color(0.5f, 0.3f, 0.6f, 1f));
                            materialBlock.SetFloat("_OverlayStrength", 0.5f);
                            visualPart.renderer.spriteRenderer.SetPropertyBlock(materialBlock);
                        }
                    }
                }
            }
        }
    }

    //Cursed unit reduced attack
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetAttack), typeof(UnitState), typeof(GameState))]
    public static void UnitDataExtensions_GetAttack(ref int __result, UnitState unitState, GameState gameState)
    {
        if (unitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolcursed")))
        {
            __result = 10;
        }
    }

    //Blessed unit more max health
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMaxHealth), typeof(UnitState), typeof(GameState))]
    public static void UnitDataExtensions_GetMaxHealth(ref int __result, UnitState unitState, GameState gameState)
    {
        if (unitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolblessed")))
        {
            __result = (int)(__result * 1.25);
        }
    }

    //Called unit more max health
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitDataExtensions), nameof(UnitDataExtensions.GetMaxHealth), typeof(UnitState), typeof(GameState))]
    public static void SwarmCalledHPplus(ref int __result, UnitState unitState, GameState gameState)
    {
        if (unitState.HasEffect(EnumCache<UnitEffect>.GetType("toporzolcalled")))
        {
            //search for index
            int until = latestcallidx;

            if (until == 0)
            {
                until = 100;
            }

            if (callers.TryGetValue(unitState.id, out int amount))
            {
                __result = (int)(__result + amount * 10);
            }
        }
    }

    static string stateMarker = "Toporzol";


    // Code generously stolen from Johnklipi

    #nullable disable
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.Serialize))]
    public static void GameState_Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        writer.Write(stateMarker);
        writer.Write((ushort)callers.Count);

        foreach (var kvp in callers)
        {
            modLogger.LogMessage("Logged "+kvp.Key + " value: "+kvp.Value);
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }
    }



    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.Deserialize))]
    public static void GameState_Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        var stream = reader.BaseStream;
        long oldPos = stream.Position;

        try
        {
            if (stream.Length - stream.Position < 8)
                return;
            string marker = reader.ReadString();
            if (marker != stateMarker)
            {
                stream.Position = oldPos;
                return;
            }
            if (stream.Length - stream.Position < 2)
                return;

            ushort count = reader.ReadUInt16();
            callers = new Dictionary<uint, int>();

            for (int i = 0; i < count; i++)
            {
                if (stream.Position >= stream.Length)
                    throw new EndOfStreamException();

                uint key = reader.ReadUInt32();

                if (stream.Position >= stream.Length)
                    throw new EndOfStreamException();

                int value = reader.ReadInt32();
                modLogger.LogMessage("Added " + key + " and " + value);
                    callers.Add(key, value);
            }
        }
        catch (System.IO.EndOfStreamException)
        {
            stream.Position = oldPos;
            callers?.Clear();
        }
        catch
        {
            stream.Position = oldPos;
        }
    }

}
