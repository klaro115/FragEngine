﻿using System.Numerics;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Scenes.Utility;

namespace FragEngine3.Scenes
{
    public sealed class SceneNode : ISceneElement
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

		// Local properties:
		private string name = string.Empty;
		private bool isEnabled = true;
		private Pose localPose;

		// Hierarchy:
		private SceneNode parentNode;
		private readonly List<SceneNode> children = new();

		// Components:
		private List<Component>? components = null;
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
		/// Gets whether this node is the root node of the scene. Root nodes will not have a parent node.
		/// </summary>
		public bool IsRootNode => !IsDisposed && scene.rootNode == this || parentNode == null;

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
		/// Gets the number of child nodes attached to this node.
		/// </summary>
		public int ChildCount => !IsDisposed ? children.Count : 0;
		/// <summary>
		/// Gets the number of components attached to this node.
		/// </summary>
		public int ComponentCount => !IsDisposed && components != null ? components.Count : 0;

		/// <summary>
		/// Gets the node's immediate parent node. May return null only if the node is disposed or if this is the scene's root node.
		/// </summary>
		public SceneNode Parent => !IsDisposed ? parentNode : null!;

		#endregion
		#region Properties Transformations

		public Pose LocalTransformation
		{
			get => localPose;
			set => localPose = value;
		}
		public Vector3 LocalPosition
		{
			get => localPose.position;
			set => localPose.position = value;
		}
		public Quaternion LocalRotation
		{
			get => localPose.rotation;
			set => localPose.rotation = value;
		}
		public Vector3 LocalScale
		{
			get => localPose.scale;
			set => localPose.scale = value;
		}

		public Pose WorldTransformation
		{
			get => GetWorldPose();
			set => localPose = parentNode != null ? parentNode.TransformWorldToLocal(value) : value;
		}
		public Vector3 WorldPosition
		{
			get => GetWorldPose().position;
			set => localPose.position = parentNode != null ? parentNode.TransformWorldToLocalPoint(value) : value;
		}
		public Quaternion WorldRotation
		{
			get => GetWorldRotation();
			set => localPose.rotation = parentNode != null ? parentNode.TransformWorldToLocal(value) : value;
		}
		public Vector3 WorldScale
		{
			get => GetWorldPose().scale;
			set => localPose.scale = parentNode != null ? parentNode.TransformWorldToLocalDirection(value) : value;
		}

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			if (eventManager != null)
			{
				if (_disposing)
				{
					eventManager.SendEvent(SceneEventType.OnNodeDestroyed);
				}
				eventManager.Destroy();
				eventManager = null;
			}

			IsDisposed = true;
			isEnabled = false;

			// Recursively purge all children first, to hopefully cut any upwards dependencies:
			for (int i = 0; i < ChildCount; i++)
			{
				children[i]?.Dispose();
			}
			if (_disposing) children.Clear();

			// Purge all components:
			for (int i = 0; i < ComponentCount; i++)
			{
				components![i]?.Dispose();
			}
			if (_disposing) components?.Clear();

