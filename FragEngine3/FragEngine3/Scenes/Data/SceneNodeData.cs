namespace FragEngine3.Scenes.Data;

/// <summary>
/// Serializable data for saving or loading '<see cref="SceneNode"/>' objects.
/// </summary>
[Serializable]
public sealed class SceneNodeData : ISceneElementData
{
	#region Properties

	/// <summary>
	/// The name of this node.
	/// </summary>
	public string Name { get; set; } = string.Empty;
	public SceneElementType ElementType => SceneElementType.SceneNode;
	public int ID { get; set; } = -1;
	/// <summary>
	/// Scene element ID of the parent node.
	/// </summary>
	public int ParentID { get; set; } = -1;

	/// <summary>
	/// Whether this node is enabled.
	/// </summary>
	public bool IsEnabled { get; set; } = true;
	/// <summary>
	/// The transformation of this node in its parent node's local space.
	/// </summary>
	public Pose LocalPose { get; set; } = Pose.Identity;

	/// <summary>
	/// The number of live components attached to this node.
	/// </summary>
	public int ComponentCount { get; set; } = 0;
	/// <summary>
	/// An array of serializable component data objects, representing live components attached to this node.
	/// The length of the array may not exceed <see cref="ComponentCount"/>.
	/// </summary>
	public ComponentData[]? ComponentData { get; set; } = null;

	#endregion
}
