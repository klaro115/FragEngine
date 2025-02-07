using FragEngine3.Graphics;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes;

public sealed partial class SceneNode
{
	#region Fields

	// Components:
	private List<Component>? components = null;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the number of components attached to this node.
	/// </summary>
	public int ComponentCount => !IsDisposed && components != null ? components.Count : 0;

	#endregion
	#region Methods

	private void DisposeComponents(bool _disposing)
	{
		// Purge all components:
		for (int i = 0; i < ComponentCount; i++)
		{
			components![i]?.Dispose();
		}
		if (_disposing) components?.Clear();
	}

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

	public bool GetComponent<T>(out T? _outComponent) where T : Component
	{
		for (int i = 0; i < ComponentCount; ++i)
		{
			if (components![i] is T typedComponent && !typedComponent.IsDisposed)
			{
				_outComponent = typedComponent;
				return true;
			}
		}
		_outComponent = null;
		return false;
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
			Logger.LogError("Cannot store components in results list that is null!");
			return;
		}

		_results.Clear();
		if (_enabledOnly && !IsEnabled) return;

		T? component;

		IEnumerator<SceneNode> e = IterateHierarchy(_enabledOnly);
		while (e.MoveNext())
		{
			component = e.Current.GetComponent<T>();
			if (component is not null)
			{
				_results.Add(component);
			}
		}

		component = GetComponent<T>();
		if (component is not null)
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
		if (component is not null)
		{
			return component;
		}

		for (int i = 0; i < children.Count; ++i)
		{
			SceneNode child = children[i];
			if (!_enabledOnly || child.IsEnabled)
			{
				component = child.GetComponentInChildren<T>(_enabledOnly);
				if (component is not null)
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
		return ComponentCount != 0 && components!.Contains(_component);
	}

	/// <summary>
	/// Remove a component from this node. This will destroy and dispose the component instance, and all references to it are void.
	/// </summary>
	/// <param name="_component">The component we wish removed from this node.</param>
	/// <returns>True if the component belonged to this node and was removed, false otherwise.</returns>
	public bool RemoveComponent(Component _component)
	{
		if (_component is null)
		{
			Logger.LogError("Cannot remove null component from node!");
			return false;
		}
		if (IsDisposed)
		{
			Logger.LogError("Cannot remove component from disposed node!");
			return false;
		}

		// Remove the component from this node:
		bool removed = components is not null && components.Remove(_component);
		if (removed)
		{
			// Send removal event to all components:
			eventManager?.SendEvent(SceneEventType.OnComponentRemoved, _component);

			// Update renderer list:
			if (_component is IRenderer renderer)
			{
				scene.drawManager.UnregisterRenderer(renderer);
			}

			if (_component is ISceneEventListener)
			{
				// Update update stage lists:
				if (_component is ISceneUpdateListener updateListener)
				{
					scene.updateManager.UnregisterSceneElements(updateListener);
				}

				// Update event manager:
				if (eventManager is not null)
				{
					eventManager.RemoveListenersFromComponent(_component);
					if (eventManager.TotalListenerCount == 0)
					{
						eventManager?.Destroy();
						eventManager = null;
					}
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
			Logger.LogError("Cannot create new component on disposed node!");
			_outNewComponent = null;
			return false;
		}
		if (!ComponentFactory.CreateComponent(this, out _outNewComponent, _params) || _outNewComponent is null)
		{
			Logger.LogError($"Failed to create new component on node '{Name}'!");
			_outNewComponent?.Dispose();
			_outNewComponent = null;
			return false;
		}

		if (components is null)
		{
			components = [_outNewComponent];
		}
		else
		{
			components.Add(_outNewComponent);
		}

		// Register the component's event listeners:
		RegisterComponentEvents(_outNewComponent);

		// Notify other components that a new one was added, and initialize the new component:
		eventManager?.SendEvent(SceneEventType.OnComponentAdded, _outNewComponent);

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
		if (_outComponent is not null && !_outComponent.IsDisposed)
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
			Logger.LogError("Cannot add new component on disposed node!");
			return false;
		}
		if (_newComponent is null || _newComponent.IsDisposed)
		{
			Logger.LogError($"Cannot add null or disposed component to node '{Name}'!");
			return false;
		}
		if (_newComponent.node != this)
		{
			Logger.LogError($"Cannot add component '{_newComponent}' to node '{Name}', as it was created for a different node!");
			return false;
		}

		// Make sure components are initialized, then register component:
		if (components is null)
		{
			components = [_newComponent];
		}
		else
		{
			if (components.Contains(_newComponent))
			{
				Logger.LogError("Component has already been added to this node!");
				return false;
			}
			components.Add(_newComponent);
		}

		// Register the component's event listeners:
		RegisterComponentEvents(_newComponent);

		// Notify other components that a new one was added, and initialize the new component:
		eventManager?.SendEvent(SceneEventType.OnComponentAdded, _newComponent);

		return true;
	}

	private bool RegisterComponentEvents(Component _component)
	{
		if (_component is null || _component.IsDisposed) return false;

		bool success = true;

		// Update renderer list:
		if (_component is IRenderer renderer)
		{
			success &= scene.drawManager.RegisterRenderer(renderer);
		}

		// Update update stage lists:
		if (_component is ISceneEventListener)
		{
			if (_component is ISceneUpdateListener updateListener)
			{
				success &= scene.updateManager.RegisterSceneElement(updateListener);
			}

			if (eventManager != null)
			{
				// Update event manager's listeners:
				success &= eventManager.AddListenersFromComponent(_component);
			}
			else
			{
				// Create an event manager for relaying events to components:
				eventManager = new(this);
			}
		}

		return success;
	}

	/// <summary>
	/// Gets an enumerator for iterating over all components on this node.
	/// </summary>
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

	/// <summary>
	/// Gets an enumerator for iterating over all renderer type components on this node.
	/// </summary>
	public IEnumerator<IRenderer> IterateRenderers()
	{
		if (IsDisposed) yield break;

		for (int i = 0; i < ComponentCount; ++i)
		{
			if (!components![i].IsDisposed && components[i] is IRenderer renderer)
			{
				yield return renderer;
			}
		}
	}

	#endregion
}
