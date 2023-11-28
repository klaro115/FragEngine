
namespace FragEngine3.Scenes
{
	public interface IUpdatableSceneElement : ISceneElement
	{
		#region Properties

		public SceneUpdateStage UpdateStageFlags { get; }

		#endregion
		#region Methods

		bool HandleUpdate(SceneUpdateStage _updateStage);

		#endregion
	}
}
