using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragBulletPhysics.ShapeComponents;

/// <summary>
/// Rigidbody physics component with a cylindrical shape.
/// </summary>
public sealed class CylinderPhysicsComponent : PhysicsBodyComponent
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(CylinderPhysicsComponent))]
	public sealed class Data : BaseData
	{
		public float Radius { get; set; }
		public float Height { get; set; }
	}

	#endregion
	#region Constructors

	public CylinderPhysicsComponent(SceneNode _node, PhysicsWorldComponent _world, float _radius, float _height, float _mass, bool _isStatic) : base(_node, _world, _mass, _isStatic)
	{
		radius = Math.Max(_radius, 0.001f);
		height = Math.Max(_height, 0.001f);

		CollisionShape = new CylinderShape(radius, radius, height * 0.5f);
		LocalInertia = IsStatic ? Vector3.Zero : CollisionShape.CalculateLocalInertia(ActualMass);
		DefaultMotionState motionState = new(node.WorldTransformation.ConvertHandedness());
		using RigidBodyConstructionInfo rigidbodyInfo = new(ActualMass, motionState, CollisionShape, LocalInertia);
		Rigidbody = new(rigidbodyInfo);

		World.RegisterBody(this);
	}

	#endregion
	#region Fields

	private float radius = 0.5f;
	private float height = 1.0f;

	#endregion
	#region Properties

	public override PhysicsBodyShapeType ShapeType => PhysicsBodyShapeType.Cylinder;

	/// <summary>
	/// Gets or sets the radius at cylindrical collision shape's base.
	/// </summary>
	public float Radius
	{
		get => radius;
		set
		{
			radius = Math.Max(value, 0.001f);
			if (!IsDisposed && CollisionShape is SphereShape sphereShape)
			{
				UpdateShape();
			}
		}
	}

	/// <summary>
	/// Gets or sets the diameter at cylindrical collision shape's base.
	/// </summary>
	public float Diameter
	{
		get => radius * 2;
		set => Radius = value / 2;
	}

	/// <summary>
	/// Gets or sets the height of the cylindrical collision shape's sides.
	/// </summary>
	public float Height
	{
		get => height;
		set
		{
			height = Math.Max(value, 0.001f);
			if (!IsDisposed && CollisionShape is CylinderShape cylinderShape)
			{
				UpdateShape();
			}
		}
	}

	#endregion
	#region Methods

	private void UpdateShape()
	{
		if (!IsDisposed)
		{
			CylinderShape newShape = new CylinderShape(radius, radius, height * 0.5f);
			CollisionShape prevShape = CollisionShape;
			if (!IsStatic)
			{
				LocalInertia = newShape.CalculateLocalInertia(Mass);
				Rigidbody.SetMassProps(Mass, LocalInertia);
			}
			Rigidbody.CollisionShape = newShape;
			prevShape.Dispose();

			UpdateMass();
		}
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		if (!FragEngine3.Utility.Serialization.Serializer.DeserializeFromJson(_componentData.SerializedData, out Data? data))
		{
			return false;
		}

		isStatic = data!.IsStatic;
		Mass = data.Mass;
		Radius = data.Radius;
		Height = data.Height;
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		Data data = new()
		{
			IsStatic = IsStatic,
			Mass = Mass,
			Radius = Radius,
			Height = Height,
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
