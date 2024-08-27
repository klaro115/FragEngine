namespace FragEngine3.Scenes.Data;

/// <summary>
/// Common interface for serializable data types, shared across all scene element types.
/// </summary>
public interface ISceneElementData
{
	#region Properties

	/// <summary>
	/// Scene element ID of the object represented by this data.
	/// </summary>
	int ID { get; }

	/// <summary>
	/// The type of scene element this data represents.
	/// </summary>
	SceneElementType ElementType { get; }

	#endregion
}
