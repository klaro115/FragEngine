using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

public sealed class ShadowMapArray : IDisposable
{
	#region Constructors

	public ShadowMapArray(GraphicsCore _core, uint _initialCount = 1)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		Capacity = Math.Max(_initialCount, 1);

		ResizeTextureArrays();
		CreateShadowMapSampler();
	}

	~ShadowMapArray()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	public event Action? OnRecreatedShadowMapsEvent = null;

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private readonly List<Framebuffer> framebuffers = [];

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public uint ResourceVersion { get; private set; } = 0;

	public Texture TexNormalMapArray { get; private set; } = null!;
	public Texture TexDepthMapArray { get; private set; } = null!;
	public Sampler SamplerShadowMaps { get; private set; } = null!;

	public uint Count {  get; private set; } = 0;
	public uint Capacity { get; private set; } = 0;

	private Logger Logger => core.graphicsSystem.engine.Logger;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
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

	public bool Prepare(uint _requiredShadowMapCount, out bool _outRecreatedShadowMaps)
	{
		_outRecreatedShadowMaps = false;
		if (IsDisposed)
		{
			Logger.LogError("Cannot prepare disposed shadow map array for use!");
			return false;
		}

		if (_requiredShadowMapCount > Capacity)
		{
			Capacity = _requiredShadowMapCount;

			if (!ResizeTextureArrays())
			{
				Logger.LogError("Failed to prepare shadow map texture arrays!");
				return false;
			}
			_outRecreatedShadowMaps = true;

			ResourceVersion++;
			OnRecreatedShadowMapsEvent?.Invoke();
		}

		Count = _requiredShadowMapCount;
		return true;
	}

	public bool GetFramebuffer(uint _shadowMapIdx, out Framebuffer _outFramebuffer)
	{
		if (IsDisposed)
		{
			_outFramebuffer = null!;
			return false;
		}
		
		if (_shadowMapIdx >= Capacity)		//Note: 'Count' would be more correct here, but 'Capacity' is safer against creashes and aborts.
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
			Capacity,
			normalFormat,
			TextureUsage.Sampled | TextureUsage.RenderTarget,
			TextureType.Texture2D);

		try
		{
			TexNormalMapArray = core.MainFactory.CreateTexture(ref texNormalMapArrayDesc);
			TexNormalMapArray.Name = $"TexShadowMapArray_Normals_Size={resolution}x{resolution}_Layers={Capacity}_Format={normalFormat}";
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
			Capacity,
			depthFormat,
			TextureUsage.Sampled | TextureUsage.DepthStencil,
			TextureType.Texture2D);

		try
		{
			TexDepthMapArray = core.MainFactory.CreateTexture(ref texDepthMapArrayDesc);
			TexDepthMapArray.Name = $"TexShadowMapArray_Depth_Size={resolution}x{resolution}_Layers={Capacity}_Format={depthFormat}";
		}
		catch (Exception ex)
		{
			Logger.LogException($"Failed to create shadow maps' depth map texture array!", ex);
			return false;
		}

		// Frame buffers:

		for (uint i = 0; i < Capacity; ++i)
		{
			FramebufferDescription frameBufferDesc = new(
				new FramebufferAttachmentDescription(TexDepthMapArray, i),
				[
					new FramebufferAttachmentDescription(TexNormalMapArray, i)
				]);

			Framebuffer framebuffer = core.MainFactory.CreateFramebuffer(ref frameBufferDesc);
			framebuffer.Name = $"Framebuffer_ShadowMapArray_Size={resolution}x{resolution}_Layer={i}/{Capacity}";

			framebuffers.Add(framebuffer);
		}

		return true;
	}

	private bool CreateShadowMapSampler()
	{
		SamplerDescription samplerDesc = new(
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerAddressMode.Clamp,
				SamplerFilter.MinLinear_MagLinear_MipPoint,
				null,
				0,
				0,
				uint.MaxValue,
				0,
				SamplerBorderColor.OpaqueWhite);

		if (!core.SamplerManager.GetSampler(ref samplerDesc, out Sampler samplerShadowMaps))
		{
			Logger.LogError("Failed to create sampler for shadow map texture array!");
			return false;
		}
		samplerShadowMaps.Name = "Sampler_ShadowMaps";
		return true;
	}

	#endregion
}
