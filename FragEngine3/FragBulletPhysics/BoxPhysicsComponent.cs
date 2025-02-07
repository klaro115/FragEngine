using BulletSharp;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using System.Numerics;

namespace FragBulletPhysics;

public sealed class BoxPhysicsComponent(SceneNode _node, PhysicsWorldComponent _world) : PhysicsBodyComponent(_node, _world)
{
	#region Types

	[Serializable]
	[ComponentDataType(typeof(BoxPhysicsComponent))]
	public sealed class Data
	{
		public required Vector3 Size { get; init; }
	}

	#endregion
	#region Fields

	private Vector3 size = Vector3.One;

	#endregion
	#region Properties

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
				ReinitializeBody();
			}
		}
	}

	#endregion
	#region Methods

	protected override bool InitializeCollisionShape(out CollisionShape _outCollisionShape)
	{
		try
		{
			_outCollisionShape = new BoxShape(0.5f * size);
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to initialize box collision shape!", ex);
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
