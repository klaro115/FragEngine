using Veldrid;

namespace FragEngine3.Graphics.Cameras;

public struct CameraClearing
{
	#region Constructors

	public CameraClearing() { }

	#endregion
	#region Fields

	// Color targets:
	public bool clearColor = true;
	public RgbaFloat clearColorValue = RgbaFloat.CornflowerBlue;

	// Depth/Stencil targets:
	public bool clearDepth = true;
	public bool clearStencil = true;
	public float clearDepthValue = 1.0f;
	public byte clearStencilValue = 0x00;

	#endregion
	#region Methods

	public override readonly string ToString()
	{
		return $"Color: clear={clearColor}, value={clearColorValue} | Depth: clear={clearDepth}, value={clearDepthValue} | Stencil: clear={clearStencil}, value={clearStencilValue}";
	}

	#endregion
}
