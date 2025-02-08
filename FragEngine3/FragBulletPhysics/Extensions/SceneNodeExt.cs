using FragBulletPhysics.ShapeComponents;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using System.Numerics;

namespace FragBulletPhysics.Extensions;

/// <summary>
/// Helper class with extension methods for adding physics to <see cref="SceneNode"/> objects.
/// </summary>
public static class SceneNodeExt
{
	#region Methods

	public static bool CreatePhysicsBodyComponent(this SceneNode _node, out PhysicsBodyComponent? _outComponent, PhysicsBodyShapeType _shapeType, Vector4 _dimensions, float _mass = 1.0f, bool _isStatic = true)
	{
		// Check parameters:
		if (_node is null || _node.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create phyiscs body component to null or disposed node!");
			_outComponent = null;
			return false;
		}
		if (float.IsNaN(_mass) || _mass < 0)
		{
			Logger.Instance?.LogError("Cannot create phyiscs body component with invalid mass!");
			_outComponent = null;
			return false;
		}

		// Create components depending on shape type:
		PhysicsBodyComponent newComponent;
		switch (_shapeType)
		{
			case PhysicsBodyShapeType.Sphere:
				{
					float radius = _dimensions.X;
					newComponent = new SpherePhysicsComponent(_node, null!, radius, _mass, _isStatic);
				}
				break;
			case PhysicsBodyShapeType.Box:
				{
					Vector3 size = new(_dimensions.X, _dimensions.Y, _dimensions.Z);
					newComponent = new BoxPhysicsComponent(_node, null!, size, _mass, _isStatic);
				}
				break;
			//...
			default:
				{
					Logger.Instance?.LogError($"Cannot prepare constructor parameters for physics body shape type '{_shapeType}'!");
					_outComponent = null;
					return false;
				}
		}

		// Attach the newly created component to the node:
		if (!_node.AddComponent(newComponent!))
		{
			newComponent?.Dispose();
			_outComponent = null;
			return false;
		}

		_outComponent = newComponent;
		return true;
	}

	public static bool CreatePhysicsBodyComponent<T>(this SceneNode _node, out T? _outComponent, Vector4 _dimensions, float _mass = 1.0f, bool _isStatic = true) where T : PhysicsBodyComponent
	{
		// Check parameters:
		if (_node is null || _node.IsDisposed)
		{
			Logger.Instance?.LogError($"Cannot create component '{typeof(T).Name}' to null or disposed node!");
			_outComponent = null;
			return false;
		}
		if (float.IsNaN(_mass) || _mass < 0)
		{
			Logger.Instance?.LogError($"Cannot create component '{typeof(T).Name}' with invalid mass!");
			_outComponent = null;
			return false;
		}

		// Create components depending on generic type:
		PhysicsBodyComponent newComponent;
		if (typeof(T) == typeof(SpherePhysicsComponent))
		{
			float radius = _dimensions.X;
			newComponent = new SpherePhysicsComponent(_node, null!, radius, _mass, _isStatic);
		}
		else if (typeof(T) == typeof(BoxPhysicsComponent))
		{
			Vector3 size = new(_dimensions.X, _dimensions.Y, _dimensions.Z);
			newComponent = new BoxPhysicsComponent(_node, null!, size, _mass, _isStatic);
		}
		//...
		else
		{
			Logger.Instance?.LogError($"Cannot prepare constructor parameters for physics component type '{typeof(T).Name}'!");
			_outComponent = null;
			return false;
		}

		// Attach the newly created component to the node:
		if (!_node.AddComponent(newComponent!))
		{
			newComponent?.Dispose();
			_outComponent = null;
			return false;
		}

		_outComponent = newComponent as T;
		return true;
	}

	public static bool GetOrCreatePhysicsBodyComponent(this SceneNode _node, out PhysicsBodyComponent? _outComponent, PhysicsBodyShapeType _shapeType, Vector4 _dimensions, float _mass = 1.0f, bool _isStatic = true)
	{
		if (_node is null || _node.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot get or create phyiscs body component on null or disposed node!");
			_outComponent = null;
			return false;
		}

		if (_node.GetComponent(out _outComponent) && _outComponent is not null && !_outComponent.IsDisposed && _outComponent.ShapeType == _shapeType)
		{
			return true;
		}
		return CreatePhysicsBodyComponent(_node, out _outComponent, _shapeType, _dimensions, _mass, _isStatic);
	}

	public static bool GetOrCreatePhysicsBodyComponent<T>(this SceneNode _node, out T? _outComponent, Vector4 _dimensions, float _mass = 1.0f, bool _isStatic = true) where T : PhysicsBodyComponent
	{
		if (_node is null || _node.IsDisposed)
		{
			Logger.Instance?.LogError($"Cannot get or create component '{typeof(T).Name}' on null or disposed node!");
			_outComponent = null;
			return false;
		}

		if (_node.GetComponent(out _outComponent) && _outComponent is not null && !_outComponent.IsDisposed)
		{
			return true;
		}
		return CreatePhysicsBodyComponent(_node, out _outComponent, _dimensions, _mass, _isStatic);
	}

	#endregion
}
