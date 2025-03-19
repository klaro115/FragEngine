using FragEngine3.Scenes.SpatialTrees;
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
	public AABB(float _minX, float _minY, float _minZ, float _maxX, float _maxY, float _maxZ)
	{
		minimum = new(_minX, _minY, _minZ);
		maximum = new(_maxX, _maxY, _maxZ);
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

	/// <summary>
	/// Checks whether the bounding box contains a specific point in space.
	/// </summary>
	/// <param name="_position">Coordinates of the point.</param>
	/// <returns>True if the AABB contains the point, false otherwise.</returns>
	public readonly bool Contains(Vector3 _position)
	{
		return
			_position.X >= minimum.X && _position.X <= maximum.X &&
			_position.Y >= minimum.Y && _position.Y <= maximum.Y &&
			_position.Z >= minimum.Z && _position.Z <= maximum.Z;
	}

	/// <summary>
	/// Clamps a position to the extends of this bounding box.
	/// </summary>
	/// <param name="_position">Coordinates of the point.</param>
	/// <returns>The clamped coordinates.</returns>
	public readonly Vector3 ClampToBounds(Vector3 _position)
	{
		return Vector3.Clamp(_position, minimum, maximum);
	}

	/// <summary>
	/// Expands the bounding box to enclose all space contained within another bounding box.
	/// </summary>
	/// <param name="_other">Another bounding box.</param>
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

	/// <summary>
	/// Splits the bounding box into two partitions along a given axis. The split runs through the center point.
	/// </summary>
	/// <param name="_splitAxis">The axis along which the bounding box will be cut.<para/>
	/// Example: X=left/right, Y=bottom/top, Z=back/front.</param>
	/// <param name="_outPartitionA">Outputs the first partition, which is positioned at the lower value range along the split axis.</param>
	/// <param name="_outPartitionB">Outputs the second partition, which is positioned at the higher value range along the split axis.</param>
	/// <returns>True if the bounding box could be split along the given axis, false otherwise.</returns>
	public readonly bool Split(BspSplitAxis _splitAxis, out AABB _outPartitionA, out AABB _outPartitionB) => Split(Center, _splitAxis, out _outPartitionA, out _outPartitionB);

	/// <summary>
	/// Splits the bounding box into two partitions along a given axis, and centered around a specific point within its volume.
	/// </summary>
	/// <param name="_splitCenterPoint">The center point through which the split runs.</param>
	/// <param name="_splitAxis">The axis along which the bounding box will be cut.<para/>
	/// Example: X=left/right, Y=bottom/top, Z=back/front.</param>
	/// <param name="_outPartitionA">Outputs the first partition, which is positioned at the lower value range along the split axis.</param>
	/// <param name="_outPartitionB">Outputs the second partition, which is positioned at the higher value range along the split axis.</param>
	/// <returns>True if the bounding box could be split along the given axis and through the given point, false otherwise.</returns>
	public readonly bool Split(Vector3 _splitCenterPoint, BspSplitAxis _splitAxis, out AABB _outPartitionA, out AABB _outPartitionB)
	{
		if (!Contains(_splitCenterPoint))
		{
			_outPartitionA = this;
			_outPartitionB = new(maximum, maximum);
			return false;
		}

		switch (_splitAxis)
		{
			case BspSplitAxis.X:
				{
					_outPartitionA = new(
						minimum.X,
						minimum.Y,
						minimum.Z,
						_splitCenterPoint.X,
						maximum.Y,
						maximum.Z);
					_outPartitionB = new(
						_splitCenterPoint.X,
						minimum.Y,
						minimum.Z,
						maximum.X,
						maximum.Y,
						maximum.Z);
					return true;
				}
			case BspSplitAxis.Z:
				{
					_outPartitionA = new(
						minimum.X,
						minimum.Y,
						minimum.Z,
						maximum.X,
						_splitCenterPoint.Y,
						maximum.Z);
					_outPartitionB = new(
						minimum.X,
						_splitCenterPoint.Y,
						minimum.Z,
						maximum.X,
						maximum.Y,
						maximum.Z);
					return true;
				}
			case BspSplitAxis.Y:
				{
					_outPartitionA = new(
						minimum.X,
						minimum.Y,
						minimum.Z,
						maximum.X,
						maximum.Y,
						_splitCenterPoint.Z);
					_outPartitionB = new(
						minimum.X,
						minimum.Y,
						_splitCenterPoint.Z,
						maximum.X,
						maximum.Y,
						maximum.Z);
					return true;
				}
			default:
				_outPartitionA = this;
				_outPartitionB = new(maximum, maximum);
				return false;
		}
	}

	#endregion
}
