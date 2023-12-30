using Veldrid;

namespace FragEngine3;

public sealed class CameraOutput
{
	#region Fields

	// Color output:
	public uint resolutionX = 1280;
	public uint resolutionY = 720;
	public PixelFormat colorFormat = PixelFormat.R8_G8_B8_A8_UNorm;

	// Depth/Stencil output:
	public bool hasDepth = true;
	public bool hasStencil = false;
	public PixelFormat depthFormat = PixelFormat.D32_Float_S8_UInt;

	#endregion
	#region Properties

	public bool IsValid => resolutionX != 0 && resolutionY != 0;

	public float AspectRatio => (float)resolutionX / resolutionY;

    #endregion
    #region Methods

	public bool HaveSettingsChanged(in Framebuffer _framebuffer)
	{
		if (_framebuffer == null || _framebuffer.IsDisposed) return false;

		OutputDescription outputDesc = _framebuffer.OutputDescription;

		if (_framebuffer.Width != resolutionX ||
			_framebuffer.Height != resolutionY ||
			colorFormat != outputDesc.ColorAttachments[0].Format)
		{
			return true;
		}

		bool descHasDepth = outputDesc.DepthAttachment != null;
		if (descHasDepth == hasDepth) return true;

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
			depthFormat = PixelFormat.R8_UNorm;
			hasStencil = false;
		}
	}

    public override string ToString()
	{
		return $"ResX: {resolutionX}, ResY: {resolutionY}, Color Format: {colorFormat}, Depth Format: {depthFormat} (D: {hasDepth}, S: {hasStencil})";
	}

    #endregion
}