			// Detach node from hierarchy:
			if (parentNode != null && !parentNode.IsDisposed)
			{
				parentNode.children.Remove(this);
			}
			parentNode = null!;
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
					components![i].Refresh();
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
			if (!IsDisposed && eventManager != null)
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
				Console.WriteLine("Error! Cannot duplicate disposed node!");
				_outDuplicate = null;
				return false;
			}
			if (_newParentNode != null && _newParentNode.IsDisposed)
			{
				Console.WriteLine("Error! Cannot duplicate node as child of disposed parent node!");
				_outDuplicate = null;
				return false;
			}
			_newParentNode ??= parentNode;

			// Save and then reload hierarchy branch starting from this node to create the duplicate:
			if (!SceneBranchSerializer.SaveBranchToData(this, out SceneBranchData data, out _, false))
			{
				Console.WriteLine("Error! Failed to copy node data for duplication!");
				_outDuplicate = null;
				return false;
			}
			if (!SceneBranchSerializer.LoadBranchFromData(_newParentNode, in data, out _outDuplicate, out _) || _outDuplicate == null)
			{
				Console.WriteLine("Error! Failed to paste node data for duplication!");
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
		#region Methods Hierarchy

		public SceneNode CreateChild(string? _name = null)
		{
			if (IsDisposed) throw new ObjectDisposedException(name, "Cannot add child to disposed node!");

			SceneNode newChild = new SceneNode(this, _name);
			newChild.SetParent(this);

			return newChild;
		}

		public bool DestroyChild(SceneNode _child)
		{
			if (_child == null) return false;
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
				throw new ObjectDisposedException(name, "Cannot change parent of disposed node!");
			if (IsRootNode)
			{
				Console.WriteLine("Error! Cannot change parent of a scene's root node!");
				return false;
			}
			if (_newParent?.scene != scene)
			{
				Console.WriteLine("Error! Parent node must belong to the same scene!");
				return false;
			}
			if (_newParent == this)
			{
				Console.WriteLine("Error! A node cannot be its own parent!");
				return false;
			}

			// If no parent is given, attach the node directly to the root node:
			_newParent ??= scene.rootNode;

			if (_newParent.IsDisposed)
				throw new ObjectDisposedException(nameof(_newParent), "Cannot add child to disposed node!");

			if (_newParent == parentNode)
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
		#region Methods Components

		public Component? GetComponent(int _index)
		{
			return _index >= 0 && _index < ComponentCount && !components![_index].IsDisposed ? components![_index] : null;
		}
		public bool GetComponent(int _index, out Component? _outComponent)
		{
			if (_index >= 0 && _index < ComponentCount)
			{
				_outComponent = components![_index];
				return !_outComponent.IsDisposed;
			}
			_outComponent = null;
			return false;
		}

		public T? GetComponent<T>() where T : Component
		{
			for (int i = 0; i < ComponentCount; ++i)
			{
				if (components![i] is T typedComponent && !typedComponent.IsDisposed)
				{
					return typedComponent;
				}
			}
			return null;
		}

		public bool FindComponent(Func<Component, bool> _funcSelector, out Component? _outComponent)
		{
			if (!IsDisposed)
			{
				for (int i = 0; i < ComponentCount; ++i)
				{
					Component component = components![i];
					if (!component.IsDisposed && _funcSelector(component))
					{
						_outComponent = component;
						return true;
					}
				}
			}
			_outComponent = null;
			return false;
		}

		/// <summary>
		/// Find all occurrances of a specific component type within the hierarchy of this node, using a depth-first recursive search.
		/// </summary>
		/// <typeparam name="T">The type of component we're looking for.</typeparam>
		/// <param name="_results">Target list for storing all search results in. The list will be cleared before any results are added to it.</param>
		/// <param name="_enabledOnly">Whether to only include components from nodes that are currently enabled.</param>
		public void GetComponentsInChildren<T>(List<T> _results, bool _enabledOnly = true) where T : Component
		{
			if (_results == null)
			{
				Console.WriteLine("Error! Cannot store components in results list that is null!");
				return;
			}

			_results.Clear();
			if (_enabledOnly && !IsEnabled) return;

			T? component;

			IEnumerator<SceneNode> e = IterateHierarchy(_enabledOnly);
			while (e.MoveNext())
			{
				component = e.Current.GetComponent<T>();
				if (component != null)
				{
					_results.Add(component);
				}
			}

			component = GetComponent<T>();
			if (component != null)
			{
				_results.Add(component);
			}
		}

		/// <summary>
		/// Find the first occurrance of a specific component type in this node's hierarchy.
		/// </summary>
		/// <typeparam name="T">The type of component we're looking for.</typeparam>
		/// <param name="_enabledOnly">Whether to only include components from nodes that are currently enabled.</param>
		/// <returns>The first instance thatg is found, or null.</returns>
		public T? GetComponentInChildren<T>(bool _enabledOnly = true) where T : Component
		{
			if (IsDisposed) return null;
			if (_enabledOnly && !isEnabled) return null;

			T? component = GetComponent<T>();
			if (component != null)
			{
				return component;
			}

			for (int i = 0; i < children.Count; ++i)
			{
				SceneNode child = children[i];
				if (!_enabledOnly || child.IsEnabled)
				{
					component = child.GetComponentInChildren<T>(_enabledOnly);
					if (component != null)
					{
						return component;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Check whether this node has a specific component instance attached to it.
		/// </summary>
		/// <param name="_component"></param>
		/// <returns></returns>
		public bool HasComponent(Component _component)
		{
			return !IsDisposed && ComponentCount != 0 && components!.Contains(_component);
		}

		/// <summary>
		/// Remove a component from this node. This will destroy and dispose the component instance, and all references to it are void.
		/// </summary>
		/// <param name="_component">The component we wish removed from this node.</param>
		/// <returns>True if the component belonged to this node and was removed, false otherwise.</returns>
		public bool RemoveComponent(Component _component)
		{
			if (_component == null)
			{
				Console.WriteLine("Error! Cannot remove null component from node!");
				return false;
			}
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot remove component from disposed node!");
				return false;
			}

			// Remove the component from this node:
			bool removed = components != null && components.Remove(_component);
			if (removed)
			{
				// Update event manager:
				if (eventManager != null)
				{
					eventManager.RemoveListenersFromComponent(_component);
					if (eventManager.TotalListenerCount == 0)
					{
						eventManager?.Destroy();
						eventManager = null;
					}
					else
					{
						eventManager.SendEvent(SceneEventType.OnDestroyComponent, _component);
					}
				}

				// Destroy the removed component:
				if (!_component.IsDisposed) _component.Dispose();
			}
			return removed;
		}

		/// <summary>
		/// Try to create a component of a specific type on this node.
		/// </summary>
		/// <typeparam name="T">The type of component we wish to create.</typeparam>
		/// <param name="_outNewComponent">Outputs the newly created component, or null, if creation failed.</param>
		/// <param name="_params">[Optional] An array of parameters to pass to the component's constructor. The first parameter is
		/// always assumed to be the scene node, so this array should skip and ommit the constructor's first parameter. Leave this
		/// null or empty, if no further parameters are needed beyond the scene node.</param>
		/// <returns>True if the component was created successfully, false otherwise.</returns>
		public bool CreateComponent<T>(out T? _outNewComponent, params object[] _params) where T : Component
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot create new component on disposed node!");
				_outNewComponent = null;
				return false;
			}
			if (!Component.CreateComponent(this, out _outNewComponent, _params) || _outNewComponent == null)
			{
				Console.WriteLine($"Error! Failed to create new component on node '{Name}'!");
				_outNewComponent?.Dispose();
				_outNewComponent = null;
				return false;
			}

			// Register the component's event listeners:
			RegisterComponentEvents(_outNewComponent);

			// Notify other components that a new one was added, and initialize the new component:
			eventManager?.SendEvent(SceneEventType.OnCreateComponent, _outNewComponent);

			return true;
		}

		/// <summary>
		/// Try to retrieve a component, or create a new one if none exists yet.
		/// </summary>
		/// <typeparam name="T">The type of component we wish to find or create.</typeparam>
		/// <param name="_outComponent">Outputs the component we're looking for, or null, if creation failed.</param>
		/// <returns>True if the component was found or created, false if creation failed.</returns>
		public bool GetOrCreateComponent<T>(out T? _outComponent) where T : Component
		{
			_outComponent = GetComponent<T>();
			if (_outComponent != null && !_outComponent.IsDisposed)
			{
				return true;
			}
			return CreateComponent(out _outComponent);
		}

		/// <summary>
		/// Adds a new component that was created externally to this node.<para/>
		/// NOTE: In most cases, using '<see cref="CreateComponent{T}(out T?, object[])"/>' should be used instead, unless creation of this component required some very exotic logic.
		/// </summary>
		/// <param name="_newComponent">A new node, that was created specifically for this node. May not be null.</param>
		/// <returns>True if the component was added, false otherwise.</returns>
		public bool AddComponent(Component _newComponent)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot add new component on disposed node!");
				return false;
			}
			if (_newComponent == null || _newComponent.IsDisposed)
			{
				Console.WriteLine($"Error! Cannot add null or disposed component to node '{Name}'!");
				return false;
			}
			if (_newComponent.node != this)
			{
				Console.WriteLine($"Error! Cannot add component '{_newComponent}' to node '{Name}', as it was created for a different node!");
				return false;
			}

			// Make sure components are initialized, then register component:
			if (components == null)
			{
				components = new() { _newComponent };
			}
			else
			{
				if (components.Contains(_newComponent))
				{
					Console.WriteLine("Error! Component has already been added to this node!");
					return false;
				}
				components.Add(_newComponent);
			}

			// Register the component's event listeners:
			RegisterComponentEvents(_newComponent);

			// Notify other components that a new one was added, and initialize the new component:
			eventManager?.SendEvent(SceneEventType.OnCreateComponent, _newComponent);

			return true;
		}

		private bool RegisterComponentEvents(Component _component)
		{
			if (_component == null || _component.IsDisposed) return false;

			if (eventManager != null)
			{
				// Update event manager's listeners:
				return eventManager.AddListenersFromComponent(_component);
			}
			else
			{
				// Create an event manager for relaying events to components:
				SceneEventType[] eventTypes = _component.GetSceneEventList();
				if (eventTypes != null && eventTypes.Length != 0)
				{
					eventManager = new(this);
				}
				return true;
			}
		}

		/// <summary>
		/// Gets an enumerator for iterating over all components on this node.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<Component> IterateComponents()
		{
			if (IsDisposed) yield break;

			for (int i = 0; i < ComponentCount; ++i)
			{
				if (!components![i].IsDisposed)
				{
					yield return components![i];
				}
			}
		}

		#endregion
		#region Methods Transformations

		public Pose TransformWorldToLocal(Pose _worldPose)
		{
			return GetWorldPose().InverseTransform(_worldPose);
		}

		public Vector3 TransformWorldToLocalPoint(Vector3 _worldPoint)
		{
			return GetWorldPose().InverseTransformPoint(_worldPoint);
		}
		public Vector3 TransformWorldToLocalDirection(Vector3 _worldDir)
		{
			return GetWorldPose().InverseTransformDirection(_worldDir);
		}
		public Quaternion TransformWorldToLocal(Quaternion _worldRot)
		{
			return Quaternion.Conjugate(WorldRotation) * _worldRot;
		}

		public Pose TransformLocalToWorld(Pose _localPose)
		{
			return GetWorldPose().Transform(_localPose);
		}

		public Vector3 TransformLocalToWorldPoint(Vector3 _localPoint)
		{
			return GetWorldPose().TransformPoint(_localPoint);
		}
		public Vector3 TransformLocalToWorldDirection(Vector3 _localDir)
		{
			return GetWorldRotation().Rotate(_localDir);
		}
		public Quaternion TransformLocalToWorld(Quaternion _localRot)
		{
			return GetWorldRotation() * _localRot;
		}

		/// <summary>
		/// Calculates the world space transformation of this node.
		/// </summary>
		/// <returns>A pose decribing the node's transformtion in world space.</returns>
		private Pose GetWorldPose()
		{
			Pose pose = LocalTransformation;
			SceneNode? parent = parentNode;
			while (parent != null)
			{
				pose = parent.LocalTransformation.Transform(pose);
				parent = parent.parentNode;
			}
			return pose;
		}
		/// <summary>
		/// Calculates the world space rotation of this node. Scale and tranlation are ignored completely.
		/// </summary>
		/// <returns>The node's rotation in world space.</returns>
		private Quaternion GetWorldRotation()
		{
			Quaternion rot = LocalRotation;
			SceneNode? parent = parentNode;
			while (parent != null)
			{
				rot = parent.LocalRotation * rot;
				parent = parent.parentNode;
			}
			return rot;
		}

		#endregion
	}
}