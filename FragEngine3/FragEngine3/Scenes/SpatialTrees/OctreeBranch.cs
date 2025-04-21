using FragEngine3.EngineCore;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace FragEngine3.Scenes.SpatialTrees;

public sealed class OctreeBranch(uint _depth = 0, int _initialCapacity = OctreeBranch.defaultObjectCapacity) : ISpatialTree
{
	#region Types

	[InlineArray(10)]
	private struct BranchBuffer
	{
		public OctreeBranch? branch;
	}

	#endregion
	#region Fields

	public readonly uint depth = _depth;

	public readonly List<ISpatialTreeObject> objects = new(_initialCapacity);

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

	#endregion
	#region Methods

	public void Clear(bool _discardSubBranches = false)
	{
		ContentBounds = new(PartitionBounds.minimum, Vector3.Zero);

		objects.Clear();
		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
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

	public bool AddObject(ISpatialTreeObject _newObject)
	{
		if (_newObject is null)
		{
			return false;
		}

		AABB bounds = _newObject.CalculateAxisAlignedBoundingBox();
		return AddObject(_newObject, in bounds, true);
	}

	private bool AddObject(ISpatialTreeObject _newObject, in AABB _newObjectBounds, bool _allowFurtherBranching)
	{
		if (!PartitionBounds.Overlaps(in _newObjectBounds))
		{
			return false;
		}

		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
			{
				if (subBranch!.AddObject(_newObject, in _newObjectBounds, _allowFurtherBranching))
				{
					ContentBounds.Expand(subBranch.ContentBounds);
					return true;
				}
			}
			return false;
		}

		objects.Add(_newObject);
		ContentBounds.Expand(in _newObjectBounds);

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
		foreach (ISpatialTreeObject obj in objects)
		{
			AABB objBounds = obj.CalculateAxisAlignedBoundingBox();
			foreach (OctreeBranch? subBranch in branches)
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

	public bool RemoveObject(ISpatialTreeObject _object)
	{
		if (_object is null)
		{
			Logger.Instance?.LogError("Cannot remove null object from BSP tree!");
			return false;
		}

		return Remove_internal(_object);
	}

	private bool Remove_internal(ISpatialTreeObject _object)
	{
		if (objects.Count != 0 && objects.Remove(_object))
		{
			return true;
		}

		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
			{
				if (subBranch!.Remove_internal(_object))
				{
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
			OctreeBranch subBranch = branches[0]!;
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
			ContentBounds = new(PartitionBounds.minimum, Vector3.Zero);
		}
	}

	public void GetAllObjects(List<ISpatialTreeObject> _dstAllObjects)
	{
		if (objects.Count != 0)
		{
			_dstAllObjects.AddRange(objects);
		}
		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
			{
				subBranch!.GetAllObjects(_dstAllObjects);
			}
		}
	}

	public IEnumerator<ISpatialTreeObject> EnumerateAllObjects()
	{
		if (objects.Count != 0)
		{
			foreach (ISpatialTreeObject obj in objects)
			{
				yield return obj;
			}
		}
		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
			{
				IEnumerator<ISpatialTreeObject> eA = subBranch!.EnumerateAllObjects();
				while (eA.MoveNext())
				{
					yield return eA.Current;
				}
			}
		}
	}

	public void GetObjectsInBounds(in AABB _boundingBox, List<ISpatialTreeObject> _dstObjects)
	{
		foreach (ISpatialTreeObject obj in objects)
		{
			if (_boundingBox.Overlaps(obj.CalculateAxisAlignedBoundingBox()))
			{
				_dstObjects.Add(obj);
			}
		}
		if (IsBranched)
		{
			foreach (OctreeBranch? subBranch in branches)
			{
				subBranch!.GetObjectsInBounds(in _boundingBox, _dstObjects);
			}
		}
	}

	#endregion
}
