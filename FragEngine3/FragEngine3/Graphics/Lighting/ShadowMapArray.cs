using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

public sealed class ShadowMapArray : IDisposable
{
	#region Constructors

	public ShadowMapArray(GraphicsCore _core, uint _initialCount = 1)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		ShadowMapCount = Math.Max(_initialCount, 1);

		ResizeTextureArrays();
	}

	~ShadowMapArray()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private readonly List<Framebuffer> framebuffers = [];

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Texture TexNormalMapArray { get; private set; } = null!;
	public Texture TexDepthMapArray { get; private set; } = null!;

	public uint ShadowMapCount { get; private set; } = 0;

	private Logger Logger => core.graphicsSystem.engine.Logger;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _disposing)
	{
		IsDisposed = true;
		DisposeResources();
	}

	private void DisposeResources()
	{
		TexNormalMapArray?.Dispose();
		TexDepthMapArray?.Dispose();

		foreach (Framebuffer framebuffer in framebuffers)
		{
			framebuffer?.Dispose();
		}
		framebuffers.Clear();
	}

	public bool Prepare(uint _requiredShadowMapCount)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot prepare disposed shadow map array for use!");
			return false;
		}

		if (_requiredShadowMapCount > ShadowMapCount)
		{
			ShadowMapCount = _requiredShadowMapCount;

			if (!ResizeTextureArrays())
			{
				Logger.LogError("Failed to prepare shadow map texture arrays!");
				return false;
			}
		}

		return true;
	}

	public bool GetFramebuffer(uint _shadowMapIdx, out Framebuffer _outFramebuffer)
	{
		if (IsDisposed)
		{
			_outFramebuffer = null!;
			return false;
		}
		
		if (_shadowMapIdx >= ShadowMapCount)
		{
			Logger.LogError($"Shadow map index {_shadowMapIdx} is out of range! Make sure you call 'Prepare()' with the right number of array elements!");
			_outFramebuffer = null!;
			return false;
		}

		_outFramebuffer = framebuffers[(int)_shadowMapIdx];
		return true;
	}

	private bool ResizeTextureArrays()
	{
		if (IsDisposed) return false;

		DisposeResources();

		const uint resolution = LightConstants.shadowResolution;
		const PixelFormat normalFormat = PixelFormat.R8_G8_B8_A8_UNorm;
		PixelFormat depthFormat = core.DefaultShadowMapDepthTargetFormat;

		// Normals texture array:

		TextureDescription texNormalMapArrayDesc = new(
			resolution,
			resolution,
			1,
			1,
			ShadowMapCount,
			normalFormat,
			TextureUsage.Sampled | TextureUsage.RenderTarget,
			TextureType.Texture2D);

		try
		{
			TexNormalMapArray = core.MainFactory.CreateTexture(ref texNormalMapArrayDesc);
			TexNormalMapArray.Name = $"TexShadowMapArray_Normals_Size={resolution}x{resolution}_Layers={ShadowMapCount}_Format={normalFormat}";
		}
		catch (Exception ex)
		{
			Logger.LogException($"Failed to create shadow maps' normal map texture array!", ex);
			return false;
		}

		// Depth texture array:

		TextureDescription texDepthMapArrayDesc = new(
			resolution,
			resolution,
			1,
			1,
			ShadowMapCount,
			depthFormat,
			TextureUsage.Sampled | TextureUsage.DepthStencil,
			TextureType.Texture2D);

		try
		{
			TexDepthMapArray = core.MainFactory.CreateTexture(ref texDepthMapArrayDesc);
			TexDepthMapArray.Name = $"TexShadowMapArray_Depth_Size={resolution}x{resolution}_Layers={ShadowMapCount}_Format={depthFormat}";
		}
		catch (Exception ex)
		{
			Logger.LogException($"Failed to create shadow maps' depth map texture array!", ex);
			return false;
		}

		// Frame buffers:

		for (uint i = 0; i < ShadowMapCount; ++i)
		{
			FramebufferDescription frameBufferDesc = new(
				new FramebufferAttachmentDescription(TexDepthMapArray, i),
				[
					new FramebufferAttachmentDescription(TexNormalMapArray, i)
				]);

			Framebuffer framebuffer = core.MainFactory.CreateFramebuffer(ref frameBufferDesc);
			framebuffer.Name = $"Framebuffer_ShadowMapArray_Size={resolution}x{resolution}_Layer={i}/{ShadowMapCount}";

			framebuffers.Add(framebuffer);
		}

		return true;
	}

	#endregion
}
