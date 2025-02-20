namespace FragEngine3.Graphics.Resources.Materials;

/// <summary>
/// Enumeration of different type of materials and their purposes.
/// </summary>
public enum MaterialType
{
	/// <summary>
	/// The material is used to render the mesh surface of 3D objects.
	/// </summary>
	Surface,
	/// <summary>
	/// The material is used to execute GPU computations using a compute shader.
	/// </summary>
	Compute,
	/// <summary>
	/// The material is used to render screen-space visual effects or for post-processing.
	/// </summary>
	PostProcessing,
	/// <summary>
	/// The material is used to render controls and text for the user interface.
	/// </summary>
	UI,
	/// <summary>
	/// The material is used for compositing rendering outputs into a final image.
	/// </summary>
	Compositing,
	//...
}
