using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Reflection;

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

		private static readonly SceneEventType[] emptyEventArray = Array.Empty<SceneEventType>();
		private static readonly Dictionary<string, Type> componentTypeRegistry = new(32);

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public SceneElementType ElementType => SceneElementType.Component;

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
		/// Load and initialize this component's data and states from data. This is used when loading a saved scene from file.
		/// </summary>
		/// <param name="_componentData">The data object describing the target states of this component. Must be non-null.</param>
		/// <param name="_idDataMap">A mapping of scene elements' IDs onto the data representing these elements.</param>
		/// <returns>True if the component was loaded and initialized from data, false if that fails.</returns>
		public abstract bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElementData> _idDataMap);
		/// <summary>
		/// Write this component's data and states to a data object. This is used to serialize the component to file when saving the scene.
		/// </summary>
		/// <param name="_componentData">Outputs a data object describing this component's states and data that needs to be persisted across saves. Outputs an empty data object on failure.</param>
		/// <param name="_idDataMap">A mapping of scene elements onto ID numbers used to represent them in serializable data.</param>
		/// <returns>True if the component's data was successfully written, false if that fails.</returns>
		public abstract bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap);

		/// <summary>
		/// Try creating a new instance of a given component type.<para/>
		/// NOTE: This is called internally by '<see cref="SceneNode.CreateComponent{T}(out T?)"/>', and you probably want to call that instead.
		/// </summary>
		/// <typeparam name="T">The type of component we wish to create. The component is assumed to have a constructor which takes an object of type '<see cref="SceneNode"/>' first,
		/// and any other parameters after.</typeparam>
		/// <param name="_node">The node that we wish to attach this component to. Must be non-null and non-disposed.<para/>
		/// NOTE: The component will not be added to the node's component list, so this has to be done manually via '<see cref="SceneNode.AddComponent(Component)"/>'.</param>
		/// <param name="_outComponent">Outputs the new component instance, or null, if the process fails.</param>
		/// <param name="_params">[Optional] A list of parameters to pass to the constructor. The first parameter is always expected to be the node, so that should be skipped in this array.
		/// Leave this null if no further parameters are required for this type.</param>
		/// <returns>True if a new component was created successfully, false otherwise.</returns>
		public static bool CreateComponent<T>(SceneNode _node, out T? _outComponent, params object[] _params) where T : Component
		{
			if (CreateComponent(_node, typeof(T), out Component? newComponent, _params) && newComponent != null)
			{
				_outComponent = newComponent as T;
				if (_outComponent == null)
				{
					Console.WriteLine($"Error! Type mismatch when trying to create component! Expected '{typeof(T)}', found '{newComponent.GetType()}'");
					newComponent.Dispose();
					return false;
				}
				return !_outComponent.IsDisposed;
			}
			_outComponent = null;
			return false;
		}
		public static bool CreateComponent(SceneNode _node, string _typeName, out Component? _outComponent, params object[] _params)
		{
			Type? type;
			try
			{
				type = Type.GetType(_typeName, false, false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to parse component type name '{_typeName}' for node '{_node.Name}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outComponent = null;
				return false;
			}

			if (type != null)
			{
				return CreateComponent(_node, type, out _outComponent, _params);
			}
			else
			{
				Console.WriteLine($"Error! Component type name '{_typeName}' could not be found!");
				_outComponent = null;
				return false;
			}
		}
		public static bool CreateComponent(SceneNode _node, Type _type, out Component? _outComponent, params object[] _params)
		{
			if (_node == null || _node.IsDisposed)
			{
				Console.WriteLine($"Error! Cannot create component for null or disposed node!");
				_outComponent = null;
				return false;
			}
			if (_type == null)
			{
				Console.WriteLine($"Error! Component type may not be null!");
				_outComponent = null;
				return false;
			}
			if (_type.IsPrimitive || _type.IsValueType || _type.IsInterface)
			{
				Console.WriteLine($"Error! Component type may not be a primitive, value type, or interface!");
				_outComponent = null;
				return false;
			}
			if (_type.IsAbstract)
			{
				Console.WriteLine($"Error! Cannot create instance of abstract component type '{_type}'!");
				_outComponent = null;
				return false;
			}

			// Prepare an array of all constructor parameters, lead by the node:
			int paramCount = _params != null ? _params.Length : 0;
			int argumentCount = 1 + paramCount;

			object[] arguments = new object[argumentCount];
			arguments[0] = _node;
			for (int i = 0; i < paramCount; ++i)
			{
				arguments[i + 1] = _params![i];
			}

			// Try creating a new component instance:
			try
			{
				object? instance = Activator.CreateInstance(_type, arguments);
				_outComponent = instance as Component;

				if (_outComponent == null && instance is IDisposable disp)
				{
					disp.Dispose();
				}
				return _outComponent != null && !_outComponent.IsDisposed;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create instance of component type '{_type}' for node '{_node.Name}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outComponent = null;
				return false;
			}
		}

		#endregion
	}
}
