using FragEngine3.EngineCore;
using System.Collections;
using System.Runtime.CompilerServices;

namespace FragEngine3.Scenes.SpatialTrees;

public sealed class OctreeBranch<T>(uint _depth = 0, int _initialCapacity = OctreeBranch<T>.defaultObjectCapacity) : ISpatialTree<T>
	where T : ISpatialTreeObject
{
	#region Types

	[InlineArray(10)]
	private struct BranchBuffer
	{
		public OctreeBranch<T>? branch;
	}

	#endregion
	#region Fields

	public readonly uint depth = _depth;

	public readonly List<T> objects = new(_initialCapacity);

	private BranchBuffer branches = new();

	#endregion
	#region Constants

	public const int defaultObjectCapacity = 16;
	public const int minimumObjectsBeforeBranching = 5;
	public const uint maximumTreeDepth = 5;

	#endregion
	#region Properties

	public bool IsRootBranch => depth == 0;
	public bool IsBranched { get; private set; } = false;

	public AABB PartitionBounds { get; private set; } = AABB.One;
	public AABB ContentBounds { get; private set; } = AABB.Zero;

	public int ObjectCount { get; private set; } = 0;

	#endregion
	#region Methods

	public void Clear(bool _discardSubBranches = false)
	{
		ContentBounds = new(PartitionBounds.minimum, PartitionBounds.minimum);

		objects.Clear();
		ObjectCount = 0;

		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				subBranch?.Clear();
			}
		}

		if (_discardSubBranches)
		{
			IsBranched = false;
			for (int i = 0; i < 8; ++i)
			{
				branches[i] = null;
			}
		}
	}

	public bool AddObject(T _newObject)
	{
		if (_newObject is null)
		{
			return false;
		}

		AABB bounds = _newObject.CalculateAxisAlignedBoundingBox();
		return AddObject(_newObject, in bounds, true);
	}

	private bool AddObject(T _newObject, in AABB _newObjectBounds, bool _allowFurtherBranching)
	{
		if (!PartitionBounds.Overlaps(in _newObjectBounds))
		{
			return false;
		}

		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				if (subBranch!.AddObject(_newObject, in _newObjectBounds, _allowFurtherBranching))
				{
					ContentBounds.Expand(subBranch.ContentBounds);
					ObjectCount++;
					return true;
				}
			}
			return false;
		}

		objects.Add(_newObject);
		ContentBounds.Expand(in _newObjectBounds);
		ObjectCount++;

		if (!_allowFurtherBranching ||
			objects.Count < minimumObjectsBeforeBranching ||
			ContentBounds.Overlaps(in _newObjectBounds) ||
			depth >= maximumTreeDepth)
		{
			return true;
		}

		// Split branch into 2 sub-branches:
		if (branches[0] is null)
		{
			uint subBranchDepth = depth + 1;
			int subBranchInitCapacity = Math.Max(objects.Count / 2, defaultObjectCapacity);

			for (int i = 0; i < 8; ++i)
			{
				AABB subBranchBounds = PartitionBounds.GetOctreePartition(i);
				branches[i] = new(subBranchDepth, subBranchInitCapacity)
				{
					PartitionBounds = subBranchBounds,
				};
			}
		}

		// Distribute all objects into sub-branches:
		foreach (T obj in objects)
		{
			AABB objBounds = obj.CalculateAxisAlignedBoundingBox();
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				if (subBranch!.AddObject(obj, in objBounds, false))
				{
					break;
				}
			}
		}

		objects.Clear();
		return true;
	}

	public bool RemoveObject(T _object)
	{
		if (_object is null)
		{
			Logger.Instance?.LogError("Cannot remove null object from BSP tree!");
			return false;
		}

		return Remove_internal(_object);
	}

	private bool Remove_internal(T _object)
	{
		if (objects.Count != 0 && objects.Remove(_object))
		{
			ObjectCount--;
			return true;
		}

		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				if (subBranch!.Remove_internal(_object))
				{
					ObjectCount--;
					return true;
				}
			}
		}
		return false;
	}

	public void RecalculateContentBounds(bool _recursive = true)
	{
		bool hasObjects = objects.Count != 0;
		bool hasBranches = _recursive && IsBranched;

		if (hasObjects)
		{
			ContentBounds = objects[0].CalculateAxisAlignedBoundingBox();
			for (int i = 1; i < objects.Count; ++i)
			{
				AABB bounds = objects[i].CalculateAxisAlignedBoundingBox();
				ContentBounds.Expand(in bounds);
			}
		}

		if (hasBranches)
		{
			OctreeBranch<T> subBranch = branches[0]!;
			if (hasObjects)
			{
				ContentBounds.Expand(subBranch.ContentBounds);
			}
			else
			{
				ContentBounds = subBranch.ContentBounds;
			}

			for (int i = 0; i < 8; i++)
			{
				subBranch = branches[i]!;
				subBranch!.RecalculateContentBounds(true);
				ContentBounds.Expand(subBranch.ContentBounds);
			}
		}

		if (!hasObjects && !hasBranches)
		{
			ContentBounds = new(PartitionBounds.minimum, PartitionBounds.minimum);
		}
	}

	public void GetAllObjects(List<T> _dstAllObjects)
	{
		if (objects.Count != 0)
		{
			_dstAllObjects.AddRange(objects);
		}
		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				subBranch!.GetAllObjects(_dstAllObjects);
			}
		}
	}

	public IEnumerator<T> GetEnumerator()
	{
		if (objects.Count != 0)
		{
			foreach (T obj in objects)
			{
				yield return obj;
			}
		}
		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				IEnumerator<T> eA = subBranch!.GetEnumerator();
				while (eA.MoveNext())
				{
					yield return eA.Current;
				}
			}
		}
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void GetObjectsInBounds(in AABB _boundingBox, List<T> _dstObjects)
	{
		foreach (T obj in objects)
		{
			if (_boundingBox.Overlaps(obj.CalculateAxisAlignedBoundingBox()))
			{
				_dstObjects.Add(obj);
			}
		}
		if (IsBranched)
		{
			foreach (OctreeBranch<T>? subBranch in branches)
			{
				subBranch!.GetObjectsInBounds(in _boundingBox, _dstObjects);
			}
		}
	}

	#endregion
}
