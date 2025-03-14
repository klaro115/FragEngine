using System.Numerics;

namespace FragEngine3.Scenes;

/// <summary>
/// An axis-aligned bounding box.
/// </summary>
public struct AABB
{
	#region Constructors

	public AABB()
	{
		minimum = Vector3.Zero;
		maximum = Vector3.Zero;
	}
	public AABB(Vector3 _minimum, Vector3 _maximum)
	{
		minimum = _minimum;
		maximum = _maximum;
	}
	public AABB(IList<Vector3> _positions)
	{
		if (_positions is null || _positions.Count == 0)
		{
			minimum = Vector3.Zero;
			maximum = Vector3.Zero;
			return;
		}

		minimum = new Vector3(1.0e+8f, 1.0e+8f, 1.0e+8f);
		maximum = new Vector3(-1.0e+8f, -1.0e+8f, -1.0e+8f);
		foreach (Vector3 position in _positions)
		{
			minimum = Vector3.Min(position, minimum);
			maximum = Vector3.Max(position, maximum);
		}
	}

	#endregion
	#region Fields

	/// <summary>
	/// The bottom-rear-left corner of the bounding box volume.
	/// </summary>
	public Vector3 minimum;
	/// <summary>
	/// The top-front-right corner of the bounding box volume.
	/// </summary>
	public Vector3 maximum;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the center point of the bounding box.
	/// </summary>
	public readonly Vector3 Center => 0.5f * (minimum + maximum);
	/// <summary>
	/// Gets the dimensions of the bounding box.
	/// </summary>
	public readonly Vector3 Size => maximum - minimum;
	/// <summary>
	/// Gets the volume occupied by the bounding box.
	/// </summary>
	public readonly float Volume
	{
		get
		{
			Vector3 size = Size;
			return size.X * size.Y * size.Z;
		}
	}

	/// <summary>
	/// Gets a bounding box with all-zero values.
	/// </summary>
	public static AABB Zero => new(Vector3.Zero, Vector3.Zero);
	/// <summary>
	/// Gets a bounding box whose minima lie at the coordinate origin, and with all-one side lengths.
	/// </summary>
	public static AABB One => new(Vector3.Zero, Vector3.One);

	#endregion
	#region Methods

	public readonly bool Contains(Vector3 _position)
	{
		return
			_position.X >= minimum.X && _position.X <= maximum.X &&
			_position.Y >= minimum.Y && _position.Y <= maximum.Y &&
			_position.Z >= minimum.Z && _position.Z <= maximum.Z;
	}

	public void Expand(in AABB _other)
	{
		minimum = Vector3.Min(_other.minimum, minimum);
		maximum = Vector3.Max(_other.maximum, maximum);
	}

	public readonly bool Overlaps(in AABB _other)
	{
		//TODO
		throw new NotImplementedException();
	}

	public readonly bool GetOverlapRegion(in AABB _other, out AABB _overlapRegion)
	{
		//TODO
		throw new NotImplementedException();
	}

	#endregion
}
