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

	public virtual void ExecuteNew(GameState state)
	{
		
	}

	public virtual void SerializeNew(Il2CppSystem.IO.BinaryWriter writer, int version)
	{
		//Empty cause its postfixed. Users still have to define this
	}

	public virtual void DeserializeNew(Il2CppSystem.IO.BinaryReader reader, int version)
	{
		//Same
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