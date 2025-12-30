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
        modLogger.LogInfo($"{index}");
        while (index < lines.Length)
        {
            modLogger.LogInfo($"Reading line no. {index}");
            ReadLine(lines[index]);
        }
    }

    private void ReadLine(string line)
    {
        string[] commandAndParams; //pl. set:szam int 1

        commandAndParams = line.Split(":", 2); //set / szam int 1
        

        string command = commandAndParams[0]; //set
        string[] rawparameters = commandAndParams[1].Split(" "); //szam / int / 1

        bool isString = false;

        List<string> untrimmedParams = new List<string>();



        foreach (string s in rawparameters)
        {
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
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + " ";
                }
            }
            else
            {
                untrimmedParams.Add(s);

                if (s[0] == '§' && s[s.Length - 1] != '§')
                {
                    isString = true;
                    untrimmedParams[untrimmedParams.Count - 1] = untrimmedParams.Last() + " ";
                }
            }
        }

        List<string> parameters = new List<string>();
        foreach (string s in untrimmedParams)
        {
            parameters.Add(s.Trim('§'));
        }

        increment = 1; //reset increment to 1 so we only gotta write diff to those methods where it matters

        RunFunction(command, parameters);

        index += increment;
    }

    private void RunFunction(string command, List<string> ps)
    {
        bool run = true;

        if (command.Contains('?'))
        {
            string[] commandSplit = command.Split('?');
            run = ParseBool(commandSplit[1]);
        }
        else if (command.Contains('!'))
        {
            string[] commandSplit = command.Split('!');
            run = !ParseBool(commandSplit[1]);
        }

        if (!run)
        return;

        foreach (string p in ps)
        {
            CheckParams(p);
        }
        
        switch (command)
        {
            //GENERIC
            case "set": //sets a variable (refer with @)
                SetVariable(ps[0], ps[1], ps[2]);
                break;
            case "dispose": //disposes a variable (removes from variable dict)
                Dispose(ps[0]);
                break;
            case "setd": //sets a disposable variable (refer with #)
                SetDisposableVariable(ps[0], ps[1], ps[2]);
                break;
            case "autodispose": //dispose all disposable variables
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
            case "callchild": //calls an action as a child action (inherits variables)
                CallChildAction(ps[0]);
                break;
            case "callat": //calls an action at a specific origin
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


            //FLAGS
            case "isunit": //checks if the unit on tile is the type of unit specified
                IsUnit(ps[0],ps[1],ps[2]);
                break;
            case "containsunit": //checks if the area has a unit of type
                ContainsUnit(ps[0],ps[1],ps[2]);
                break;
            case "not":
                Not(ps[0]);
                break;
            

            //FUNCTIONS
            case "getradius": //gets an area around an origin and returns it to a variable
                GetRadiusFromOrigin(ps[0],ps[1],ps[2],ps[3]);
                break;
            case "getorigin": //gets the origin of the action
                GetActionOrigin(ps[0]);
                break;
            case "getx": //get the x of a wcoords
                GetX(ps[0], ps[1]);
                break;
            case "gety": //get the y of a wcoords
                GetY(ps[0], ps[1]);
                break;
            case "getmember":
                GetMember(ps[0],ps[1],ps[2]);
                break;
            case "getcount":
                GetCount(ps[0], ps[1]);
                break;

            
            //COMMANDS
            case "setimprovement": //sets an improvement on a tile
                SetImprovement(ps[0],ps[1],ps[2]);
                break;
            case "setimprovements": //sets improvements on tiles of an area
                SetImprovements(ps[0],ps[1],ps[2]);
                break;
            case "afflicttile": //gives a tile a tileeffect
                AfflictTile(ps[0], ps[1]);
                break;
            case "afflictunit": //gives a unit a uniteffect
                AfflictUnit(ps[0], ps[1]);
                break;
            case "unitexhaustion": //sets the unit's exhaustion state
                SetUnitExhaustion(ps[0],ps[1],ps[2]);
                break;
            case "trainunit": //trains a unit
                TrainUnit(ps[0],ps[1],ps[2]);
                break;
            case "attackunit": //attack a unit with another unit
                AttackUnit(ps[0],ps[1],ps[2]);
                break;
            case "healunit": //heals a unit
                HealUnit(ps[0],ps[1]);
                break;
            case "convertunit": //converts a unit from an origin
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
            case "sfx": //plays a sound effect
                PlaySfx(ps[0]);
                break;
            case "vfx": //plays a visual effect on a tile
                Vfx(ps[0], ps[1]);
                break;
            case "screenshake": //yo momma when she farts
                ScreenShake(ps[0], ps[1]);
                break;
        }
    }

    #region generic
    
    private void SetVariable(string varName, string obj, string value)
    {
        object valueObj = null;
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
            
            case "wcoords[]": //0;0|0;0|0;0|0;0
                valueObj = ParseWcoordsArray(value);
                break;

            case "unitType":
                valueObj = ParseUnitDataType(value);
                break;
            case "improvementType":
                valueObj = ParseImprovementDataType(value);
                break;
            case "unitEffectType":
                valueObj = ParseUnitEffectType(value);
                break;
            case "tileEffectType":
                valueObj = ParseTileEffectType(value);
                break;
        }
        variables['@' + varName] = valueObj;
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

    private void SetDisposableVariable(string varName, string obj, string value)
    {
        object valueObj = null;
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
            
            case "wcoords[]": //0;0|0;0|0;0|0;0
                valueObj = ParseWcoordsArray(value);
                break;

            case "unitType":
                valueObj = ParseUnitDataType(value);
                break;
            case "improvementType":
                valueObj = ParseImprovementDataType(value);
                break;
            case "unitEffectType":
                valueObj = ParseUnitEffectType(value);
                break;
            case "tileEffectType":
                valueObj = ParseTileEffectType(value);
                break;
        }
        variables['#' + varName] = valueObj;
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

        variables[variable] = tile.unit.type == unit;
    }

    private void ContainsUnit(string variable, string swcoordsarray, string sunit)
    {
        WorldCoordinates[] wcoordsarray = ParseWcoordsArray(swcoordsarray);
        UnitData.Type unit = ParseUnitDataType(sunit);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;
        List<UnitData.Type> units = new List<UnitData.Type>();
        foreach (WorldCoordinates coords in wcoordsarray)
        {
            units.Add(map.GetTile(coords).unit.type);
        }

        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("IsUnit", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = units.Contains(unit);
    }

    private void Not(string variable)
    {
        if (!IsVariable<bool>(variable, out var obj))
        {
            LogError("IsUnit", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: bool.");
            return;
        }

        variables[variable] = !obj;
    }
    #endregion

    #region functions

    private void GetRadiusFromOrigin(string variable, string sorigin, string sradius, string sallowCenter)
    {
        if (!IsVariable<WorldCoordinates[]>(variable, out var obj))
        {
            LogError("GetRadiusFromOrigin", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords[].");
            return;
        }
        
        WorldCoordinates origin = ParseWcoords(sorigin);
        int radius = ParseInt(sradius);
        bool allowCenter = ParseBool(sallowCenter);

        GameState gameState = GameManager.GameState;
        MapData map = gameState.Map;

        variables[variable] = map.GetArea(origin, radius, true, allowCenter); //who in the ever living fuck would want to exclude diagonals?? //i remembered water exists

    }

    private void GetActionOrigin(string variable)
    {
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("GetActionOrigin", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords[].");
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

    private void GetMember(string variable, string swcoordsarray, string si)
    {
        WorldCoordinates[] wcoordsarray = ParseWcoordsArray(swcoordsarray);
        int i = ParseInt(si);
        
        if (!IsVariable<WorldCoordinates>(variable, out var obj))
        {
            LogError("GetMember", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: wcoords");
            return;
        }


        variables[variable] = wcoordsarray[i];
    }

    private void GetCount(string variable, string swcoordsarray)
    {
        WorldCoordinates[] wcoordsarray = ParseWcoordsArray(swcoordsarray);
        
        if (!IsVariable<int>(variable, out var obj))
        {
            LogError("GetCount", "Variable is invalid. Reason: Either variable doesnt exist, spelling is incorrect or the variable is not of type: int");
            return;
        }


        variables[variable] = wcoordsarray.Length;
    }

    #endregion
    #region commands

    private void SetImprovement(string swcoords, string simprovement, string sdeductCost)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);;
        ImprovementData.Type imp = ParseImprovementDataType(simprovement);
        bool deductCost = ParseBool(sdeductCost);
        
        GameManager.GameState.ActionStack.Add(new BuildAction(playerId, imp, wcoords, deductCost));
    }

    private void SetImprovements(string swcoordsarray, string simprovement, string sdeductCost)
    {
        WorldCoordinates[] wcoordsarray = ParseWcoordsArray(swcoordsarray);
        ImprovementData.Type imp = ParseImprovementDataType(simprovement);
        bool deductCost = ParseBool(sdeductCost);

        foreach (WorldCoordinates wcoords in wcoordsarray)
        GameManager.GameState.ActionStack.Add(new BuildAction(playerId, imp, wcoords, deductCost));
    }

    private void AfflictTile(string stileEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        TileData.EffectType effect = ParseTileEffectType(stileEffectType);

        if (Tile(wcoords).unit == null)
        {
            LogError("AfflictTile", "TileEffect is null");
            return;
        }

        Tile(wcoords).AddEffect(effect);
    }

    private void AfflictUnit(string sunitEffectType, string swcoords)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        UnitEffect effect = ParseUnitEffectType(sunitEffectType);

        if (Tile(wcoords).unit == null)
        {
            LogError("AfflictUnit", "Unit is null");
            return;
        }

        Tile(wcoords).unit.AddEffect(effect);
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
        gameState.ActionStack.Add(new AttackAction(playerId, origin, target, battleResults.attackDamage, move, AttackAction.AnimationType.Splash, 20));
    }

    private void HealUnit(string swcoords, string si)
    {
        WorldCoordinates wcoords = ParseWcoords(swcoords);
        int i = ParseInt(si);


        GameState gameState = GameManager.GameState;
        PolibUtils.HealUnit(gameState, Tile(wcoords).unit, i);
    }

    private void ConvertUnit(string sorigin, string starget)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        WorldCoordinates target = ParseWcoords(starget);

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

        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new PromoteAction(playerId, origin));
    }

    private void Upgrade(string sorigin, string sunit)
    {
        WorldCoordinates origin = ParseWcoords(sorigin);
        UnitData.Type type = ParseUnitDataType(sunit);

        GameLogicData gld = new GameLogicData();
        UnitData data = gld.GetUnitData(type);
        
        GameState gameState = GameManager.GameState;
        gameState.ActionStack.Add(new UpgradeAction(playerId, type, origin, data.cost));
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

        if (p.Contains('='))
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

        return p;
    }

    
    private bool IsVariable<T>(string s, out T obj)
    {
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
            LogError("ParseInt", "Invalid int format. Correct format: 0 . eg. set:var int 5");
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
            LogError("ParseWcoords", "Invalid wcoords format. Correct format: 0;0 . eg. set:var wcoords 0;0");
            return default;
        }
    }

    private WorldCoordinates[] ParseWcoordsArray(string value)
    {
        if (IsVariable<WorldCoordinates[]>(value, out var obj))
        {
            return obj;
        }
        string[] splitValues = value.Split('|');
        WorldCoordinates[] wcoordsarray = new WorldCoordinates[splitValues.Length];

        int i = 0;
        foreach (string newValue in splitValues)
        {
            string[] strings = newValue.Split(';');
            WorldCoordinates wcoords = new WorldCoordinates(0, 0);

            if (int.TryParse(strings[0], out int X) && int.TryParse(strings[1], out int Y))
            {
                wcoords.X = X;
                wcoords.Y = Y;
                wcoordsarray[i] = wcoords;
            }
            else
            {
                LogError("ParseWcoordsArray", "Invalid wcoords format. Correct format: 0;0 . eg. set:var wcoords 0;0");
            }
            i++;
        }
        return wcoordsarray;
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
            LogError("ParseBool", "Invalid bool format. Correct format: true/false . eg. set:var bool false");
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