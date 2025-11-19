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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;
using pbb = PolytopiaBackendBase.Common;
using Il2CppSystem.Net.Http.Headers;


namespace Polibrary;

public class ApplyEffectAction : ActionBase
{
    public WorldCoordinates coordinates { get; set; }
    public UnitEffect effect { get; set; }
    public ApplyEffectAction(WorldCoordinates coords, UnitEffect ieffect)
    {
        coordinates = coords;
        effect = ieffect;
    }

    public override void Execute(GameState gameState)
    {
        gameState.Map.GetTile(coordinates).unit.AddEffect(effect);
    }

    public override ActionType GetActionType()
	{
		return EnumCache<ActionType>.GetType("polib_applyeffectaction");
	}
}