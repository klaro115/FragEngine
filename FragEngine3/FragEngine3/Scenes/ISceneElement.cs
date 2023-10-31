using FragEngine3.Scenes.Data;

namespace FragEngine3.Scenes
{
	public interface ISceneElement : IDisposable
	{
		#region Properties

		bool IsDisposed { get; }
		SceneElementType ElementType { get; }

		#endregion
	}
}
