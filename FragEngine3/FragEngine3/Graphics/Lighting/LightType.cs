namespace FragEngine3.Graphics.Lighting;

/// <summary>
/// Enumeration of different supported light types.
/// This dictates how a light works, in which direction and from which origin light rays are cast.
/// </summary>
public enum LightType : uint
{
	/// <summary>
	/// Point-shaped light source. All light rays are cast uniformely in all directions, starting
	/// from a single point.
	/// </summary>
	Point = 0,
	/// <summary>
	/// Cone-shaped light source. All light rays are cast from a single point and in one general
	/// direction, with rays distributed evenly across a given angle.
	/// </summary>
	Spot = 1,
	/// <summary>
	/// Directional sun-like light source. Light rays are cast following a single direction across
	/// the entire world space. The light does not attenuate over increasing distances, and is not
	/// tied to the source's position.
	/// </summary>
	Directional = 2,
}
