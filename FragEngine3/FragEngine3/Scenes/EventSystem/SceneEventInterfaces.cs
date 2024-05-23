namespace FragEngine3.Scenes.EventSystem;

// GENERAL:

/// <summary>
/// Base interface for all components and behaviours that respond to scene events.<para/>
/// NOTE: This interface does nothing by itself and only indicates that more specific event listener interfaces are implemented on a type.
/// </summary>
public interface ISceneEventListener : IDisposable
{
	bool IsDisposed { get; }
}

/// <summary>
/// Base interface for all components and behaviours that respond to update events triggered by the main game loop.<para/>
/// NOTE: This interface does nothing by itself and only indicates that more specific update listener interfaces are implemented on a type.
/// </summary>
public interface ISceneUpdateListener : ISceneEventListener
{
	/// <summary>
	/// Gets a sorting key for delaying or prioritizing the execution of update events on listeners.
	/// This value is static and should be immutable at run-time.
	/// </summary>
	virtual int UpdateOrder { get => 0; }
}

// UPDATE / DRAW:

[SceneEventInterface(SceneEventType.EarlyUpdate)]
public interface IOnEarlyUpdateListener : ISceneUpdateListener
{
	bool OnEarlyUpdate();
}

[SceneEventInterface(SceneEventType.MainUpdate)]
public interface IOnMainUpdateListener : ISceneUpdateListener
{
	bool OnUpdate();				//TODO: Consider passing along a context object, with deltaTime and frame count ready to go?
}

[SceneEventInterface(SceneEventType.LateUpdate)]
public interface IOnLateUpdateListener : ISceneUpdateListener
{
	bool OnLateUpdate();
}

[SceneEventInterface(SceneEventType.FixedUpdate)]
public interface IOnFixedUpdateListener : ISceneUpdateListener
{
	bool OnFixedUpdate();
}

// NODE STATE:

[SceneEventInterface(SceneEventType.OnNodeDestroyed)]
public interface IOnNodeDestroyedListener : ISceneEventListener
{
	/// <summary>
	/// Event listener function for when the node associated with this behaviour is destroyed.
	/// If the behaviour is a component, it will be destroyed alongside its host node.<para/>
	/// EVENT: This event is triggered just before the node and its components and children are disposed.
	/// </summary>
	void OnNodeDestroyed();
}

[SceneEventInterface(SceneEventType.OnSetNodeEnabled)]
public interface IOnNodeSetEnabledListener : ISceneEventListener
{
	/// <summary>
	/// Event listener function for when the node associated with this behaviour is enabled or disabled.<para/>
	/// EVENT: This event is triggered whenever the <see cref="SceneNode.IsEnabled"/> state changes.
	/// </summary>
	/// <param name="_isEnabled">Whether the node is now enabled or disabled.</param>
	void OnNodeEnabled(bool _isEnabled);
}

[SceneEventInterface(SceneEventType.OnParentChanged)]
public interface IOnNodeParentChangedListener : ISceneEventListener
{
	/// <summary>
	/// Event listener function for when the parent of the node associated with this behaviour changes.<para/>
	/// EVENT: This event is triggered whenever the <see cref="SceneNode.Parent"/> value is replaced, and the node now resides at a different spot in the scene hierarchy.
	/// </summary>
	/// <param name="_newParent">The host node's new parent node.</param>
	void OnNodeParentChanged(SceneNode _newParent);
}

// COMPONENTS:

[SceneEventInterface(SceneEventType.OnComponentAdded)]
public interface IOnComponentAddedListener : ISceneEventListener
{
	/// <summary>
	/// Event listener function for when a new component is added to the node this behaviour is associated with.
	/// </summary>
	/// <param name="_newComponent">The new component that was added to the host node.</param>
	void OnComponentAdded(Component _newComponent);
}

[SceneEventInterface(SceneEventType.OnComponentRemoved)]
public interface IOnComponentRemovedListener : ISceneEventListener
{
	/// <summary>
	/// Event listener function for when an existing component is removed from the node this behaviour is associated with.<para/>
	/// WARNING: The removed node is likely disposed immediately after this event was called, so all references to it must be dropped.
	/// </summary>
	/// <param name="_removedComponent">The existing component that was removed from the host node.</param>
	void OnComponentRemoved(Component _removedComponent);
}
