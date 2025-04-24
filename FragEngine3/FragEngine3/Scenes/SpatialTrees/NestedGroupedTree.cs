using FragEngine3.EngineCore;
using System.Collections;

namespace FragEngine3.Scenes.SpatialTrees;

/// <summary>
/// A spatial partitioning structure that is based on pre-defined groups. Each group can have its own
/// sub-tree, and groups are identified by an ID. Objects are automatically assigned to a group based
/// on the value of their '<see cref="T.SpatialPartitionGroupID"/>'. Any objects that
/// have an unknown ID or whose ID is zero will be assigned to a global fallback Octree partition instead.<para/>
/// Note: This partitioning scheme was added mainly for semantic partitioning around nested static
/// structures in the world. Groups could be individual buildings in a city, with each buildings having
/// its own sub-tree using whichever partitioning scheme is most efficient for its room layout.
/// </summary>
public sealed class NestedGroupedTree<T> : ISpatialTree<T> where T : ISpatialTreeObject
{
	#region Constructors

	/// <summary>
	/// Creates a new nested group-based partitioning tree.
	/// </summary>
	/// <param name="initialGroupCapacity">Number of groups for which capacity should be allocated initially.</param>
	/// <param name="_initialCapacity">Number of objects for which capacity should be allocated for the fallback octree.</param>
	public NestedGroupedTree(int initialGroupCapacity = defaultGroupCapacity, int _initialCapacity = defaultObjectCapacity)
	{
		groups = new(initialGroupCapacity);
		fallback = new(0u, _initialCapacity);

		ContentBounds = fallback.ContentBounds;
	}

	#endregion
	#region Fields

	private readonly Dictionary<uint, ISpatialTree<T>> groups;
	private readonly OctreeBranch<T> fallback;

	#endregion
	#region Constants

	public const int defaultGroupCapacity = 32;
	public const int defaultObjectCapacity = 64;

	#endregion
	#region Properties

	public bool IsRootBranch => true;

	public AABB PartitionBounds => fallback.PartitionBounds;
	/// <summary>
	/// Gets a bounding box enclosing all objects on this branch.
	/// </summary>
	public AABB ContentBounds { get; private set; } = AABB.Zero;

	/// <summary>
	/// Gets the total number of groups in the structure.
	/// </summary>
	public int GroupCount => groups.Count;

	public int ObjectCount { get; private set; } = 0;

	#endregion
	#region Methods

	public void Clear(bool _discardSubBranches = false)
	{
		ContentBounds = new(PartitionBounds.minimum, PartitionBounds.minimum);
		ObjectCount = 0;

		foreach (ISpatialTree<T> group in groups.Values)
		{
			group.Clear(_discardSubBranches);
		}

		if (_discardSubBranches)
		{
			groups.Clear();
		}
	}

	public bool AddObject(T _newObject)
	{
		if (_newObject is null)
		{
			return false;
		}

		if (groups.TryGetValue(_newObject.SpatialPartitionGroupID, out ISpatialTree<T>? group))
		{
			if (group.AddObject(_newObject))
			{
				ObjectCount++;
				return true;
			}
		}

		bool added = fallback.AddObject(_newObject);
		if (added)
		{
			ContentBounds.Expand(_newObject.CalculateAxisAlignedBoundingBox());
			ObjectCount++;
		}
		return added;
	}

	public bool RemoveObject(T _object)
	{
		if (_object is null)
		{
			return false;
		}

		if (groups.TryGetValue(_object.SpatialPartitionGroupID, out ISpatialTree<T>? group))
		{
			if (group.RemoveObject(_object))
			{
				ObjectCount--;
				return true;
			}
		}
		
		bool removed = fallback.RemoveObject(_object);
		if (removed)
		{
			ObjectCount--;
		}
		return removed;
	}

	public bool AddGroup(uint _groupID, ISpatialTree<T> _groupPartitioningTree)
	{
		if (_groupID == 0u)
		{
			Logger.Instance?.LogError("Invalid ID; Group ID 0 is reserved for the fallback partitioning tree!");
			return false;
		}
		if (groups.ContainsKey(_groupID))
		{
			Logger.Instance?.LogError($"Invalid ID; {nameof(NestedGroupedTree<T>)} already has a group with ID {_groupID}!");
			return false;
		}
		
		groups.Add(_groupID, _groupPartitioningTree);

		PartitionBounds.Expand(_groupPartitioningTree.PartitionBounds);
		if (_groupPartitioningTree.ContentBounds.Volume > 0)
		{
			ContentBounds.Expand(_groupPartitioningTree.ContentBounds);
		}
		if (_groupPartitioningTree.ObjectCount > 0)
		{
			ObjectCount += _groupPartitioningTree.ObjectCount;
		}
		return true;
	}

	public void RecalculateContentBounds(bool _recursive = true)
	{
		fallback.RecalculateContentBounds(_recursive);
		ContentBounds = fallback.ContentBounds;

		foreach (ISpatialTree<T> group in groups.Values)
		{
			group.RecalculateContentBounds(_recursive);
			ContentBounds.Expand(group.ContentBounds);
		}
	}

	public void GetAllObjects(List<T> _dstAllObjects)
	{
		foreach (ISpatialTree<T> group in groups.Values)
		{
			group.GetAllObjects(_dstAllObjects);
		}

		fallback.GetAllObjects(_dstAllObjects);
	}

	public IEnumerator<T> GetEnumerator()
	{
		IEnumerator<T> e;
		foreach (ISpatialTree<T> group in groups.Values)
		{
			e = group.GetEnumerator();
			while (e.MoveNext())
			{
				yield return e.Current;
			}
		}

		e = fallback.GetEnumerator();
		while (e.MoveNext())
		{
			yield return e.Current;
		}
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void GetObjectsInBounds(in AABB _boundingBox, List<T> _dstObjects)
	{
		foreach (ISpatialTree<T> group in groups.Values)
		{
			group.GetObjectsInBounds(in _boundingBox, _dstObjects);
		}

		fallback.GetObjectsInBounds(in _boundingBox, _dstObjects);
	}

	#endregion
}
