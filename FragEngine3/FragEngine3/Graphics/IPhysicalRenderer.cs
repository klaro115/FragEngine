using System.Numerics;

namespace FragEngine3.Graphics;

public interface IPhysicalRenderer : IRenderer
{
	#region Properties

	/// <summary>
	/// Gets the center point position of this renderer within the scene in world space.
	/// </summary>
	Vector3 VisualCenterPoint { get; }

	/// <summary>
	/// Gets the radius of the bounding sphere containing this renderer's visual features, centered around <see cref="VisualCenterPoint"/>.
	/// </summary>
	float BoundingRadius { get; }

	#endregion
}
