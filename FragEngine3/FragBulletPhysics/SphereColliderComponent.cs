using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;

namespace FragBulletPhysics;

public sealed class SphereColliderComponent(SceneNode _node, PhysicsWorldComponent _world) : ColliderComponent(_node, _world)
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(SphereColliderComponent))]
	public sealed class Data
	{
		public required float Radius { get; init; }
	}

	#endregion
	#region Fields

	private float radius = 0.5f;

	#endregion
	#region Properties

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

	protected override bool InitializeCollisionShape(out CollisionShape _outCollisionShape)
	{
		try
		{
			_outCollisionShape = new SphereShape(radius);
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to initialize sphere collision shape!", ex);
			_outCollisionShape = null!;
			return false;
		}
	}

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
