namespace FragEngine3.Scenes;

public interface ISceneElement : IDisposable
{
	#region Properties

	/// <summary>
	/// Gets whether this object has been disposed already.
	/// </summary>
	bool IsDisposed { get; }

	/// <summary>
	/// Gets the general category of scene elements this object belongs to.
	/// </summary>
	SceneElementType ElementType { get; }

	#endregion
}
