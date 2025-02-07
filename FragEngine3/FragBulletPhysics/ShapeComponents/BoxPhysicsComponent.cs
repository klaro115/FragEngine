using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragBulletPhysics.ShapeComponents;

/// <summary>
/// Rigidbody physics component with a rectangular shape.
/// </summary>
public sealed class BoxPhysicsComponent : PhysicsBodyComponent
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(BoxPhysicsComponent))]
	public sealed class Data
	{
		public required Vector3 Size { get; init; }
	}

	#endregion
	#region Constructors

	public BoxPhysicsComponent(SceneNode _node, PhysicsWorldComponent _world, Vector3 _size, float _mass, bool _isStatic) : base(_node, _world, _mass, _isStatic)
	{
		Size = _size;

		CollisionShape = new BoxShape(0.5f * size);
		LocalInertia = IsStatic ? Vector3.Zero : CollisionShape.CalculateLocalInertia(ActualMass);
		DefaultMotionState motionState = new(node.WorldTransformation.Matrix);
		using RigidBodyConstructionInfo rigidbodyInfo = new(ActualMass, motionState, CollisionShape, LocalInertia);
		Rigidbody = new(rigidbodyInfo);
		
		World.RegisterBody(this);
	}

	#endregion
	#region Fields

	private Vector3 size = Vector3.One;

	#endregion
	#region Properties

	public override PhysicsBodyShapeType ShapeType => PhysicsBodyShapeType.Box;

	/// <summary>
	/// Gets or sets the dimensions of the box collision shape.
	/// </summary>
	public Vector3 Size
	{
		get => size;
		set
		{
			Vector3 prevSize = size;
			size = new(
				Math.Max(value.X, 0.001f),
				Math.Max(value.Y, 0.001f),
				Math.Max(value.Z, 0.001f));

			if (!IsDisposed && size != prevSize)
			{
				BoxShape newShape = new(0.5f * size);
				CollisionShape prevShape = CollisionShape;
				if (!IsStatic)
				{
					LocalInertia = newShape.CalculateLocalInertia(Mass);
					Rigidbody.SetMassProps(Mass, LocalInertia);
				}
				Rigidbody.CollisionShape = newShape;
				prevShape.Dispose();
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

		Size = data!.Size;
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		Data data = new()
		{
			Size = Size,
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
