using System.Numerics;

namespace FragEngine3.Scenes.SpatialTrees;

/// <summary>
/// An "unpartitioned" spatial tree. This is essentially a fancy wrapper around a list, and serves to stand in for those rare
/// situations where an API requires a spatial tree, but actual spatial partitioning would just introduce unnecessary overhead.
/// If your scene contains less than 10-ish simple objects, this is the way to go.
/// </summary>
/// <param name="_initialCapacity">The initial number of objects to allocate in the internal list.</param>
public sealed class UnpartitionedTree<T>(int _initialCapacity = UnpartitionedTree<T>.defaultObjectCapacity) : ISpatialTree<T> where T : ISpatialTreeObject
{
	#region Fields

	private readonly List<T> objects = new(_initialCapacity);

	#endregion
	#region Constants

	public const int defaultObjectCapacity = 64;

	#endregion
	#region Properties

	public bool IsRootBranch => true;

	public AABB PartitionBounds => new(Vector3.One * -1.0e+8f, Vector3.One * 1.0e+8f);
	public AABB ContentBounds { get; private set; } = AABB.Zero;

	/// <summary>
	/// Gets the total number of objects in the structure.
	/// </summary>
	public int ObjectCount => objects.Count;

	/// <summary>
	/// Gets an object contained in this structure by its index position.<para/>
	/// Warning: This will not perform any boundary checks!
	/// </summary>
	/// <param name="_index">The index of the object.</param>
	public T this[int _index] => objects[_index];
	
	#endregion
	#region Methods

	public void Clear(bool _discardSubBranches = false)
	{
		ContentBounds = new(PartitionBounds.minimum, PartitionBounds.minimum);
		objects.Clear();
	}

	public bool AddObject(T _newObject)
	{
		if (_newObject is null)
		{
			return false;
		}

		AABB bounds = _newObject.CalculateAxisAlignedBoundingBox();
		ContentBounds.Expand(bounds);

		objects.Add(_newObject);
		return true;
	}

	public bool RemoveObject(T _object)
	{
		if (_object is null)
		{
			return false;
		}

		bool removed = objects.Remove(_object);
		return removed;
	}

	public void RecalculateContentBounds(bool _recursive = true)
	{
		if (ObjectCount == 0)
		{
			ContentBounds = new(PartitionBounds.minimum, PartitionBounds.minimum);
			return;
		}

		ContentBounds = objects[0].CalculateAxisAlignedBoundingBox();

		for (int i = 1; i < objects.Count; i++)
		{
			AABB bounds = objects[i].CalculateAxisAlignedBoundingBox();
			ContentBounds.Expand(bounds);
		}
	}

	public void GetAllObjects(List<T> _dstAllObjects) => _dstAllObjects.AddRange(objects);

	public IEnumerator<T> EnumerateAllObjects() => objects.GetEnumerator();

	public void GetObjectsInBounds(in AABB _boundingBox, List<T> _dstObjects)
	{
		foreach (T obj in objects)
		{
			AABB bounds = obj.CalculateAxisAlignedBoundingBox();
			if (_boundingBox.Overlaps(in bounds))
			{
				_dstObjects.Add(obj);
			}
		}
	}

	#endregion
}
