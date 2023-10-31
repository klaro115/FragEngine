using FragEngine3.Scenes.Utility;

namespace FragEngine3.Scenes.Data
{
	public interface ISceneElementData
	{
		#region Properties

		int ID { get; }
		SceneElementType ElementType { get; }

		#endregion
	}
}
