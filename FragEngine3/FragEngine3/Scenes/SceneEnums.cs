using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes
{
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

	/// <summary>
	/// Helper class with extension methods for dealing with the '<see cref="SceneUpdateStage"/>' enum.
	/// </summary>
	public static class SceneUpdateStageExt
	{
		#region Fields

		private static readonly Tuple<SceneUpdateStage, SceneEventType>[] eventTypeMap = new Tuple<SceneUpdateStage, SceneEventType>[]
		{
			new Tuple<SceneUpdateStage, SceneEventType>(SceneUpdateStage.Early, SceneEventType.OnEarlyUpdate),
			new Tuple<SceneUpdateStage, SceneEventType>(SceneUpdateStage.Main, SceneEventType.OnUpdate),
			new Tuple<SceneUpdateStage, SceneEventType>(SceneUpdateStage.Late, SceneEventType.OnLateUpdate),

			new Tuple<SceneUpdateStage, SceneEventType>(SceneUpdateStage.Fixed, SceneEventType.OnFixedUpdate),

			new Tuple<SceneUpdateStage, SceneEventType>(SceneUpdateStage.None, SceneEventType.None),
		};

		#endregion
		#region Methods

		/// <summary>
		/// Try to find an event type correspondingb to a given update stage.<para/>
		/// NOTE: This event will be fired and broadcast internally by the scene's update logic, and neither of these update events should be sent
		/// by user-side code, unless you know exactly what you're doing and what side effects may occur.
		/// </summary>
		/// <param name="_stage">The update stage for which we seek a corresponding event type.</param>
		/// <returns></returns>
		public static SceneEventType GetEventType(this SceneUpdateStage _stage)
		{
			foreach (var mapping in eventTypeMap)
			{
				if (mapping.Item1 == _stage) return mapping.Item2;
			}
			return SceneEventType.None;
		}

		#endregion
	}
}
