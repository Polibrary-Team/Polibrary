using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine.ResourceManagement.Util;

namespace Polibrary;
public class PolibReactionBase : ReactionBase
{
    public virtual ActionBase actionProperty {get; set;}
	public PolibReactionBase(IntPtr ptr) : base(ptr) {}
	public PolibReactionBase() {}

    public override void Execute(Il2CppSystem.Action onComplete)
    {
        base.Execute(onComplete);
    }

	public override string ToString()
	{
		return string.Format("{0}", new object[]
		{
			base.GetType()
		});
	}
}
/* example
public class TestReaction : PolibReactionBase
{
    protected PolibActionBase action;
    public override ActionBase actionProperty 
    { 
        get => this.action; 
        set
        {
            PolibActionBase polibActionBase = value.TryCast<PolibActionBase>();
            if (polibActionBase != null)
            this.action = polibActionBase;
            else
            Main.modLogger.LogInfo("shits fucked");
        } 
    }
    public TestReaction(IntPtr ptr) : base(ptr) {}
	public TestReaction(PolibActionBase action)
    {
        this.action = action;
    }

    public override bool ShouldFocusCamera()
    {
        return IsRecapOrOpponentAction(action);
    }

    public override WorldCoordinates GetCameraFocusCoordinates()
    {
        GameManager.GameState.TryGetPlayer(action.PlayerId, out var player);
        return player.GetCurrentCapitalCoordinates(GameManager.GameState);
    }

    public override void Execute(Il2CppSystem.Action onComplete)
    {
        GameManager.GameState.TryGetPlayer(action.PlayerId, out var player);
        TileData tile = GameManager.GameState.Map.GetTile(player.GetCurrentCapitalCoordinates(GameManager.GameState));
        Tile instance = tile.GetInstance();
        if (instance != null && !instance.IsHidden)
        {
            instance.Render();
            instance.Sway();
            AudioManager.PlaySFXAtTile(SFXTypes.Transform, tile.coordinates);
            GameManager.DelayCall(FreezeTileAction.DEFAULT_TIME_MILLISECONDS, onComplete);
        }
        else
        {
            onComplete.Invoke();
        }
    }
}*/