using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragBulletPhysics.ShapeComponents;

/// <summary>
/// Rigidbody physics component with a spherical shape.
/// </summary>
public sealed class SpherePhysicsComponent : PhysicsBodyComponent
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(SpherePhysicsComponent))]
	public sealed class Data
	{
		public required float Radius { get; init; }
	}

	#endregion
	#region Constructors

	public SpherePhysicsComponent(SceneNode _node, PhysicsWorldComponent _world, float _radius, float _mass, bool _isStatic) : base(_node, _world, _mass, _isStatic)
	{
		Radius = _radius;

		CollisionShape = new SphereShape(radius);
		LocalInertia = IsStatic ? Vector3.Zero : CollisionShape.CalculateLocalInertia(ActualMass);
		DefaultMotionState motionState = new(node.WorldTransformation.Matrix);
		using RigidBodyConstructionInfo rigidbodyInfo = new(ActualMass, motionState, CollisionShape, LocalInertia);
		Rigidbody = new(rigidbodyInfo);

		World.RegisterBody(this);
	}

	#endregion
	#region Fields

	private float radius = 0.5f;

	#endregion
	#region Properties

	public override PhysicsBodyShapeType ShapeType => PhysicsBodyShapeType.Sphere;

	/// <summary>
	/// Gets or sets the radius of the spherical collision shape.
	/// </summary>
	public float Radius
	{
		get => radius;
		set
		{
			radius = Math.Max(value, 0.001f);
			if (!IsDisposed && CollisionShape is SphereShape sphereShape)
			{
				sphereShape.SetUnscaledRadius(radius);
			}
		}
	}

	/// <summary>
	/// Gets or sets the diameter of the spherical collision shape.
	/// </summary>
	public float Diameter
	{
		get => radius * 2;
		set
		{
			radius = Math.Max(value / 2, 0.001f);
			if (!IsDisposed && CollisionShape is SphereShape sphereShape)
			{
				sphereShape.SetUnscaledRadius(radius);
			}
		}
	}

	#endregion
	#region Methods

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		if (!FragEngine3.Utility.Serialization.Serializer.DeserializeFromJson(_componentData.SerializedData, out Data? data))
		{
			return false;
		}

		Radius = data!.Radius;
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		Data data = new()
		{
			Radius = Radius,
		};

		if (!FragEngine3.Utility.Serialization.Serializer.SerializeToJson(data, out string jsonTxt))
		{
			_componentData = new ComponentData();
			return false;
		}

		_componentData = new ComponentData()
		{
			SerializedData = jsonTxt,
		};
		return true;
	}

	#endregion
}
