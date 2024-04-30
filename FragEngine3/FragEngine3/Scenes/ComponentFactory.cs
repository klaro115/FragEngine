using FragEngine3.EngineCore;
using FragEngine3.Scenes.Data;

namespace FragEngine3.Scenes;

public static class ComponentFactory
{
	#region Methods

	/// <summary>
	/// Try creating a new instance of a given component type.<para/>
	/// NOTE: This is called internally by '<see cref="SceneNode.CreateComponent{T}(out T?)"/>', and you probably want to call that instead.
	/// </summary>
	/// <typeparam name="T">The type of component we wish to create. The component is assumed to have a constructor which takes an object of type '<see cref="SceneNode"/>' first,
	/// and any other parameters after.</typeparam>
	/// <param name="_node">The node that we wish to attach this component to. Must be non-null and non-disposed.<para/>
	/// NOTE: The component will not be added to the node's component list, so this has to be done manually via '<see cref="SceneNode.AddComponent(Component)"/>'. The method
	/// '<see cref="SceneNode.CreateComponent{T}(out T?, object[])"/>' handles both creation and registration automatically and should be your go-to for common components.</param>
	/// <param name="_outComponent">Outputs the new component instance, or null, if the process fails.</param>
	/// <param name="_params">[Optional] A list of parameters to pass to the constructor. The first parameter is always expected to be the node, so that should be skipped in this array.
	/// Leave this parameter null if no further parameters are required for this type.</param>
	/// <returns>True if a new component was created successfully, false otherwise.</returns>
	public static bool CreateComponent<T>(SceneNode _node, out T? _outComponent, params object[] _params) where T : Component
	{
		if (CreateComponent(_node, typeof(T), out Component? newComponent, _params) && newComponent != null)
		{
			_outComponent = newComponent as T;
			if (_outComponent == null)
			{
				_node.Logger.LogError($"Type mismatch when trying to create component! Expected '{typeof(T)}', found '{newComponent.GetType()}'");
				newComponent.Dispose();
				return false;
			}
			return !_outComponent.IsDisposed;
		}
		_outComponent = null;
		return false;
	}

	/// <summary>
	/// Try creating a new instance of a given component type.<para/>
	/// NOTE: In most cases, you will probably want to call '<see cref="SceneNode.CreateComponent{T}(out T?)"/>' instead.
	/// </summary>
	/// <param name="_node">The node that we wish to attach this component to. Must be non-null and non-disposed.<para/>
	/// NOTE: The component will not be added to the node's component list, so this has to be done manually via '<see cref="SceneNode.AddComponent(Component)"/>'.</param>
	/// <param name="_typeName">The full name (with namespaces) of the type which you want to create an instance of. May not be null or blank.</param>
	/// <param name="_outComponent">Outputs the new component instance, or null, if the process fails.</param>
	/// <param name="_params">[Optional] A list of parameters to pass to the constructor. The first parameter is always expected to be the node, so that should be skipped in this array.
	/// Leave this parameter null if no further parameters are required for this type.</param>
	/// <returns>True if a new component was created successfully, false otherwise.</returns>
	public static bool CreateComponent(SceneNode _node, string _typeName, out Component? _outComponent, params object[] _params)
	{
		if (string.IsNullOrEmpty(_typeName))
		{
			_node?.Logger.LogError("Cannot create component using null or blank type name!");
			_outComponent = null;
			return false;
		}

		Type? type;
		try
		{
			type = Type.GetType(_typeName, false, false);
		}
		catch (Exception ex)
		{
			_node.Logger.LogException($"Failed to parse component type name '{_typeName}' for node '{_node.Name}'!", ex);
			_outComponent = null;
			return false;
		}

		if (type != null)
		{
			return CreateComponent(_node, type, out _outComponent, _params);
		}
		else
		{
			_node?.Logger.LogError($"Component type name '{_typeName}' could not be found!");
			_outComponent = null;
			return false;
		}
	}

