namespace FragEngine3.Scenes;

/// <summary>
/// The different update stages which are called once of more for each update cycle.
/// </summary>
[Flags]
public enum SceneUpdateStage : int
{
	/// <summary>
	/// Early update stage, executed before any other per-frame update logic. Initialization and input logic belong here.
	/// </summary>
	Early	= 1,
	/// <summary>
	/// Main update stage, executed once per frame. This is where most continuous update logic should happen.
	/// </summary>
	Main	= 2,
	/// <summary>
	/// Delayed update stage, executed after any other per-frame update logic and right before draw calls are issued.
	/// Camera movement and similar logic belong here.
	/// </summary>
	Late	= 4,

	/// <summary>
	/// Fixed-step update stage, executed once or more per frame and at fixed time intervals. This is reserved for physics-driven behaviour.
	/// </summary>
	Fixed	= 8,

	None	= 0,
	All		= Early | Main | Late | Fixed,
}

/// <summary>
/// Different types of elements and structures within a scene that need to be persisted and can be serialized for saving and loading.
/// </summary>
public enum SceneElementType
{
	/// <summary>
	/// An scene-wide behaviour type inheriting from '<see cref="Scenes.SceneBehaviour"/>'.
	/// </summary>
	SceneBehaviour,
	/// <summary>
	/// A node within the scene's hierarchy graph, of type '<see cref="Scenes.SceneNode"/>'.
	/// </summary>
	SceneNode,
	/// <summary>
	/// A component type attached to a scene node, inheriting from '<see cref="Scenes.Component"/>'.
	/// </summary>
	Component,
	/// <summary>
	/// A self-contained composition of scene nodes starting from a parent node and containg all nodes' components.
	/// Nodes will be of type '<see cref="Scenes.SceneNode"/>', components will inherit from '<see cref="Scenes.Component"/>'.
	/// </summary>
	SceneBranch,
	//...
}
