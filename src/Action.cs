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
using Il2CppSystem.Linq.Expressions;
using UnityEngine.Rendering.Universal;
using LibCpp2IL.Elf;
using PolytopiaBackendBase.Common;


namespace Polibrary;


public class pAction
{
    private static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
    }

    public string[] lines;
    public WorldCoordinates ActionOrigin;
    public byte playerId;
    public string name;


    int index = 0;
    int increment = 1;
    public Dictionary<string, object> variables = new Dictionary<string, object>(); 

    Dictionary<string, int> cycleIds = new Dictionary<string, int>();

    public pAction() { }
    public pAction(pAction other)
    {
        lines = other.lines;
    }

    public void Execute()
    {
        variables["@origin_auto"] = ActionOrigin;
        while (index < lines.Length)
        {
            ReadLine(lines[index]);
        }
    }

    private void ReadLine(string line)
    {
        string[] commandAndParams;

        commandAndParams = line.Split(":", 2);
        

        string command = commandAndParams[0];
        string[] rawparameters = commandAndParams[1].Split(",");

        bool isString = false;

        List<string> untrimmedParams = new List<string>();



        foreach (string s in rawparameters)
        {
            string trimmed = s.TrimStart();

            if (s.Length == 0) continue;

            if (isString)
            {
                untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + s;

                if (s[s.Length - 1] == '§')
                {
                    isString = false;
                }
                else
                {
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + ",";
                }
            }
            else
            {
                untrimmedParams.Add(s);

                if (trimmed[0] == '§' && s[s.Length - 1] != '§') //here
                {
                    isString = true;
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + ",";
                }
            }
        }

        List<string> parameters = new List<string>();
        foreach (string s in untrimmedParams)
        {
            if (s.Contains('§'))
            {
                parameters.Add(s.Trim().Trim('§'));
            }
            else
            {
                parameters.Add(s.Replace(" ", ""));
            }
            
        }

        increment = 1; //reset increment to 1 so we only gotta write diff to those methods where it matters

        

        if (command.Replace(" ", "") != "c") //comment
        {
            RunCommand(command.Replace(" ", ""), parameters); //make command space insensitive too
        }

        index += increment;
    }

    private void RunCommand(string command, List<string> ps)
    {
        bool run = true;

        string[] commandSplit = {command};
        if (command.Contains('?'))
        {
            commandSplit = command.Split('?');
            string s = CheckParams(commandSplit[1]);
            run = ParseBool(s);
        }
        else if (command.Contains('!'))
        {
            commandSplit = command.Split('!');
            string s = CheckParams(commandSplit[1]);
            run = !ParseBool(s);
        }

        if (!run)
        return;

        for (int i = 0; i < ps.Count; i++)
        {
            ps[i] = CheckParams(ps[i]);
        }
        
        switch (commandSplit[0])
        {
            //GENERIC
            case "set": //sets a variable (refer with @)
                SetVariable(ps[0], ps[1], ps[2]);
                break;
            case "setg": //sets a variable (refer with @)
                SetGlobalVariable(ps[0], ps[1], ps[2]);
                break;
            case "setd": //sets a disposable variable (refer with #)
                SetDisposableVariable(ps[0], ps[1], ps[2]);
                break;
            case "dispose": //disposes a variable (removes from variable dict)
                Dispose(ps[0]);
                break;
            case "disposeGlobal": //disposes a variable (removes from variable dict)
                DisposeGlobal(ps[0]);
                break;
            case "autoDispose": //dispose all disposable variables
                AutoDispose();
                break;
            case "ad": //alias for autodispose
                AutoDispose();
                break;
            case "log": //logs a string
                LogMessage(ps[0]);
                break;
            case "alert": //popup alert in game with title and message
                Alert(ps[0], ps[1]);
                break;
            case "wcoords++": //increments wcoords with 2 ints
                IncrementwCoords(ps[0], ps[1], ps[2]);
                break;
            case "call": //calls an action
                CallAction(ps[0]);
                break;
            case "callChild": //calls an action as a child action (inherits variables)
                CallChildAction(ps[0]);
                break;
            case "callAt": //calls an action at a specific origin
                CallActionAt(ps[0], ps[1]);
                break;
            case "return": //aborts the action
                Return();
                break;
            case "loop": //starts a loop
                Loop(ps[0]);
                break;
            case "back": //loops back to the loop with matching id
                Back(ps[0]);
                break;
            case "backPer": //loops back to the loop with matching id based on the count of an area. basically a foreach loop
                BackPer(ps[0],ps[1]);
                break;


            //FLAGS
            case "isUnit": //checks if the unit on tile is the type of unit specified
                IsUnit(ps[0],ps[1],ps[2]);
                break;
            case "containsUnit": //checks if the area has a unit of type
                ContainsUnit(ps[0],ps[1],ps[2]);
                break;
            case "not":
                Not(ps[0]);
                break;
            case "isTribe": //checks if owner is a tribe of specified type
                IsTribe(ps[0],ps[1]);
                break;
            

            //FUNCTIONS
            case "getRadius": //gets an area around an origin and returns it to a variable
                GetRadiusFromOrigin(ps[0],ps[1],ps[2],ps[3]);
                break;
            case "getOrigin": //gets the origin of the action
                GetActionOrigin(ps[0]);
                break;
            case "getX": //get the x of a wcoords
                GetX(ps[0], ps[1]);
                break;
            case "getY": //get the y of a wcoords
                GetY(ps[0], ps[1]);
                break;
            case "getMember":
                GetMember(ps[0],ps[1],ps[2]);
                break;
            case "getCount":
                GetCount(ps[0], ps[1]);
                break;
            case "getCapital": //gets the owners capital
                GetCapital(ps[0]);
                break;

            
            //COMMANDS
            case "addStars": //builds an improvement
                AddCurrency(ps[0],ps[1],ps[2]);
                break;
            case "build": //builds an improvement
                Build(ps[0],ps[1],ps[2]);
                break;
            case "destroy": //destroys an improvement
                Destroy(ps[0]);
                break;
            case "addTileEffect": //gives a tile a tileeffect
                AddTileEffect(ps[0], ps[1]);
                break;
            case "addUnitEffect": //gives a unit a uniteffect
                AddUnitEffect(ps[0], ps[1]);
                break;
            case "removeTileEffect": 
                RemoveTileEffect(ps[0], ps[1]);
                break;
            case "removeUnitEffect": 
                RemoveUnitEffect(ps[0], ps[1]);
                break;
            case "unitExhaustion": //sets the unit's exhaustion state
                SetUnitExhaustion(ps[0],ps[1],ps[2]);
                break;
            case "trainUnit": //trains a unit
                TrainUnit(ps[0],ps[1],ps[2]);
                break;
            case "attackUnit": //attack a unit with another unit
                AttackUnit(ps[0],ps[1],ps[2]);
                break;
            case "damageUnit": //damage a unit with calculated damage from another unit
                DamageUnit(ps[0],ps[1],ps[2]);
                break;
            case "damageUnitManual": //damage a unit with specified damage
                DamageUnitManual(ps[0],ps[1],ps[2],ps[3]);
                break;
            case "healUnit": //heals a unit
                HealUnit(ps[0],ps[1]);
                break;
            case "convertUnit": //converts a unit from an origin
                ConvertUnit(ps[0],ps[1]);
                break;
            case "explore": //explores a tile
                Explore(ps[0]);
                break;
            case "promote": //promotes a unit
                Promote(ps[0]);
                break;
            case "upgrade": //upgrades a unit to the specified type
                Upgrade(ps[0],ps[1]);
                break;
            case "reveal":
                Reveal(ps[0],ps[1]);
                break;
            case "kill":
                KillUnit(ps[0]);
                break;
            case "research":
                Research(ps[0],ps[1]);
                break;
            case "ruleArea":
                RuleArea(ps[0]);
                break;
            case "addCityReward":
                AddCityReward(ps[0],ps[1]);
                break;
            case "increaseScore":
                IncreaseScore(ps[0],ps[1]);
                break;
            case "decreaseScore":
                DecreaseScore(ps[0]);
                break;
            case "sfx": //plays a sound effect
                PlaySfx(ps[0]);
                break;
            case "vfx": //plays a visual effect on a tile
                Vfx(ps[0], ps[1]);
                break;
            case "screenShake": //yo momma when she farts
                ScreenShake(ps[0], ps[1]);
                break;
            default:
                LogError("RunFunction", $"Could'nt find command '{command}'");
                break;
        }
    }

    #region generic
    
    private void SetVariable(string varName, string obj, string value)
    {
        SwitchType(obj, value, out var valueObj);
        variables['@' + varName] = valueObj;
    }

    private void SetGlobalVariable(string varName, string obj, string value)
    {
        SwitchType(obj, value, out var valueObj);
        Main.polibGameState.globalVariables['&' + varName] = valueObj;
    }

    private void SetDisposableVariable(string varName, string obj, string value)
    {
        SwitchType(obj, value, out var valueObj);
        variables['#' + varName] = valueObj;
    }

    private void Dispose(string variable)
    {
        if (!IsVariable<object>(variable, out var obj))
        {
            LogError("Dispose", "Variable is invalid. Reason: Either variable doesnt exist or spelling is incorrect.");
            return;
        }

        variables.Remove(variable);
    }

    private void DisposeGlobal(string variable)
    {
        if (!IsVariable<object>(variable, out var obj))
        {
            LogError("Dispose", "Variable is invalid. Reason: Either variable doesnt exist or spelling is incorrect.");
            return;
        }

        Main.polibGameState.globalVariables.Remove(variable);
    }


    private void AutoDispose()
    {
        foreach (KeyValuePair<string, object> kvp in variables)
        {
            if (kvp.Key[0] == '#')
            {
                variables.Remove(kvp.Key);
            }
        }
    }


    private void LogMessage(string msg)
    {
        modLogger.LogInfo(ParseString(msg));
    }

    private void Alert(string stitle, string smsg)
    {
        string title = ParseString(stitle);
        string msg = ParseString(smsg);

        NotificationManager.Notify(msg, title);
    }

    private void IncrementwCoords(string variable, string sx, string sy)
    {
        int x = ParseInt(sx);
        int y = ParseInt(sy);
        
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("IncrementWCoords", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords.");
            return;
        }


        variables[variable] = new WorldCoordinates(obj.X + x, obj.Y + y);
    }

    private void CallAction(string s)
    {
        string name = ParseString(s);

        if (Parse.actions.TryGetValue(name, out pAction action))
        {
            LogError("CallAction", $"Couldn't find action '{name}'");
            return;
        }

        PolibUtils.RunAction(name, ActionOrigin, playerId);
    }

    private void CallChildAction(string s)
    {
        string name = ParseString(s);

        if (Parse.actions.TryGetValue(name, out pAction action))
        {
            LogError("CallAction", $"Couldn't find action '{name}'");
            return;
        }

        PolibUtils.RunChildAction(name, ActionOrigin, playerId, variables);
    }

    private void CallActionAt(string s, string swcoords)
    {
        string name = ParseString(s);
        WorldCoordinates wcoords = ParseWcoords(swcoords);

        if (Parse.actions.TryGetValue(name, out pAction action))
        {
            LogError("CallAction", $"Couldn't find action '{name}'");
            return;
        }

        PolibUtils.RunAction(name, wcoords, playerId);
    }

    private void Return()
    {
        index = lines.Length;
    }
    private void Loop(string sid)
    {
        string id = ParseString(sid);

        cycleIds[id] = index;
    }
    private void Back(string sid)
    {
        string id = ParseString(sid);

        if (!cycleIds.TryGetValue(id, out var i))
        {
            LogError("Back",$"Couldn't find start of cycle '{id}'");
            return;
        }

        index = i;
    }

    private void BackPer(string sid, string sarea)
    {
        List<WorldCoordinates> area = ParseWcoordsList(sarea);

        string id = ParseString(sid);

        if (!cycleIds.TryGetValue(id, out var i))
        {
            LogError("Back",$"Couldn't find start of cycle '{id}'");
            return;
        }

        if (!variables.TryGetValue("#" + id + "_loopIndex", out var dun))
        {
            variables["#" + id + "_loopIndex"] = 0;
            dun = 0;
        }

        if (dun is int)
        {
            int dunint = (int)dun;

            dunint++;
            variables["#" + id + "_loopIndex"] = dunint;
            modLogger.LogInfo($"{dunint}, count: {area.Count}");

            if (dunint < area.Count)
            {
                index = i;
            }
            else
            {
                variables.Remove("#" + id + "_loopIndex");
            }
        }
        else
        {
            LogError("BackPer",$"Index variable already in use, and its not an int. Consider not using {id + "_loopIndex"} as a disposable variable, dumbass. If you are looking at this as an accident I am in awe at how unbelievably goddamn stupid you are. If you are looking at this cause you're verifying the mod and saw a very very long log message, then get back to work, we dont have all day (or all year, for the matter AHEM AHEM WUBL AHEM)");
            return;
        }
    }

    #endregion

    #region flags

    private void IsUnit(string variable, string swcoords, string sunit)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        UnitData.Type unit = ParseUnitDataType(sunit);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;
        TileData tile = map.GetTile(wcoords);

        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("IsUnit", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        if (tile.unit == null) return;

        variables[variable] = tile.unit.type == unit;
    }

    private void HasUnit(string variable, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;
        TileData tile = map.GetTile(wcoords);

        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("IsUnit", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = tile.unit != null;
    }

    private void ContainsUnit(string variable, string swcoordslist, string sunit)
    {
        List<WorldCoordinates> wcoordslist = ParseWcoordsList(swcoordslist);
        UnitData.Type unit = ParseUnitDataType(sunit);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;
        List<UnitData.Type> units = new List<UnitData.Type>();
        foreach (WorldCoordinates coords in wcoordslist)
        {
            if (map.GetTile(coords).unit == null) continue;
            units.Add(map.GetTile(coords).unit.type);
        }

        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("ContainsUnit", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = units.Contains(unit);
    }

    private void Not(string variable)
    {
        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("Not", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = !obj;
    }

    private void IsTribe(string variable, string stribeType)
    {
        TribeType tribeType = ParseTribeType(stribeType);

        if (!GameManager.GameState.TryGetPlayer(playerId, out var playerState))
        {
            LogError("IsTribe", "Owner doesn't exist, somehow. Mate if you see this consider yourself beyond cooked.");
            return;
        }
        
        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("IsTribe", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = playerState.tribe == tribeType;
    }
    #endregion

    #region functions

    private void GetRadiusFromOrigin(string variable, string sorigin, string sradius, string sallowCenter)
    {
        if (!IsVariable<List<WorldCoordinates>>(variable, out var obj))
        {
            LogError("GetRadiusFromOrigin", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: area.");
            return;
        }
        
        WorldCoordinates origin = ParseWcoords(sorigin);
        int radius = ParseInt(sradius);
        bool allowCenter = ParseBool(sallowCenter);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;

        List<WorldCoordinates> list = new List<WorldCoordinates>();

        foreach (TileData tile in map.GetArea(origin, radius, true, allowCenter))
        {
            list.Add(tile.coordinates);
        }

        variables[variable] = list; //who in the ever living fuck would want to exclude diagonals?? //i remembered water exists

    }

    private void GetActionOrigin(string variable)
    {
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("GetActionOrigin", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords.");
            return;
        }

        variables[variable] = ActionOrigin;
    }

    private void GetX(string variable, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        
        if (!IsVariable<int>(variable, out var obj))
        {
            LogError("GetX", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: int.");
            return;
        }

        variables[variable] = wcoords.x;
    }
    private void GetY(string variable, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        
        if (!IsVariable<int>(variable, out var obj))
        {
            LogError("GetY", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: int.");
            return;
        }

        variables[variable] = wcoords.y;
    }

    private void GetMember(string variable, string swcoordslist, string si)
    {
        List<WorldCoordinates> wcoordslist = ParseWcoordsList(swcoordslist);
        int i = ParseInt(si);
        
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("GetMember", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords");
            return;
        }


        variables[variable] = wcoordslist[i];
    }

    private void GetCount(string variable, string swcoordslist)
    {
        List<WorldCoordinates> wcoordslist = ParseWcoordsList(swcoordslist);
        
        if (!IsVariable<int>(variable, out var obj))
        {
            LogError("GetCount", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: int");
            return;
        }


        variables[variable] = wcoordslist.Count;
    }

    private void GetCapital(string variable)
    {   
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("GetCapital", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords.");
            return;
        }
        GameManager.GameState.TryGetPlayer(playerId, out PlayerState playerState);
        variables[variable] = playerState.GetCurrentCapitalCoordinates(GameManager.GameState);
    }

    #endregion
    #region commands

    private void AddCurrency(string si, string swcoords, string sdelay)
    {
        int i = ParseInt(si);
        int delay = ParseInt(sdelay);
        WorldCoordinates wcoords = ParseWcoords(swcoords);

        GameManager.GameState.ActionStack.Add(new IncreaseCurrencyAction(playerId, wcoords, i, delay));
    }
    
    private void Build(string swcoords, string simprovement, string sdeductCost)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);;
        ImprovementData.Type imp = ParseImprovementDataType(simprovement);
        bool deductCost = ParseBool(sdeductCost);
        
        GameManager.GameState.ActionStack.Add(new BuildAction(playerId, imp, wcoords, deductCost));
    }

    private void Destroy(string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);;
        
        GameManager.GameState.ActionStack.Add(new DestroyImprovementAction(playerId, wcoords));
    }

    private void AddTileEffect(string stileEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        TileData.EffectType effect = ParseTileEffectType(stileEffectType);

        if (Tile(wcoords).HasEffect(effect)) return;

        Tile(wcoords).AddEffect(effect);
    }

    private void AddUnitEffect(string sunitEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        UnitEffect effect = ParseUnitEffectType(sunitEffectType);

        if (Tile(wcoords).unit == null)
        {
            LogError("AfflictUnit", "Unit is null");
            return;
        }

        if (Tile(wcoords).unit.HasEffect(effect)) return;

        Tile(wcoords).unit.AddEffect(effect);
    }

    private void RemoveTileEffect(string stileEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        TileData.EffectType effect = ParseTileEffectType(stileEffectType);

        Tile(wcoords).RemoveEffect(effect);
    }

    private void RemoveUnitEffect(string sunitEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        UnitEffect effect = ParseUnitEffectType(sunitEffectType);

        if (Tile(wcoords).unit == null)
        {
            LogError("RemoveUnitEffect", "Unit is null");
            return;
        }

        Tile(wcoords).unit.RemoveEffect(effect);
    }
    
    private void SetUnitExhaustion(string swcoords, string smoved, string sattacked)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        bool moved = ParseBool(smoved);
        bool attacked = ParseBool(sattacked);

        if (Tile(wcoords).unit == null)
        {
            LogError("SetUnitExhaustion", "Unit is null");
            return;
        }

        Tile(wcoords).unit.moved = moved;
        Tile(wcoords).unit.attacked = attacked;
    }

    private void TrainUnit(string sunit, string swcoords, string sdeductCost)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        UnitData.Type unit = ParseUnitDataType(sunit);
        bool deductCost = ParseBool(sdeductCost);

        GameLogicData gameLogicData = new GameLogicData();
        int cost = deductCost ? gameLogicData.GetUnitData(unit).cost : 0;
        GameManager.GameState.ActionStack.Add(new TrainAction(playerId, unit, wcoords, cost));
    }

    private void AttackUnit(string sorigin, string starget, string shouldMove)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        WorldCoordinates target = ParseWcoords(starget);
        bool move = ParseBool(shouldMove);

        if (Tile(origin).unit == null || Tile(target).unit == null) return;

        GameState gameState = GameManager.GameState;
        
        BattleResults battleResults = BattleHelpers.GetBattleResults(gameState, Tile(origin).unit, Tile(target).unit);
        gameState.ActionStack.Add(new AttackAction(playerId, origin, target, battleResults.attackDamage, move, AttackAction.AnimationType.Normal, 20));
    }

    private void DamageUnit(string sorigin, string starget, string shouldMove)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        WorldCoordinates target = ParseWcoords(starget);
        bool move = ParseBool(shouldMove);

        if (Tile(origin).unit == null || Tile(target).unit == null) return;

        GameState gameState = GameManager.GameState;
        
        BattleResults battleResults = BattleHelpers.GetBattleResults(gameState, Tile(origin).unit, Tile(target).unit);
        gameState.ActionStack.Add(new AttackAction(playerId, target, target, battleResults.attackDamage, move, AttackAction.AnimationType.Splash, 20));
    }

    private void DamageUnitManual(string sorigin, string starget, string si, string shouldMove)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        WorldCoordinates target = ParseWcoords(starget);
        int i = ParseInt(si);
        bool move = ParseBool(shouldMove);

        if (Tile(origin).unit == null || Tile(target).unit == null) return;

        GameState gameState = GameManager.GameState;
        
        gameState.ActionStack.Add(new AttackAction(playerId, origin, target, i, move, AttackAction.AnimationType.Splash, 20));
    }

    private void HealUnit(string swcoords, string si)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        int i = ParseInt(si);

        if (Tile(wcoords).unit == null)
        {
            LogError("HealUnit", "Unit is null");
            return;
        }

        GameState gameState = GameManager.GameState;
        PolibUtils.HealUnit(gameState, Tile(wcoords).unit, i);
    }

    private void ConvertUnit(string sorigin, string starget)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        WorldCoordinates target = ParseWcoords(starget);

        if (Tile(origin).unit == null || Tile(target).unit == null) return;

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new ConvertAction(playerId, origin, target));
    }

    private void Explore(string sorigin)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new ExploreAction(playerId, origin));
    }

    private void Promote(string sorigin)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);

        if (Tile(origin).unit == null)
        {
            LogError("Promote", "Unit is null");
            return;
        }


        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new PromoteAction(playerId, origin));
    }

    private void Upgrade(string sorigin, string sunit)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        UnitData.Type type = ParseUnitDataType(sunit);

        GameLogicData gld = new GameLogicData();
        UnitData data = gld.GetUnitData(type);

        if (Tile(origin).unit == null)
        {
            LogError("Upgrade", "Unit is null");
            return;
        }
        
        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new UpgradeAction(playerId, type, origin, data.cost));
    }

    private void Reveal(string sorigin, string sshowPopup)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        bool showPopup = ParseBool(sshowPopup);

        if (Tile(origin).unit == null)
        {
            return;
        }

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new RevealAction(playerId, origin, showPopup));
    }

    private void KillUnit(string sorigin)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);

        if (Tile(origin).unit == null)
        {
            LogError("KillUnit", "Unit is null");
            return;
        }

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new KillUnitAction(playerId, origin));
    }

    private void Research(string stech, string si)
    {
        TechData.Type tech = ParseTechDataType(stech);
        int i = ParseInt(si);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new ResearchAction(playerId, tech, i));
    }

    private void RuleArea(string sorigin)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new RuleAreaAction(playerId, origin));
    }

    private void AddCityReward(string sorigin, string sreward)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        CityReward reward = ParseCityRewardType(sreward);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new CityRewardAction(playerId, reward, origin));
    }

    private void IncreaseScore(string si, string sorigin)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        int i = ParseInt(si);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new IncreaseScoreAction(playerId, i, origin));
    }

    private void DecreaseScore(string si)
    {
        int i = ParseInt(si);

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new DecreaseScoreAction(playerId, i));
    }

    private void PlaySfx(string ssfx)
    {
        string sfx = ParseString(ssfx);
        
        if (!EnumCache<SFXTypes>.TryGetType(sfx, out var enumValue))
        {
            LogError("PlaySfx", "SFXType doesn't exist.");
            return;
        }
        
        AudioManager.PlaySFX(enumValue);
    }

    private void Vfx(string svfx, string swcoords)
    {
        string vfx = ParseString(svfx);
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        
        Tile tile = sTile(wcoords);

        if (tile == null)
        {
            LogError("Vfx", "Couldn't find tile");
            return;
        }

        GameManager.GameState.TryGetPlayer(Tile(wcoords).owner, out var playerState);
        SkinType skinType = (playerState != null) ? playerState.skinType : SkinType.None;

        switch (vfx)
        {
            case "darkpuff":
                tile.SpawnDarkPuff();
                break;
            case "embers":
                tile.SpawnEmbers();
                break;
            case "explosion":
                tile.SpawnExplosion();
                break;
            case "green":
                tile.SpawnGreenParticles(); //??
                break;
            case "halo":
                tile.SpawnHalo();
                break;
            case "heal":
                tile.SpawnHeal();
                break;
            case "love":
                tile.SpawnLove();
                break;
            case "poison":
                tile.SpawnPoison(skinType);
                break;
            case "puff":
                tile.SpawnPuff();
                break;
            case "shine":
                tile.SpawnShine();
                break;
            case "sparkles":
                tile.SpawnSparkles();
                break;
            
            case "startfire":
                tile.SpawnFire();
                break;
            case "stopfire":
                tile.StopFire();
                break;

            case "startfoam":
                tile.SpawnFoam();
                break;
            case "stopfoam":
                tile.StopFoam();
                break;
            
            case "startrainbowfire":
                tile.SpawnRainbowFire();
                break;
            case "stoprainbowfire":
                tile.StopRainbowFire(false);
                break;
        }
    }

    private void ScreenShake(string sduration, string samount)
    {
        float duration = ParseInt(sduration);
        float amount = ParseInt(samount);

        PolibUtils.ShakeCamera(duration / 10, amount);
    }
    #endregion












    #region utils

    private void LogError(string origin, string msg)
    {   
        modLogger.LogError($"{name}: Error from {origin}: " + msg);
    }

    private TileData Tile(WorldCoordinates coordinates)
    {
        return GameManager.GameState.Map.GetTile(coordinates);
    }

    private Tile sTile(WorldCoordinates coordinates)
    {
        return MapRenderer.Current.GetTileInstance(coordinates);
    }

    
    private string CheckParams(string p)
    {
        if (p.Contains('+'))
        {
            string[] ab = p.Split('+', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A + B).ToString();
        }

        if (p.Contains('-'))
        {
            string[] ab = p.Split('-', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A - B).ToString();
        }

        if (p.Contains('*'))
        {
            string[] ab = p.Split('*', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A * B).ToString();
        }

        if (p.Contains('/'))
        {
            string[] ab = p.Split('/', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A / B).ToString();
        }

        if (p.Contains("=="))
        {
            string[] ab = p.Split('=', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A == B).ToString();
        }

        if (p.Contains('>'))
        {
            string[] ab = p.Split('>', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A > B).ToString();
        }

        if (p.Contains('<'))
        {
            string[] ab = p.Split('<', 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A < B).ToString();
        }

        if (p.Contains(">="))
        {
            string[] ab = p.Split(">=", 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A >= B).ToString();
        }

        if (p.Contains("<="))
        {
            string[] ab = p.Split("<=", 2);

            int A = ParseInt(ab[0]);
            int B = ParseInt(ab[1]);

            return (A <= B).ToString();
        }

        if (p.Contains("=w="))
        {
            string[] ab = p.Split("=w=", 2);

            WorldCoordinates A = ParseWcoords(ab[0]);
            WorldCoordinates B = ParseWcoords(ab[1]);

            return (A.X == B.X && A.Y == B.Y).ToString();
        }

        if (p.Contains("~"))
        {
            string[] ab = p.Split("~", 2);

            List<WorldCoordinates> A = ParseWcoordsList(ab[0]);
            int B = ParseInt(ab[1]);

            modLogger.LogInfo(A.Count - 1 + ":" + B);

            return ToScript(A[B]);
        }

        return p;
    }

    private string ToScript(WorldCoordinates coordinates)
    {
        return coordinates.X + ";" + coordinates.Y;
    }

    private void SwitchType(string obj, string value, out object valueObj)
    {
        valueObj = null;
        switch (obj)
        {
            case "int": //0
                valueObj = ParseInt(value);
                break;

            case "string": //helloworld! (no spaces allowed) OR §hello world!§ (spaces allowed)
                valueObj = ParseString(value);
                break;

            case "bool":
                valueObj = ParseBool(value);
                break;

            case "wcoords": //0;0
                valueObj = ParseWcoords(value);
                break;
            
            case "area": //0;0|0;0|0;0|0;0
                valueObj = ParseWcoordsList(value);
                break;

            case "unitType":
                valueObj = ParseUnitDataType(value);
                break;
            case "improvementType":
                valueObj = ParseImprovementDataType(value);
                break;
            case "techType":
                valueObj = ParseTechDataType(value);
                break;
            case "tribeType":
                valueObj = ParseTribeType(value);
                break;
            case "cityRewardType":
                valueObj = ParseCityRewardType(value);
                break;
            case "unitEffectType":
                valueObj = ParseUnitEffectType(value);
                break;
            case "tileEffectType":
                valueObj = ParseTileEffectType(value);
                break;
        }
    }

    
    private bool IsVariable<T>(string s, out T obj)
    {
        if (string.IsNullOrEmpty(s)) 
        {
            obj = default;
            LogError("IsVariable", "String is empty");
            return false;
        }

        if (s[0] == 'ص')
        {
            s = s.Replace('ص', '@'); //you happy? huh? YOU GOT YOUR ARABIC LETTER ARE YOU SATISFIED?
        }
        if (s[0] == '@' || s[0] == '#')
        {
            if (variables.TryGetValue(s, out var value))
            {
                obj = (T)value;
                if (obj != null)
                {
                    return true;
                }
            }
            else if (typeof(T) == typeof(int) && s.Contains("_loopIndex"))
            {
                obj = default;
                return true;
            }
        }
        if (s[0] == '&')
        {
            if (Main.polibGameState.globalVariables.TryGetValue(s, out var value))
            {
                obj = (T)value;
                if (obj != null)
                {
                    return true;
                }
            }
        }
        obj = default;
        return false;
    }

    private int ParseInt(string value)
    {
        if (IsVariable<int>(value, out var obj))
        {
            return obj;
        }
        if (int.TryParse(value, out var parsedInt))
        {
            return parsedInt;
        }
        else
        {
            LogError("ParseInt", $"Invalid int format. '{value}' Correct format: 0 . eg. set:var int 5");
            return 0;
        }
    }

    private string ParseString(string value)
    {
        if (IsVariable<string>(value, out var obj))
        {
            return obj;
        }
        return value;
    }

    private WorldCoordinates ParseWcoords(string value)
    {
        if (IsVariable<WorldCoordinates>(value, out var obj))
        {
            return obj;
        }
        string[] strings = value.Split(';');
        WorldCoordinates wcoords = new WorldCoordinates(0, 0);

        if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
        {
            wcoords.X = X;
            wcoords.Y = Y;
            return wcoords;
        }
        else
        {
            LogError("ParseWcoords", $"Invalid wcoords format. '{value}' Correct format: 0;0 . eg. set:var wcoords 0;0");
            return default;
        }
    }

    private List<WorldCoordinates> ParseWcoordsList(string value)
    {
        if (IsVariable<List<WorldCoordinates>>(value, out var obj))
        {
            return obj;
        }
        string[] splitValues = value.Split('|');

        List<WorldCoordinates> wcoordslist = new List<WorldCoordinates>();

        int i = 0;
        foreach (string newValue in splitValues)
        {
            string[] strings = newValue.Split(';');
            WorldCoordinates wcoords = new WorldCoordinates(0, 0);

            if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
            {
                wcoords.X = X;
                wcoords.Y = Y;
                wcoordslist.Add(wcoords);
            }
            else
            {
                LogError("ParseWcoordsList", $"Invalid wcoords format. '{value}' Correct format: 0;0 . eg. set:var wcoords 0;0");
            }
            i++;
        }
        return wcoordslist;
    }

    private bool ParseBool(string value)
    {
        if (IsVariable<bool>(value, out var obj))
        {
            return obj;
        }
        if (bool.TryParse(value, out var parsedBool))
        {
            return parsedBool;
        }
        else
        {
            LogError("ParseBool", $"Invalid bool format. '{value}' Correct format: true/false . eg. set:var bool false");
            
            return true;
        }
    }

    private UnitData.Type ParseUnitDataType(string value)
    {
        if (IsVariable<UnitData.Type>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<UnitData.Type>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseUnitDataType", $"Couldn't find {value} unit. Check spelling or idk.");
            return default;
        }
    }

    private ImprovementData.Type ParseImprovementDataType(string value)
    {
        if (IsVariable<ImprovementData.Type>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<ImprovementData.Type>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseImprovementDataType", $"Couldn't find {value} improvement. Check spelling or idk.");
            return default;
        }
    }

    private TechData.Type ParseTechDataType(string value)
    {
        if (IsVariable<TechData.Type>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<TechData.Type>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseTechDataType", $"Couldn't find {value} tech. Check spelling or idk.");
            return default;
        }
    }

    private TribeType ParseTribeType(string value)
    {
        if (IsVariable<TribeType>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<TribeType>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseTribeType", $"Couldn't find {value} tribe. Check spelling or idk.");
            return default;
        }
    }

    private CityReward ParseCityRewardType(string value)
    {
        if (IsVariable<CityReward>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<CityReward>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseCityRewardType", $"Couldn't find {value} city reward. Check spelling or idk.");
            return default;
        }
    }

    private UnitEffect ParseUnitEffectType(string value)
    {
        if (IsVariable<UnitEffect>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<UnitEffect>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseUnitEffectDataType", $"Couldn't find {value} unit effect. Check spelling or idk.");
            return default;
        }
    }

    private TileData.EffectType ParseTileEffectType(string value)
    {
        if (IsVariable<TileData.EffectType>(value, out var obj))
        {
            return obj;
        }
        if (EnumCache<TileData.EffectType>.TryGetType(value, out var enumValue))
        {
            return enumValue;
        }
        else
        {
            LogError("ParseTileEffectDataType", $"Couldn't find {value} tile effect. Check spelling or idk.");
            return default;
        }
    }
    #endregion
}