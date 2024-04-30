using FragEngine3.EngineCore;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes
{
	/// <summary>
	/// Abstract base type for scene-wide behaviours. Instances of this type may be attached to a scene, to provide
	/// overarching logic, or to handle the entire gameplay logic, in case you don't want to rely on components.
	/// </summary>
	/// <param name="_scene">The scene that this behaviour instance is attached to.</param>
	public abstract class SceneBehaviour(Scene _scene) : ISceneElement
	{
		#region Constructors

		~SceneBehaviour()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		/// <summary>
		/// The scene that this behaviour is attached to.
		/// </summary>
		public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null!");

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public SceneElementType ElementType => SceneElementType.SceneBehaviour;

		public Logger Logger => scene.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		protected virtual void Dispose(bool _disposing)
		{
			IsDisposed = true;

			// Do further disposal logic in child classes.
		}

		public virtual void Refresh() { }

		public virtual bool Draw() => true;

		/// <summary>
		/// Receiver method for scene events that this behaviour is listening to.
		/// </summary>
		/// <param name="_eventType">The type of the received event.</param>
		/// <param name="_stateObject">A state object containing additional data associated with the event.</param>
		public virtual void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData) { }

		/// <summary>
		/// Load and initialize this behaviour's data and states from data. This is used when loading a saved scene from file.
		/// </summary>
		/// <param name="_behaviourData">The data object describing the target states of this behaviour. Must be non-null.</param>
		/// <returns>True if the behaviour was loaded and initialized from data, false if that fails.</returns>
		public abstract bool LoadFromData(in SceneBehaviourData _behaviourData);
		/// <summary>
		/// Write this behaviour's data and states to a data object. This is used to serialize the behaviour to file when saving the scene.
		/// </summary>
		/// <param name="_behaviourData">Outputs a data object describing this behaviour's states and data that needs to be persisted across saves. Outputs an empty data object on failure.</param>
		/// <returns>True if the behaviour's data was successfully written, false if that fails.</returns>
		public abstract bool SaveToData(out SceneBehaviourData _behaviourData);

		#endregion
	}
}
