using FragEngine3.EngineCore;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

/// <summary>
/// Management and container type for shadow maps and shadow projection matrix buffers.
/// Ownership of the shadow map array texture (TexShadowMaps) and the matrix buffer (BufShadowMatrices) lies with this object.
/// </summary>
public sealed class ShadowMapArray : IDisposable
{
	#region Constructors

	/// <summary>
	/// Creates a new shadow map array.
	/// </summary>
	/// <param name="_core">The graphics core to use for GPU resource creation.</param>
	/// <param name="_initialCount">The initial number of shadow maps to allocate; the number may increase on-demand at run-time.
	/// One layer is required for each cascade of each shadow-castinglight source. Must be 1 or higher.</param>
	/// <exception cref="ArgumentNullException">Thrown if graphics core is null.</exception>
	/// <exception cref="Exception">Thrown if initial resource creation has failed.</exception>
	public ShadowMapArray(GraphicsCore _core, uint _initialCount = 1)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		Capacity = Math.Max(_initialCount, 1);

		shadowMatrixBuffer = new Matrix4x4[MatrixCapacity];

		bool success = true;
		success &= ResizeTextureArrays();
		success &= ResizeShadowMatrixBuffers();
		success &= CreateShadowMapSampler();
		if (!success)
		{
			throw new Exception("Creation of resources for shadow map array has failed!");
		}
	}

	~ShadowMapArray()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered when the shadow map array has recreated or resized its shadow map texture array.
	/// </summary>
	public event Action? OnRecreatedShadowMapsEvent = null;

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private readonly List<Framebuffer> framebuffers = [];
	private Matrix4x4[] shadowMatrixBuffer;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public uint ResourceVersion { get; private set; } = 0;

	public Texture TexNormalMapArray { get; private set; } = null!;
	public Texture TexDepthMapArray { get; private set; } = null!;
	public DeviceBuffer BufShadowMatrices { get; private set; } = null!;
	public Sampler SamplerShadowMaps { get; private set; } = null!;

	/// <summary>
	/// Gets the number of shadow maps that are currently in use.
	/// </summary>
	public uint Count {  get; private set; } = 0;
	/// <summary>
	/// Gets the total number of shadow maps that are currently allocated.
	/// </summary>
	public uint Capacity { get; private set; } = 0;
	/// <summary>
	/// Gets the total number of shadow projection matrices that are currently allocated.
	/// There are always 2 matrices for each shadow map.
	/// </summary>
	public uint MatrixCapacity => Capacity * 2;

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

	/// <summary>
	/// Retrieves the framebuffer for rendering a shadow map cascade straight to a layer in the shadow map texture array.
	/// </summary>
	/// <param name="_shadowMapArrayIdx">Index of the shadow map for which we need a framebuffer.</param>
	/// <param name="_outFramebuffer">Outputs the requested layer's framebuffer, or null on failure.</param>
	/// <returns>True if a framebuffer could be retrieved, false otherwise.</returns>
	public bool GetFramebuffer(uint _shadowMapArrayIdx, out Framebuffer _outFramebuffer)
	{
		if (IsDisposed)
		{
			_outFramebuffer = null!;
			return false;
		}
		
		if (_shadowMapArrayIdx >= Capacity)		//Note: 'Count' would be more correct here, but 'Capacity' is safer against creashes and aborts.
		{
			Logger.LogError($"Shadow map index {_shadowMapArrayIdx} is out of range! Make sure you call 'Prepare()' with the right number of array elements!");
			_outFramebuffer = null!;
			return false;
		}

		_outFramebuffer = framebuffers[(int)_shadowMapArrayIdx];
		return true;
	}

	/// <summary>
	/// Sets the projection matrix for a specific shadow map.
	/// </summary>
	/// <param name="_shadowMapArrayIdx">Index of the shadow map whose matrix is being set.</param>
	/// <param name="_mtxWorld2Clip">Shadow map projection matrix, transforming from world space to the shadow camera's clip space.
	/// An accompanying inverse matrix is calculated and assigned as well.</param>
	/// <returns>True if the inex was valid and the matrix set, false otherwise.</returns>
	public bool SetShadowProjectionMatrices(uint _shadowMapArrayIdx, in Matrix4x4 _mtxWorld2Clip)
	{
		if (_shadowMapArrayIdx >= Capacity)
		{
			return false;
		}

		shadowMatrixBuffer[2 * _shadowMapArrayIdx + 0] = _mtxWorld2Clip;
		if (!Matrix4x4.Invert(_mtxWorld2Clip, out shadowMatrixBuffer[2 * _shadowMapArrayIdx + 1]))
		{
			shadowMatrixBuffer[2 * _shadowMapArrayIdx + 1] = Matrix4x4.Identity;
		}
		return true;
	}

	/// <summary>
	/// Sets the projection matrix for a specific shadow map.
	/// </summary>
	/// <param name="_shadowMapArrayIdx">Index of the shadow map whose matrix is being set.</param>
	/// <param name="_mtxWorld2Clip">Shadow map projection matrix, transforming from world space to the shadow camera's clip space.</param>
	/// <param name="_maxClip2World">Inverse shadow map projection matrix, transforming from the shadow camera's clip space to world space.</param>
	/// <returns>True if the inex was valid and the matrices set, false otherwise.</returns>
	public bool SetShadowProjectionMatrices(uint _shadowMapArrayIdx, in Matrix4x4 _mtxWorld2Clip, in Matrix4x4 _maxClip2World)
	{
		if (_shadowMapArrayIdx >= Capacity)
		{
			return false;
		}

		shadowMatrixBuffer[2 * _shadowMapArrayIdx + 0] = _mtxWorld2Clip;
		shadowMatrixBuffer[2 * _shadowMapArrayIdx + 1] = _maxClip2World;
		return true;
	}

	/// <summary>
	/// Finalizes and uploads shadow map projection matrices to GPU buffer for upcoming rendering passes.
	/// </summary>
	/// <param name="_cmdList">A command list through which the matrix data should be uploaded to GPU memory. If null, upload is done via the graphics device.</param>
	/// <returns>True if data was successfully sent to GPU buffer, false otherwise.</returns>
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
			if (_cmdList is not null)
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

		if (shadowMatrixBuffer.Length < MatrixCapacity)
		{
			shadowMatrixBuffer = new Matrix4x4[MatrixCapacity];
			Array.Fill(shadowMatrixBuffer, Matrix4x4.Identity);
		}

		const uint elementByteSize = 16 * sizeof(float);
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
			SamplerAddressMode.Border,
			SamplerAddressMode.Border,
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
		SamplerShadowMaps = samplerShadowMaps;
		SamplerShadowMaps.Name = "Sampler_ShadowMaps";
		return true;
	}

	#endregion
}
