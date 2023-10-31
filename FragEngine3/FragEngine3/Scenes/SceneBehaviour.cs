using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes
{
	public abstract class SceneBehaviour : ISceneElement
	{
		#region Constructors

		protected SceneBehaviour(Scene _scene)
		{
			scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null!");
		}

		#endregion
		#region Fields

		public readonly Scene scene;

		private static readonly SceneEventType[] emptyEventArray = Array.Empty<SceneEventType>();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public SceneElementType ElementType => SceneElementType.SceneBehaviour;

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

		/// <summary>
		/// Receiver method for scene events that this behaviour is listening to.
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


		/// <summary>
		/// Try creating a new instance of a given scene-wide behaviour type.<para/>
		/// NOTE: This is called internally by '<see cref="Scene.CreateBehaviour{T}(out T?)"/>', and you probably want to call that instead.
		/// </summary>
		/// <typeparam name="T">The type of behaviour we wish to create. The behaviour is assumed to have a constructor which takes an object of type '<see cref="SceneNode"/>' first,
		/// and any other parameters after.</typeparam>
		/// <param name="_scene">The scene that we wish to attach this behaviour to. Must be non-null and non-disposed.<para/>
		/// NOTE: The behaviour will not be added to the node's behaviour list, so this has to be done manually via '<see cref="Scene.AddSceneBehaviour(SceneBehaviour)"/>'.</param>
		/// <param name="_outBehaviour">Outputs the new behaviour instance, or null, if the process fails.</param>
		/// <param name="_params">[Optional] A list of parameters to pass to the constructor. The first parameter is always expected to be the scene, so that should be skipped in this array.
		/// Leave this null if no further parameters are required for this type.</param>
		/// <returns>True if a new behaviour was created successfully, false otherwise.</returns>
		public static bool CreateBehaviour<T>(Scene _scene, out T? _outBehaviour, params object[] _params) where T : SceneBehaviour
		{
			if (CreateBehaviour(_scene, typeof(T), out SceneBehaviour? newBehaviour, _params) && newBehaviour != null)
			{
				_outBehaviour = newBehaviour as T;
				if (_outBehaviour == null)
				{
					Console.WriteLine($"Error! Type mismatch when trying to create scene behaviour! Expected '{typeof(T)}', found '{newBehaviour.GetType()}'");
					newBehaviour.Dispose();
					return false;
				}
				return !_outBehaviour.IsDisposed;
			}
			_outBehaviour = null;
			return false;
		}
		public static bool CreateBehaviour(Scene _scene, string _typeName, out SceneBehaviour? _outBehaviour, params object[] _params)
		{
			Type? type;
			try
			{
				type = Type.GetType(_typeName, false, false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to parse behaviour type name '{_typeName}' for scene '{_scene.Name}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outBehaviour = null;
				return false;
			}

			if (type != null)
			{
				return CreateBehaviour(_scene, type, out _outBehaviour, _params);
			}
			else
			{
				Console.WriteLine($"Error! Behaviour type name '{_typeName}' could not be found!");
				_outBehaviour = null;
				return false;
			}
		}
		public static bool CreateBehaviour(Scene _scene, Type _type, out SceneBehaviour? _outBehaviour, params object[] _params)
		{
			if (_scene == null || _scene.IsDisposed)
			{
				Console.WriteLine($"Error! Cannot create scene behaviour for null or disposed scene!");
				_outBehaviour = null;
				return false;
			}
			if (_type == null)
			{
				Console.WriteLine($"Error! Scene behaviour type may not be null!");
				_outBehaviour = null;
				return false;
			}
			if (_type.IsPrimitive || _type.IsValueType || _type.IsInterface)
			{
				Console.WriteLine($"Error! Scene behaviour type may not be a primitive, value type, or interface!");
				_outBehaviour = null;
				return false;
			}
			if (_type.IsAbstract)
			{
				Console.WriteLine($"Error! Cannot create instance of abstract scene behaviour type '{_type}'!");
				_outBehaviour = null;
				return false;
			}

			// Prepare an array of all constructor parameters, lead by the node:
			int paramCount = _params != null ? _params.Length : 0;
			int argumentCount = 1 + paramCount;

			object[] arguments = new object[argumentCount];
			arguments[0] = _scene;
			for (int i = 0; i < paramCount; ++i)
			{
				arguments[i + 1] = _params![i];
			}

			// Try creating a new behaviour instance:
			try
			{
				object? instance = Activator.CreateInstance(_type, arguments);
				_outBehaviour = instance as SceneBehaviour;

				if (_outBehaviour == null && instance is IDisposable disp)
				{
					disp.Dispose();
				}
				return _outBehaviour != null && !_outBehaviour.IsDisposed;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create instance of scene behaviour type '{_type}' for node '{_scene.Name}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outBehaviour = null;
				return false;
			}
		}

		#endregion
	}
}
