using FragEngine3.EngineCore;
using FragEngine3.Graphics;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Scenes.Utility;

namespace FragEngine3.Scenes;

public sealed partial class SceneNode : ISceneElement
{
	#region Constructors

	internal SceneNode(Scene _scene)
	{
		name = "root_node";

		scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null! This constructor should only be called to create a scene's root node!");
		parentNode = null!;
	}

	private SceneNode(SceneNode _parentNode, string? _name = null)
	{
		if (_parentNode == null)
			throw new ArgumentNullException(nameof(_parentNode), "Parent node may not be null!");
		if (_parentNode.IsDisposed)
			throw new ObjectDisposedException(nameof(_parentNode), "Parent node is disposed!");

		name = _name ?? $"child_node_{_parentNode.ChildCount + 1}";

		scene = _parentNode.scene;
		parentNode = _parentNode;
	}

	#endregion
	#region Fields

	public readonly Scene scene;

	private string name = string.Empty;
	private bool isEnabled = true;
	private SceneNodeEventManager? eventManager = null;

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets whether this node is enabled in the scene. If enabled, it is visible, its contents will be rendered, and components will be updated.
	/// If false, the node's logical and graphical components, as well as those of its children will not be executed.
	/// </summary>
	public bool IsEnabled
	{
		get => !IsDisposed && isEnabled;
		set => SetEnabled(value);
	}

	public bool IsDisposed { get; private set; } = false;

	/// <summary>
	/// Gets or sets the name of this node, may not be null.
	/// </summary>
	public string Name
	{
		get => name;
		set => name = value ?? string.Empty;
	}

	public SceneElementType ElementType => SceneElementType.SceneNode;

