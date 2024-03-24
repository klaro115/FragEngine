using FragEngine3.EngineCore;
using System.Numerics;
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
	private Matrix4x4[] shadowMatrixBuffer = null!;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public uint ResourceVersion { get; private set; } = 0;

	public Texture TexNormalMapArray { get; private set; } = null!;
	public Texture TexDepthMapArray { get; private set; } = null!;
	public DeviceBuffer BufShadowMatrices { get; private set; } = null!;
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
		DisposeTextureArrays();
		BufShadowMatrices?.Dispose();
	}

	private void DisposeTextureArrays()
	{
		TexNormalMapArray?.Dispose();
		TexDepthMapArray?.Dispose();

		foreach (Framebuffer framebuffer in framebuffers)
		{
			framebuffer?.Dispose();
		}
		framebuffers.Clear();
	}

	public bool PrepareTextureArrays(uint _requiredShadowMapCount, out bool _outRecreatedShadowMaps)
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

			// Texture arrays
			if (!ResizeTextureArrays())
			{
				Logger.LogError("Failed to prepare shadow map texture arrays!");
				return false;
			}

			// Projection matrices:
			if (!ResizeShadowMatrixBuffers())
			{
				Logger.LogError("Failed to prepare BufShadowMatrices!");
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

	public bool FinalizeProjectionMatrices(CommandList? _cmdList = null)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot finalize preparing disposed light data buffers for use!");
			return false;
		}

		// Projection matrices:
		try
		{
			Span<Matrix4x4> span = shadowMatrixBuffer.AsSpan(0, 2 * (int)Count);
			if (_cmdList != null)
			{
				_cmdList.UpdateBuffer(BufShadowMatrices, 0, span);
			}
			else
			{
				core.Device.UpdateBuffer(BufShadowMatrices, 0, span);
			}

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to upload light data to GPU buffer!", ex);
			return false;
		}
	}

	public bool SetShadowProjectionMatrices(uint _shadowMapIdx, in Matrix4x4 _mtxWorld2Clip)
	{
		if (_shadowMapIdx >= Capacity)
		{
			return false;
		}

		shadowMatrixBuffer[2 * _shadowMapIdx + 0] = _mtxWorld2Clip;
		if (Matrix4x4.Invert(_mtxWorld2Clip, out shadowMatrixBuffer[2 * _shadowMapIdx + 1]))
		{
			shadowMatrixBuffer[2 * _shadowMapIdx + 1] = Matrix4x4.Identity;
		}
		return true;
	}

	public bool SetShadowProjectionMatrices(uint _shadowMapIdx, in Matrix4x4 _mtxWorld2Clip, in Matrix4x4 _maxClip2World)
	{
		if (_shadowMapIdx >= Capacity)
		{
			return false;
		}

		shadowMatrixBuffer[2 * _shadowMapIdx + 0] = _mtxWorld2Clip;
		shadowMatrixBuffer[2 * _shadowMapIdx + 1] = _maxClip2World;
		return true;
	}

	private bool ResizeTextureArrays()
	{
		if (IsDisposed) return false;

		DisposeTextureArrays();

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

		FramebufferAttachmentDescription[] framebufferColorDescs = new FramebufferAttachmentDescription[1];

		try
		{
			for (uint i = 0; i < Capacity; ++i)
			{
				FramebufferAttachmentDescription framebufferDepthDesc = new(TexDepthMapArray, i);
				framebufferColorDescs[0] = new FramebufferAttachmentDescription(TexNormalMapArray, i);

				FramebufferDescription frameBufferDesc = new(
					framebufferDepthDesc,
					framebufferColorDescs);

				Framebuffer framebuffer = core.MainFactory.CreateFramebuffer(ref frameBufferDesc);
				framebuffer.Name = $"Framebuffer_ShadowMapArray_Size={resolution}x{resolution}_Layer={i}/{Capacity}";

				framebuffers.Add(framebuffer);
			}
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create framebuffers for shadow map texture arrays!", ex);
			return false;
		}

		return true;
	}

	private bool ResizeShadowMatrixBuffers()
	{
		if (IsDisposed) return false;

		BufShadowMatrices?.Dispose();

		if (shadowMatrixBuffer.Length < Capacity)
		{
			shadowMatrixBuffer = new Matrix4x4[Capacity];
			Array.Fill(shadowMatrixBuffer, Matrix4x4.Identity);
		}

		const uint elementByteSize = 4 * sizeof(float);
		uint bufferByteSize = 2 * elementByteSize * Capacity;

		BufferDescription bufferDesc = new(
			bufferByteSize,
			BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
			elementByteSize);

		try
		{
			BufShadowMatrices = core.MainFactory.CreateBuffer(ref bufferDesc);
			BufShadowMatrices.Name = $"BufShadowMatrices_Capacity={Capacity}";

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create shadow projection matrix buffer!", ex);
			return false;
		}
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
