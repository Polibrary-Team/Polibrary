using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine.ResourceManagement.Util;

namespace Polibrary;
public class PolibCommandBase : CommandBase
{
	public PolibCommandBase(IntPtr ptr) : base(ptr) {}
	public PolibCommandBase() {}

	public PolibCommandBase(byte playerId) 
    : base(playerId)
	{
		
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
		PolibActionBase action = ActionManager.MakeIl2CppAction<PolibActionBase>();
		state.ActionStack.Add(action);
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

/*

Example of this API

public class TestCommand : PolibCommandBase
{
	public int ExampleParam; 
	public TestCommand(IntPtr ptr) : base(ptr) {}
	public TestCommand() {}
	public TestCommand(byte playerId, int exampleParam) 
    : base(playerId)
	{
		ExampleParam = exampleParam;
	}

    public override void ExecuteNew(GameState state)
    {
        base.ExecuteNew(state);
		Main.modLogger.LogInfo("wow i feel like calling an action here");
    }

	public override void SerializeNew(Il2CppSystem.IO.BinaryWriter writer, int version)
	{
		writer.Write(ExampleParam);
	}

	public override void DeserializeNew(Il2CppSystem.IO.BinaryReader reader, int version)
	{
		ExampleParam = reader.ReadInt32();
	}
	
	public override CommandType GetCommandType()
	{
		CommandType type = EnumCache<CommandType>.GetType("testcommand");
		return type;
	}

    public override string ToString()
    {
        return string.Format("{0} (PlayerId: {1}, ExampleParam: {2})", new object[]
		{
			base.GetType(),
            base.PlayerId,
			this.ExampleParam
		});
    }
}
*/