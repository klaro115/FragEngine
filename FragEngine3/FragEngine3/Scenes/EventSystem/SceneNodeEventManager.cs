
using System.ComponentModel;

namespace FragEngine3.Scenes.EventSystem
{
	public sealed class SceneNodeEventManager
	{
		#region Constructors

		public SceneNodeEventManager(SceneNode _node)
		{
			node = _node;
			GetListenersFromComponents();
		}

		#endregion
		#region Fields

		public readonly SceneNode node;

		public readonly Dictionary<SceneEventType, List<ComponentEventListener>> eventListenerMap = new();

		#endregion
		#region Properties

		/// <summary>
		/// Gets the number of event type that have active listeners.
		/// </summary>
		public int EventTypeCount { get; private set; } = 0;
		/// <summary>
		/// Gets the total number of active listeners across all event types.
		/// </summary>
		public int TotalListenerCount { get; private set; } = 0;

		/// <summary>
		/// Gets all update stages that this node has listeners for.
		/// </summary>
		public SceneUpdateStage UpdateStageFlags { get; private set; } = 0;

		#endregion
		#region Methods

		public void Destroy()
		{
			if (UpdateStageFlags != 0 && !node.scene.IsDisposed)
			{
				node.scene.UnregisterNodeFromUpdateStages(node);
			}
		}

		/// <summary>
		/// Update list of event listeners from the node's components.
		/// </summary>
		/// <returns>True if events could be retrieved successfully, false otherwise.</returns>
		public bool GetListenersFromComponents()
		{
			// Purge existing event listeners:
			foreach (var kvp in eventListenerMap)
			{
				kvp.Value.Clear();
			}

			if (node.IsDisposed)
			{
				node.Logger.LogError("Cannot update event listeners for disposed node!");
				return false;
			}

			// Iterate over all components on the node:
			TotalListenerCount = 0;
			for (int i = 0; i < node.ComponentCount; i++)
			{
				Component? component = node.GetComponent(i);
				if (component != null && !component.IsDisposed)
				{
					// Register listeners for all events, mapped by event type:
					SceneEventType[] eventTypes = component.GetSceneEventList();
					foreach (SceneEventType eventType in eventTypes)
					{
						if (!eventListenerMap.TryGetValue(eventType, out List<ComponentEventListener>? listeners))
						{
							listeners = new();
							eventListenerMap.Add(eventType, listeners);
						}
						listeners.Add(new(component, component.ReceiveSceneEvent));
						TotalListenerCount++;
					}
				}
			}

			SceneUpdateStage prevUpdateStageFlags = UpdateStageFlags;
			UpdateStageFlags = 0;

			// Count the number of event types that have active listeners:
			EventTypeCount = 0;
			if (TotalListenerCount != 0)
			{
				foreach (var kvp in eventListenerMap)
				{
					if (kvp.Value.Count != 0)
					{
						EventTypeCount++;

						// Raise update stage flag if listeners were found for an update event:
						if (kvp.Key.IsUpdateStage())
						{
							UpdateStageFlags |= kvp.Key.GetUpdateStage();
						}
					}
					// Remove update stage flag if no listeners remain for an update event:
					else if (kvp.Key.IsUpdateStage())
					{
						UpdateStageFlags &= ~kvp.Key.GetUpdateStage();
					}
				}
			}

			// If no listeners were found, free up some memory:
			if (TotalListenerCount == 0)
			{
				eventListenerMap.Clear();
			}

			// Do a clean re-registration of all update stages this node listens to:
			if (prevUpdateStageFlags != 0)
			{
				node.scene.UnregisterNodeFromUpdateStages(node);
			}
			if (UpdateStageFlags != 0)
			{
				node.scene.RegisterNodeForUpdateStages(node, UpdateStageFlags);
			}
			return true;
		}

