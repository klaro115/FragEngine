using FragEngine3.EngineCore;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes;

public abstract class Component(SceneNode _node) : ISceneElement
{

	#region Constructors
	~Component()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly SceneNode node = _node ?? throw new ArgumentNullException(nameof(_node), "Node may not be null!");

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
	[Obsolete("Replaced by event listener interfaces")]
	public virtual void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData) { }

	/// <summary>
	/// Gets an enumerator for iterating over all non-update scene event types that this component is listening to.<para/>
	/// NOTE: The base implementation will check if the component implements any of the known listener interfaces; for more efficient event
	/// registration, components should override this method and directly return all event types whose interfaces they actually implement.
	/// </summary>
	public virtual IEnumerator<SceneEventType> EnumerateEventsListenedTo()
	{
		if (this is not ISceneEventListener) yield break;

		// Node Events:
		if (this is IOnNodeDestroyedListener) yield return SceneEventType.OnNodeDestroyed;
		if (this is IOnNodeSetEnabledListener) yield return SceneEventType.OnSetNodeEnabled;
		if (this is IOnNodeParentChangedListener) yield return SceneEventType.OnParentChanged;

		// Component Events:
		if (this is IOnComponentAddedListener) yield return SceneEventType.OnComponentAdded;
		if (this is IOnComponentRemovedListener) yield return SceneEventType.OnComponentRemoved;
		//...
	}

	/// <summary>
	/// Gets an enumerator for iterating over all scene dependencies of this component, starting with itself, the node it is attached to, and that node's parent.<para/>
	/// Note: This is used internally to resolve dependencies when duplicating the component.
	/// </summary>
	public virtual IEnumerator<ISceneElement> IterateSceneDependencies()
	{
		yield return this;
		yield return node;
		if (node.Parent is not null)
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
