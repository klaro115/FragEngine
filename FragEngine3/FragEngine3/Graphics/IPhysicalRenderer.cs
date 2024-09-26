using System.Numerics;

namespace FragEngine3.Graphics;

/// <summary>
/// Interface for renderer types that draw "physical" objects in the scene. These are environment assets, terrain, essentially all
/// types of nodes that represent objects with a tangible surface. Examples of non-physical renderers would be volumetris and weather,
/// post-processing effects, or UI controls.
/// </summary>
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