		/// <summary>
		/// Remove all event listeners belonging to a specific component.
		/// </summary>
		/// <param name="_component">The component whose event listeners need to be purged.</param>
		/// <returns>True if any listeners were found and removed, false otherwise.</returns>
		public bool RemoveListenersFromComponent(Component _component)
		{
			if (_component == null)
			{
				node.Logger.LogError("Cannot remove listeners of null component!");
				return false;
			}

			int removed = 0;
			TotalListenerCount = 0;
			EventTypeCount = 0;
			SceneUpdateStage prevUpdateStageFlags = UpdateStageFlags;

			foreach (var kvp in eventListenerMap)
			{
				removed += kvp.Value.RemoveAll(o => o.target.IsDisposed || o.target == _component);
				TotalListenerCount += kvp.Value.Count;
				if (kvp.Value.Count != 0)
				{
					EventTypeCount++;
				}
				// Remove update stage flag if no listeners remain for an update event:
				else if (kvp.Key.IsUpdateStage())
				{
					UpdateStageFlags &= ~kvp.Key.GetUpdateStage();
				}
			}

			// If no listeners remain, free up some memory:
			if (TotalListenerCount == 0)
			{
				eventListenerMap.Clear();
			}

			// Update registration of node's update stage listeners:
			node.scene.UpdateNodeUpdateStages(node, prevUpdateStageFlags, UpdateStageFlags);
			return removed != 0;
		}

		public bool AddListenersFromComponent(Component _component)
		{
			if (_component == null || _component.IsDisposed)
			{
				node.Logger.LogError("Cannot add listeners of null or disposed component!");
				return false;
			}

			bool added = false;
			SceneUpdateStage prevUpdateStageFlags = UpdateStageFlags;

			// Register listeners for all events, mapped by event type:
			SceneEventType[] eventTypes = _component.GetSceneEventList();
			foreach (SceneEventType eventType in eventTypes)
			{
				if (!eventListenerMap.TryGetValue(eventType, out List<ComponentEventListener>? listeners))
				{
					listeners = new();
					eventListenerMap.Add(eventType, listeners);
				}
				else if (listeners.Any(o => o.target == _component))
				{
					continue;
				}

				listeners.Add(new(_component, _component.ReceiveSceneEvent));
				if (listeners.Count == 1)
				{
					EventTypeCount++;

					// Raise update stage flag if an update event gained first listener:
					if (eventType.IsUpdateStage())
					{
						UpdateStageFlags |= eventType.GetUpdateStage();
					}
				}
				TotalListenerCount++;
				added = true;
			}

			// Update registration of node's update stage listeners:
			node.scene.UpdateNodeUpdateStages(node, prevUpdateStageFlags, UpdateStageFlags);
			return added;
		}

		public void RefreshUpdateStageListeners()
		{
			SceneUpdateStage prevUpdateStageFlags = UpdateStageFlags;
			UpdateStageFlags = 0;

			for (int i = 0; i < 4; ++i)
			{
				SceneEventType updateEventType = (SceneEventType)((int)SceneEventType.OnEarlyUpdate + i);
				if (eventListenerMap.TryGetValue(updateEventType, out List<ComponentEventListener>? listeners) && listeners.Count != 0)
				{
					UpdateStageFlags |= updateEventType.GetUpdateStage();
				}
			}

			// Do a clean re-registration of all update stages this node listens to:
			if (prevUpdateStageFlags != 0)
			{
				node.scene.UnregisterNodeFromUpdateStages(node);
			}
			if (UpdateStageFlags != 0)
			{
				node.scene.RegisterNodeForUpdateStages(node, UpdateStageFlags);
			}
		}

		/// <summary>
		/// Send an event that will be relayed to any components that are registered and listening for this event type.
		/// </summary>
		/// <param name="_eventType">The type of event that is being sent.</param>
		/// <param name="_eventData">Any additional data pertaining to or describing the event. Null if no data is needed or expected.</param>
		public void SendEvent(SceneEventType _eventType, object? _eventData = null)
		{
			if (eventListenerMap.TryGetValue(_eventType, out List<ComponentEventListener>? listeners))
			{
				foreach (ComponentEventListener listener in listeners)
				{
					if (listener.IsValid) listener.funcReceiver(_eventType, _eventData);
				}
			}
		}

		#endregion
	}
}