	/// <summary>
	/// Gets the engine's logging module for error and debug output.
	/// </summary>
	public Logger Logger => scene.engine.Logger ?? Logger.Instance!;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool _disposing)
	{
		if (eventManager is not null)
		{
			if (_disposing)
			{
				eventManager.SendEvent(SceneEventType.OnNodeDestroyed);
			}
			eventManager.Destroy();
			eventManager = null;
		}
		if (!IsDisposed && !scene.IsDisposed)
		{
			//scene.UnregisterNodeRenderers(this);			//TODO: Find a better way to replicate this through the SceneDrawManager!
		}

		IsDisposed = true;
		isEnabled = false;

		DisposeHierarchy(_disposing);
		DisposeComponents(_disposing);
	}

	/// <summary>
	/// Destroys the node, its children, and its components.<para/>
	/// NOTE: This method is merely a more intuitive synonym around '<see cref="Dispose()"/>'. Both do the same thing, and only one needs to be called for safely deleting a node.
	/// </summary>
	public void DestroyNode()
	{
		if (!IsDisposed) Dispose();
	}

	/// <summary>
	/// Go through all lists and resources held by this node, purge expired data, and reevaluate all states.
	/// </summary>
	public void Refresh()
	{
		if (IsDisposed) return;

		// Purge any expired children:
		children.RemoveAll(o => o == null || o.IsDisposed);
		
		// Purge any expired components:
		if (components != null)
		{
			components.RemoveAll(o => o == null || o.IsDisposed);
			if (ComponentCount == 0) components = null;
		}

		// Purge event manager if no live components remain:
		if (ComponentCount == 0)
		{
			eventManager?.Destroy();
			eventManager = null;
		}
		else
		{
			// Refresh components next:
			for (int i = 0; i < ComponentCount; ++i)
			{
				Component component = components![i];
				component.Refresh();

				if (component is IRenderer renderer)
				{
					scene.drawManager.UnregisterRenderer(renderer);
					scene.drawManager.RegisterRenderer(renderer);
				}
			}
			// Update event listeners and update stage flags:
			eventManager?.GetListenersFromComponents();
		}
	}

	/// <summary>
	/// Send an event to this node and all its components.
	/// </summary>
	/// <param name="_eventType">The type of event.</param>
	/// <param name="_eventData">Any data realated to or describing the event.</param>
	public void SendEvent(SceneEventType _eventType, object? _eventData)
	{
		if (!IsDisposed && eventManager is not null)
		{
			eventManager.SendEvent(_eventType, _eventData);
		}
	}

	/// <summary>
	/// Send an event to this node, all its components, and then forward it to all children.
	/// </summary>
	/// <param name="_eventType">The type of event.</param>
	/// <param name="_eventData">Any data realated to or describing the event.</param>
	/// <param name="_enabledOnly">Whether to only send this event to children that are enabled, including self.</param>
	public void BroadcastEvent(SceneEventType _eventType, object? _eventData, bool _enabledOnly)
	{
		if (_enabledOnly && !IsEnabled) return;

		// Send event locally:
		eventManager?.SendEvent(_eventType, _eventData);

		// Recursively forward the event to all (enabled) children:
		for (int i = 0; i < ChildCount; ++i)
		{
			if (!children[i].IsDisposed) children[i].BroadcastEvent(_eventType, _eventData, _enabledOnly);
		}
	}

	internal bool SaveToData(out SceneNodeData _outData)
	{
		_outData = new();
		if (IsDisposed) return false;

		_outData = new SceneNodeData()
		{
			Name = name,
			IsEnabled = isEnabled,
			LocalPose = localPose,
			//Note: components are written separately.
		};
		return true;
	}

	internal bool LoadFromData(in SceneNodeData _data)
	{
		if (IsDisposed) return false;

		Name = _data.Name;
		isEnabled = _data.IsEnabled;
		localPose = _data.LocalPose;
		//Note: components are loaded separately.

		return true;
	}

	/// <summary>
	/// Create an exact duplicate of this node and all of its children.<para/>
	/// NOTE: This uses save/load functionality via the '<see cref="SceneBranchSerializer"/>' to first serialize this node and then spawn a copy.
	/// </summary>
	/// <param name="_outDuplicate">Outputs a duplicate of this node, appended to the specified parent node. Null if duplication fails.</param>
	/// <param name="_newParentNode">[Optional] The parent node whose child the duplicate will become. If null, this node's parent is used instead.
	/// When duplicating a scene's root node, a new parent must be provided, since only one root node may exist in a scene at any given time.</param>
	/// <returns>True if the node was duplicated successfully, false otherwise.</returns>
	public bool DuplicateNode(out SceneNode? _outDuplicate, SceneNode? _newParentNode = null)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot duplicate disposed node!");
			_outDuplicate = null;
			return false;
		}
		if (_newParentNode != null && _newParentNode.IsDisposed)
		{
			Logger.LogError("Cannot duplicate node as child of disposed parent node!");
			_outDuplicate = null;
			return false;
		}
		_newParentNode ??= parentNode;

		// Save and then reload hierarchy branch starting from this node to create the duplicate:
		if (!SceneBranchSerializer.SaveBranchToData(this, out SceneBranchData data, out _, false))
		{
			Logger.LogError("Failed to copy node data for duplication!");
			_outDuplicate = null;
			return false;
		}
		if (!SceneBranchSerializer.LoadBranchFromData(_newParentNode, in data, out _outDuplicate, out _) || _outDuplicate == null)
		{
			Logger.LogError("Failed to paste node data for duplication!");
			return false;
		}

		return true;
	}

	#endregion
	#region Methods State

	public void SetEnabled(bool _enable)
	{
		if (_enable == isEnabled) return;

		isEnabled = _enable;

		// Use the local event system to tell components that behaviours will be enabled/disabled:
		if (ComponentCount != 0)
		{
			eventManager?.SendEvent(SceneEventType.OnSetNodeEnabled, isEnabled);
		}
		// Notify all children that they might be enabled or disabled by association:
		if (ChildCount != 0)
		{
			NotifyEnabledInHierarchy(IsEnabledInHierarchy());
		}
	}

	/// <summary>
	/// Recursively check if this node and all of its parents are enabled.
	/// </summary>
	/// <returns>True if if the node and its parents are enabled, false otherwise.</returns>
	public bool IsEnabledInHierarchy()
	{
		if (IsRootNode) return isEnabled;
		return isEnabled && parentNode.IsEnabledInHierarchy();
	}

	private void NotifyEnabledInHierarchy(bool _parentEnabledInHierarchy)
	{
		if (IsEnabled)
		{
			// Raise local event to tell components that behaviours will be enabled/disabled:
			bool ownState = _parentEnabledInHierarchy && isEnabled;
			if (ComponentCount != 0)
			{
				eventManager?.SendEvent(SceneEventType.OnSetNodeEnabled, ownState);
			}
			// Forward the notification to all children:
			for (int i = 0; i < ChildCount; i++)
			{
				children[i].NotifyEnabledInHierarchy(ownState);
			}
		}
	}

	#endregion
}
