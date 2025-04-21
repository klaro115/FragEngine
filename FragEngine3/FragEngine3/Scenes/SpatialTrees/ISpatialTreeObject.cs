namespace FragEngine3.Scenes.SpatialTrees;

/// <summary>
/// Interface for objects that may use hierarchical spatial partitioning to order large numbers of them,
/// such as for accelerating lookups in less-than-linear time.
/// </summary>
public interface ISpatialTreeObject
{
	#region Properties

	/// <summary>
	/// An ID or tag that identifies this object as part of a specific spatial partitioning group.<para/>
	/// Note: Objects that are not associated with any one parrtitioning group should return zero.
	/// </summary>
	uint SpatialPartitionGroupID { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Gets or recalculates the current bounding box of the object.
	/// </summary>
	/// <returns>A bounding box volume.</returns>
	AABB CalculateAxisAlignedBoundingBox();

	#endregion
}
