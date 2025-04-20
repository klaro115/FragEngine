namespace FragEngine3.Scenes.SpatialTrees;

/// <summary>
/// Interface for hierarchical structures that may be used for spatial partitioning of objects.
/// Types implementing this interface allow faster lookup of objects within a region of space,
/// typically reducing computational complexity to O(Log(N)).
/// </summary>
public interface ISpatialTree
{
	#region Properties

	/// <summary>
	/// Gets whether this branch is the root of the spatial partitioning tree.
	/// </summary>
	bool IsRootBranch { get; }

	/// <summary>
	/// Gets a bounding box enclosing the section of space that was partitioned off into this branch.
	/// </summary>
	AABB PartitionBounds { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Removes all objects from this tree branch.
	/// </summary>
	/// <param name="_discardSubBranches">Whether to discard all sub-branches.</param>
	void Clear(bool _discardSubBranches = false);

	/// <summary>
	/// Tries to add a new object to this branch. The object can only be added if it overlaps with this branch's <see cref="PartitionBounds"/>.<para/>
	/// Note: This does not check for duplicates! It is the caller's responsibility to ensure each object is only added to the BSP tree once.
	/// </summary>
	/// <param name="_newObject">The new object we wish to add.</param>
	/// <returns>True if the object was added, false otherwise.</returns>
	bool AddObject(ISpatialTreeObject _newObject);

	/// <summary>
	/// Removes an object from this tree or any of its sub-branches.
	/// </summary>
	/// <param name="_object">The object we wish to remove.</param>
	/// <returns>True if the object was removed from the tree, false if it wasn't found or on error.</returns>
	bool RemoveObject(ISpatialTreeObject _object);

	/// <summary>
	/// Recalculates the bounding box enclosing all objects on this branch.
	/// </summary>
	/// <param name="_recursive">Whether to recursively recalculate all sub-branches as well.</param>
	void RecalculateContentBounds(bool _recursive = true);

	/// <summary>
	/// Retrieves all objects on this branch and all of its sub-branches.
	/// </summary>
	/// <param name="_dstAllObjects">Destination list where all objects will be stored.</param>
	void GetAllObjects(List<ISpatialTreeObject> _dstAllObjects);

	/// <summary>
	/// Gets an enumerator for iterating over all objects on this branch and all of its sub-branches.
	/// </summary>
	IEnumerator<ISpatialTreeObject> EnumerateAllObjects();

	/// <summary>
	/// Retrieves all objects on this branch and all of its sub-branches that overlap a given bounding box volume.
	/// </summary>
	/// <param name="_boundingBox">A bounding box volume within which some or all of the branch's objects may lie.</param>
	/// <param name="_dstObjects">Destination list where resulting objects will be stored.</param>
	void GetObjectsInBounds(in AABB _boundingBox, List<ISpatialTreeObject> _dstObjects);

	#endregion
}