	/// <summary>
	/// Try creating a new instance of a given component type.<para/>
	/// NOTE: This is called internally by '<see cref="SceneNode.CreateComponent{T}(out T?)"/>', and you probably want to call that instead.
	/// </summary>
	/// <param name="_node">The node that we wish to attach this component to. Must be non-null and non-disposed.<para/>
	/// NOTE: The component will not be added to the node's component list, so this has to be done manually via '<see cref="SceneNode.AddComponent(Component)"/>'. The method
	/// '<see cref="SceneNode.CreateComponent{T}(out T?, object[])"/>' handles both creation and registration automatically and should be your go-to for common components.</param>
	/// <param name="_type">The type of component we wish to create. The component is assumed to have a constructor which takes an object of type '<see cref="SceneNode"/>' first,
	/// and any other parameters after.</param>
	/// <param name="_outComponent">Outputs the new component instance, or null, if the process fails.</param>
	/// <param name="_params">[Optional] A list of parameters to pass to the constructor. The first parameter is always expected to be the node, so that should be skipped in this array.
	/// Leave this parameter null if no further parameters are required for this type.</param>
	/// <returns>True if a new component was created successfully, false otherwise.</returns>
	public static bool CreateComponent(SceneNode _node, Type _type, out Component? _outComponent, params object[] _params)
	{
		if (_node == null || _node.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create component for null or disposed node!");
			_outComponent = null;
			return false;
		}
		if (_type == null)
		{
			_node.Logger.LogError("Component type may not be null!");
			_outComponent = null;
			return false;
		}
		if (_type.IsPrimitive || _type.IsValueType || _type.IsInterface)
		{
			_node.Logger.LogError($"Component type may not be a primitive, value type, or interface! Found: '{_type}'");
			_outComponent = null;
			return false;
		}
		if (_type.IsAbstract)
		{
			_node.Logger.LogError($"Cannot create instance of abstract component type '{_type}'!");
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
			_node.Logger.LogException($"Failed to create instance of component type '{_type}' for node '{_node.Name}'!", ex);
			_outComponent = null;
			return false;
		}
	}

	/// <summary>
	/// Create an exact duplicate of an existing component.
	/// </summary>
	/// <param name="_component">The component we wish to copy, may not be null or disposed.</param>
	/// <param name="_pasteOnNode">The node we wish to paste the copied component to. If null, the node of the copied component is used instead, thus giving it a second component of the same type.</param>
	/// <param name="_outDuplicate">Outputs the duplicated component instance, or null, if the copy-paste process failed.</param>
	/// <returns>Trtue if the component was copied and pasted successfully, false otherwise.</returns>
	public static bool DuplicateComponent(Component _component, SceneNode? _pasteOnNode, out Component? _outDuplicate)
	{
		if (_component == null || _component.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot duplicate null or disposed component!");
			_outDuplicate = null;
			return false;
		}

		_pasteOnNode ??= _component.node;
		if (_pasteOnNode.IsDisposed)
		{
			_component.Logger.LogError("Cannot paste duplicated component on disposed scene node!");
			_outDuplicate = null;
			return false;
		}

		// Try gathering all scene dependencies and references held by the component:
		Dictionary<ISceneElement, int> object2IdMap = [];
		Dictionary<int, ISceneElement> id2ObjectMap = [];
		int idCounter = 0;
		IEnumerator<ISceneElement> e = _component.IterateSceneDependencies();
		while (e.MoveNext())
		{
			object2IdMap.Add(e.Current, idCounter);
			id2ObjectMap.Add(idCounter++, e.Current);
		}

		// Save component data:
		if (!_component.SaveToData(out ComponentData copiedData, in object2IdMap))
		{
			_component.Logger.LogError("Failed to copy component !");
			_outDuplicate = null;
			return false;
		}

		// Create a new blank component instance on the given target node:
		if (!CreateComponent(_pasteOnNode, _component.GetType(), out _outDuplicate) || _outDuplicate == null)
		{
			_component.Logger.LogError($"Failed to create duplicate instance of component type '{_component.GetType()}'!");
			_outDuplicate = null;
			return false;
		}

		// Load copied component data on the blank instance:
		if (!_outDuplicate.LoadFromData(in copiedData, in id2ObjectMap))
		{
			_component.Logger.LogError($"Failed to paste copied data to duplicate component of type '{_component.GetType()}'!");
			_pasteOnNode.RemoveComponent(_outDuplicate);
			_outDuplicate.Dispose();
			_outDuplicate = null;
			return false;
		}

		return true;
	}

	#endregion
}
