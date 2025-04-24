using System.Collections;
using System.Numerics;

namespace FragEngine3.Scenes.SpatialTrees;

/// <summary>
/// An "unpartitioned" spatial tree. This is essentially a fancy wrapper around a list, and serves to stand in for those rare
/// situations where an API requires a spatial tree, but actual spatial partitioning would just introduce unnecessary overhead.
/// If your scene contains less than 10-ish simple objects, this is the way to go.
/// </summary>
public sealed class UnpartitionedTree<T> : ISpatialTree<T> where T : ISpatialTreeObject
{
	#region Constructors

	/// <summary>
	/// Creates a new unpartitioned spatial tree.
	/// </summary>
	/// <param name="_initialCapacity">The initial number of objects to allocate in the internal list.</param>
	public UnpartitionedTree(int _initialCapacity = defaultObjectCapacity)
	{
		objects = new(_initialCapacity);
		emptyListOnClear = true;
	}
	/// <summary>
	/// Creates a new unpartitioned spatial tree.
	/// </summary>
	/// <param name="_objectsList">An existing list to use as internal object storage.</param>
	/// <param name="_emptyListOnClear">Whether to remove all elements from the list when <see cref="Clear(bool)"/> is called.</param>
	public UnpartitionedTree(List<T> _objectsList, bool _emptyListOnClear = true)
	{
		objects = _objectsList;
		emptyListOnClear = _emptyListOnClear;
	}

	#endregion
	#region Fields

	private readonly List<T> objects;
	private readonly bool emptyListOnClear;

	#endregion
	#region Constants

	public const int defaultObjectCapacity = 64;

	#endregion
	#region Properties

	public bool IsRootBranch => true;

	public AABB PartitionBounds => new(Vector3.One * -1.0e+8f, Vector3.One * 1.0e+8f);
	public AABB ContentBounds { get; private set; } = AABB.Zero;

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
		if (emptyListOnClear)
		{
			objects.Clear();
		}
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

	public IEnumerator<T> GetEnumerator() => objects.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => objects.GetEnumerator();

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
