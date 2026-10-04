using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine.ResourceManagement.Util;

namespace Polibrary.PolyScript;
public class PolibCommandBase : CommandBase
{
	public PolibCommandBase(IntPtr ptr) : base(ptr) {}
	public PolibCommandBase() {}

	public PolibCommandBase(byte playerId) 
    : base(playerId)
	{
		base.PlayerId = playerId;
	}

	public override bool IsValid(GameState state, out string validationError)
	{
        validationError = "";
		return true;
	}

	public override CommandType GetCommandType()
	{
		CommandType type = EnumCache<CommandType>.GetType("polibcommandbase");
		return type;
	}

	public virtual void ExecuteNew(GameState state)
	{

	}

	public override bool ShouldAskForConfirmation()
	{
		return false;
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