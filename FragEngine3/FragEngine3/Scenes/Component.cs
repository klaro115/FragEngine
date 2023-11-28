using FragEngine3.EngineCore;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes
{
	public abstract class Component : ISceneElement
	{
		#region Constructors

		protected Component(SceneNode _node)
		{
			node = _node ?? throw new ArgumentNullException(nameof(_node), "Node may not be null!");
		}
		~Component()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly SceneNode node;

		private static readonly SceneEventType[] emptyEventArray = [];

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public SceneElementType ElementType => SceneElementType.Component;

		/// <summary>
		/// Gets the engine's logging module for error and debug output.
		/// </summary>
		public Logger Logger => node.scene.engine.Logger ?? Logger.Instance!;

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

			if (_disposing)
			{
				node.RemoveComponent(this);
			}
		}
		public void Destroy()
		{
			if (!IsDisposed) Dispose();
		}

		public virtual void Refresh() { }

		/// <summary>
		/// Receiver method for scene events that this component is listening to.
		/// </summary>
		/// <param name="_eventType">The type of the received event.</param>
		/// <param name="_stateObject">A state object containing additional data associated with the event.</param>
		public virtual void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData) { }
		/// <summary>
		/// Returns an array of event types this behaviour will listen to.
		/// </summary>
		/// <returns>An array of event type, or an empty array if not listening to any events.</returns>
		public virtual SceneEventType[] GetSceneEventList() => emptyEventArray;

		/// <summary>
		/// Gets an enumerator for iterating over all scene dependencies of this component, starting with itself, the node it is attached to, and that node's parent.
		/// </summary>
		public virtual IEnumerator<ISceneElement> IterateSceneDependencies()
		{
			yield return this;
			yield return node;
			if (node.Parent != null)
			{
				yield return node.Parent;
			}
		}

		/// <summary>
		/// Load and initialize this component's data and states from data. This is used when loading a saved scene from file.
		/// </summary>
		/// <param name="_componentData">The data object describing the target states of this component. Must be non-null.</param>
		/// <param name="_idDataMap">A mapping of scene elements' IDs onto the corresponding object instances.</param>
		/// <returns>True if the component was loaded and initialized from data, false if that fails.</returns>
		public abstract bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap);
		/// <summary>
		/// Write this component's data and states to a data object. This is used to serialize the component to file when saving the scene.
		/// </summary>
		/// <param name="_componentData">Outputs a data object describing this component's states and data that needs to be persisted across saves. Outputs an empty data object on failure.</param>
		/// <param name="_idDataMap">A mapping of scene elements onto ID numbers used to represent them in serializable data.</param>
		/// <returns>True if the component's data was successfully written, false if that fails.</returns>
		public abstract bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap);

		#endregion
	}
}
