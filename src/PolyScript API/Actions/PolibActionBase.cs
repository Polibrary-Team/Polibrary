using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine.ResourceManagement.Util;

namespace Polibrary;
public class PolibActionBase : ActionBase
{
	public PolibActionBase(IntPtr ptr) : base(ptr) {}
	public PolibActionBase() {}

	public PolibActionBase(byte playerId) 
    : base(playerId)
	{
		
	}
	
	public override bool IsValid(GameState state)
    {
        return true;
    }

    public override ActionType GetActionType()
    {
        return EnumCache<ActionType>.GetType("polibactionbase");
    }
    
	public override void Execute(GameState state)
	{
		Main.modLogger.LogInfo("yay!");
	}

    public override void Serialize(Il2CppSystem.IO.BinaryWriter writer, int version)
    {
        base.Serialize(writer, version);
    }

    public override void Deserialize(Il2CppSystem.IO.BinaryReader reader, int version)
    {
        base.Deserialize(reader, version);
    }

	public override string ToString()
	{
		return string.Format("{0} (PlayerId: {1})", new object[]
		{
			base.GetType(),
            base.PlayerId
		});
	}
}