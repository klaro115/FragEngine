using Veldrid;

namespace FragEngine3;

public struct CameraOutput
{
	#region Constructors

	public CameraOutput() { }

	#endregion
	#region Fields

	// Color output:
	public uint resolutionX = 1280;
	public uint resolutionY = 720;
	public PixelFormat? colorFormat = null;		//default is used if null.

	// Depth/Stencil output:
	public bool hasDepth = true;
	public bool hasStencil = false;
	public PixelFormat? depthFormat = null;     //default is used if null.

	#endregion
	#region Properties

	public readonly bool IsValid => resolutionX != 0 && resolutionY != 0;

	public readonly float AspectRatio => (float)resolutionX / resolutionY;

    #endregion
    #region Methods

	public readonly bool HaveSettingsChanged(in Framebuffer _framebuffer)
	{
		if (_framebuffer == null || _framebuffer.IsDisposed) return false;

		OutputDescription outputDesc = _framebuffer.OutputDescription;

		bool descHasColor = outputDesc.ColorAttachments != null && outputDesc.ColorAttachments.Length != 0;
		if (_framebuffer.Width != resolutionX ||
			_framebuffer.Height != resolutionY ||
			(descHasColor && colorFormat != null && colorFormat != outputDesc.ColorAttachments![0].Format))
		{
			return true;
		}

		bool descHasDepth = outputDesc.DepthAttachment != null;
		if (descHasDepth != hasDepth) return true;

		if (outputDesc.DepthAttachment != null)
		{
			if (depthFormat != outputDesc.DepthAttachment.Value.Format) return true;
		}
		return false;
	}

	public void Update(in Framebuffer _framebuffer)
	{
		if (_framebuffer == null || _framebuffer.IsDisposed) return;

		OutputDescription outputDesc = _framebuffer.OutputDescription;

		resolutionX = _framebuffer.Width;
		resolutionY = _framebuffer.Height;
		colorFormat = outputDesc.ColorAttachments[0].Format;

		hasDepth = outputDesc.DepthAttachment != null;
		if (hasDepth)
		{
			depthFormat = outputDesc.DepthAttachment!.Value.Format;
			hasStencil =
				depthFormat == PixelFormat.D24_UNorm_S8_UInt ||
				depthFormat == PixelFormat.D32_Float_S8_UInt;
		}
		else
		{
			depthFormat = null;
			hasStencil = false;
		}
	}

    public readonly override string ToString()
	{
		string colorFormatTxt = colorFormat?.ToString() ?? "Default";
		string depthFormatTxt = hasDepth ? (depthFormat?.ToString() ?? "Default") : "None";
		return $"ResX: {resolutionX}, ResY: {resolutionY}, Color Format: {colorFormatTxt}, Depth Format: {depthFormatTxt} (D: {hasDepth}, S: {hasStencil})";
	}

    #endregion
}
