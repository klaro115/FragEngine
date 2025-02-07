using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes;

public sealed partial class SceneNode
{
	#region Fields

	private SceneNode parentNode;
	private readonly List<SceneNode> children = [];

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this node is the root node of the scene. Root nodes will not have a parent node.
	/// </summary>
	public bool IsRootNode => !IsDisposed && scene.rootNode == this || parentNode == null;

	/// <summary>
	/// Gets the number of child nodes attached to this node.
	/// </summary>
	public int ChildCount => !IsDisposed ? children.Count : 0;

	/// <summary>
	/// Gets the node's immediate parent node. May return null only if the node is disposed or if this is the scene's root node.
	/// </summary>
	public SceneNode Parent => !IsDisposed ? parentNode : null!;

	#endregion
	#region Methods

	private void DisposeHierarchy(bool _disposing)
	{
		// Recursively purge all children first, to hopefully cut any upwards dependencies:
		for (int i = 0; i < ChildCount; i++)
		{
			children[i]?.Dispose();
		}
		if (_disposing) children.Clear();

		// Detach node from hierarchy:
		if (parentNode != null && !parentNode.IsDisposed)
		{
			parentNode.children.Remove(this);
		}
		parentNode = null!;
	}

	public SceneNode CreateChild(string? _name = null)
	{
		if (IsDisposed) throw new ObjectDisposedException(name, "Cannot add child to disposed node!");

		SceneNode newChild = new(this, _name);
		newChild.SetParent(this);

		return newChild;
	}

	public bool DestroyChild(SceneNode _child)
	{
		if (_child is null) return false;
		if (!children.Contains(_child)) return false;

		bool removed = children.Remove(_child);
		if (removed)
		{
			if (!_child.IsDisposed)
			{
				_child.eventManager?.SendEvent(SceneEventType.OnNodeDestroyed);
			}
			_child.Dispose();
		}
		return removed;
	}

	public bool GetChild(int _index, out SceneNode? _outChild)
	{
		if (_index >= 0 && _index < ChildCount)
		{
			_outChild = children[_index];
			return !_outChild.IsDisposed;
		}
		_outChild = null;
		return false;
	}

	/// <summary>
	/// Attach this node to a different parent node.
	/// </summary>
	/// <param name="_newParent">The new parent node this node should be a child of. Must be different from calling node, may not be disposed.
	/// Make sure that reattaching the node to this parent will never result in any cyclical dependencies in the hierarchy graph. If null, the
	/// node will be attached to the scene's root node instead.</param>
	/// <returns>True if the node was reattached to the given parent, false otherwise.</returns>
	/// <exception cref="ObjectDisposedException">This node and the new parent node ay not be disposed.</exception>
	public bool SetParent(SceneNode? _newParent)
	{
		if (IsDisposed)
		{
			throw new ObjectDisposedException(name, "Cannot change parent of disposed node!");
		}
		if (IsRootNode)
		{
			Logger.LogError("Cannot change parent of a scene's root node!");
			return false;
		}
		if (_newParent?.scene != scene)
		{
			Logger.LogError("Parent node must belong to the same scene!");
			return false;
		}
		if (_newParent == this)
		{
			Logger.LogError("A node cannot be its own parent!");
			return false;
		}

		// If no parent is given, attach the node directly to the root node:
		_newParent ??= scene.rootNode;

		if (_newParent.IsDisposed)
			throw new ObjectDisposedException(nameof(_newParent), "Cannot add child to disposed node!");

		if (_newParent == parentNode && parentNode.children.Contains(this))
		{
			return true;
		}

		// Convert current transformation to new parent's local space:
		Pose worldPose = WorldTransformation;
		LocalTransformation = _newParent.TransformWorldToLocal(worldPose);

		// Update hierarchy graph:
		SceneNode oldParent = parentNode;
		oldParent.children.Remove(this);
		if (!_newParent.children.Contains(this))
		{
			_newParent.children.Add(this);
		}
		parentNode = _newParent;

		// Send an event, telling components that the hierarchy has changed:
		eventManager?.SendEvent(SceneEventType.OnParentChanged, _newParent);

		return true;
	}

	/// <summary>
	/// Gets an enumerator for iterating over all children of this node.
	/// </summary>
	/// <param name="_enabledOnly">Whether to only iterate over enabled nodes. If true, only child nodes whose '<see cref="IsEnabled"/>' is true are returned.
	/// If false, all nodes all returned.</param>
	/// <returns>An enumerator of child nodes.</returns>
	public IEnumerator<SceneNode> IterateChildren(bool _enabledOnly = false)
	{
		if (IsDisposed) yield break;
		for (int i = 0; i < children.Count; i++)
		{
			SceneNode child = children[i];
			if (!child.IsDisposed && (!_enabledOnly || child.IsEnabled))
			{
				yield return child;
			}
		}
	}

	/// <summary>
	/// Gets an enumerator for iterating over all nodes within the hierarchy of this node, including itself. The hierarchy is traversed using a depth-first recursive search.
	/// </summary>
	/// <param name="_enabledOnly">Whether to only iterate over enabled nodes. If true, only child nodes whose '<see cref="IsEnabled"/>' is true are returned.
	/// If false, all nodes all returned.</param>
	/// <returns>An enumerator of nested child nodes.</returns>
	public IEnumerator<SceneNode> IterateHierarchy(bool _enabledOnly = false)
	{
		if (IsDisposed) yield break;
		if (_enabledOnly && !isEnabled) yield break;

		for (int i = 0; i < children.Count; i++)
		{
			SceneNode child = children[i];
			if (!child.IsDisposed && (!_enabledOnly || child.IsEnabled))
			{
				if (child.ChildCount != 0)
				{
					IEnumerator<SceneNode> e = child.IterateHierarchy(_enabledOnly);
					while (e.MoveNext())
					{
						yield return e.Current;
					}
				}

				yield return child;
			}
		}
		yield return this;
	}

	#endregion
}
