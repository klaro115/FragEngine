using FragEngine3.EngineCore;
using System.Numerics;

namespace FragEngine3.Scenes.SpatialTrees;

public sealed class BspTreeBranch(uint _depth = 0, BspSplitAxis _splitAxis = BspSplitAxis.X, int _initialCapacity = BspTreeBranch.defaultObjectCapacity) : ISpatialTree
{
	#region Fields

	public readonly uint depth = _depth;
	public readonly BspSplitAxis splitAxis = _splitAxis;

	public readonly List<ISpatialTreeObject> objects = new(_initialCapacity);

	private BspTreeBranch? subBranchA = null;
	private BspTreeBranch? subBranchB = null;

	#endregion
	#region Constants

	public const int defaultObjectCapacity = 16;
	public const int minimumObjectsBeforeBranching = 4;
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
		subBranchA?.Clear();
		subBranchB?.Clear();

		if (_discardSubBranches)
		{
			IsBranched = false;
			subBranchA = null;
			subBranchB = null;
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
			if (subBranchA!.AddObject(_newObject, in _newObjectBounds, _allowFurtherBranching))
			{
				ContentBounds.Expand(subBranchA.ContentBounds);
				return true;
			}
			else if (subBranchB!.AddObject(_newObject, in _newObjectBounds, _allowFurtherBranching))
			{
				ContentBounds.Expand(subBranchB.ContentBounds);
				return true;
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
		if (subBranchA is null)
		{
			uint subBranchDepth = depth + 1;
			BspSplitAxis subBranchSplitAxis = (BspSplitAxis)(((int)splitAxis + 1) % 3);
			int subBranchInitCapacity = Math.Max(objects.Count / 2, defaultObjectCapacity);
			Vector3 subBranchSplitCenter = PartitionBounds.ClampToBounds(ContentBounds.Center); // split through the content's middle.

			PartitionBounds.Split(subBranchSplitCenter, subBranchSplitAxis, out AABB subBranchBoundsA, out AABB subBranchBoundsB);

			subBranchA = new(subBranchDepth, subBranchSplitAxis, subBranchInitCapacity)
			{
				PartitionBounds = subBranchBoundsA,
			};
			subBranchB = new(subBranchDepth, subBranchSplitAxis, subBranchInitCapacity)
			{
				PartitionBounds = subBranchBoundsB,
			};
		}

		// Distribute all objects into sub-branches:
		foreach (ISpatialTreeObject obj in objects)
		{
			AABB objBounds = obj.CalculateAxisAlignedBoundingBox();
			if (!subBranchA.AddObject(obj, in objBounds, false))
			{
				subBranchB!.AddObject(obj, in objBounds, false);
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

		bool removed = IsBranched && (subBranchA!.Remove_internal(_object) || subBranchB!.Remove_internal(_object));
		return removed;
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
			subBranchA!.RecalculateContentBounds(true);
			subBranchB!.RecalculateContentBounds(true);

			if (hasObjects)
			{
				ContentBounds.Expand(subBranchA.ContentBounds);
			}
			else
			{
				ContentBounds = subBranchA.ContentBounds;
			}
			ContentBounds.Expand(subBranchB.ContentBounds);
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
			subBranchA!.GetAllObjects(_dstAllObjects);
			subBranchB!.GetAllObjects(_dstAllObjects);
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
			IEnumerator<ISpatialTreeObject> eA = subBranchA!.EnumerateAllObjects();
			while (eA.MoveNext())
			{
				yield return eA.Current;
			}

			IEnumerator<ISpatialTreeObject> eB = subBranchA!.EnumerateAllObjects();
			while (eB.MoveNext())
			{
				yield return eB.Current;
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
			subBranchA!.GetObjectsInBounds(in  _boundingBox, _dstObjects);
			subBranchB!.GetObjectsInBounds(in  _boundingBox, _dstObjects);
		}
	}

	#endregion
}
