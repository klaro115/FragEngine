using FragEngine3.EngineCore;
using FragEngine3.Graphics.Lighting.Data;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

public sealed class LightDataBuffer : IDisposable
{
	#region Constructors

	public LightDataBuffer(GraphicsCore _core, uint _initialLightCapacity = 1, uint _initialShadowMatrixCapacity = 1)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		LightCapacity = Math.Max(_initialLightCapacity, 1);
		ShadowMatrixCapacity = Math.Max(_initialShadowMatrixCapacity, 1);

		ResizeLightBuffers();
		ResizeShadowMatrixBuffers();
	}

	~LightDataBuffer()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private LightSourceData[] lightDataBuffer = null!;
	private Matrix4x4[] shadowMatrixBuffer = null!;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public DeviceBuffer BufLights { get; private set; } = null!;
	public DeviceBuffer BufShadowMatrices { get; private set; } = null!;					// TODO [Important]: We might need to move this to 'ShadowMapArray' instead! Check before reworking lighting further!

	public uint LightCount { get; private set; } = 0;
	public uint LightCapacity { get; private set; } = 0;
	public uint ShadowMatrixCapacity { get; private set; } = 0;

	private Logger Logger => core.graphicsSystem.engine.Logger;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	public void Dispose(bool _)
	{
		IsDisposed = true;

		BufLights?.Dispose();
		BufShadowMatrices?.Dispose();
	}

	public bool BeginPrepare(uint _requiredLightCount, uint _requiredShadowMatrixCount, out bool _outRecreatedBufLights, out bool _outRecreatedBufShadowMatrices)
	{
		_outRecreatedBufLights = false;
		_outRecreatedBufShadowMatrices = false;

		if (IsDisposed)
		{
			Logger.LogError("Cannot begin preparing disposed light data buffers for use!");
			return false;
		}

		// BufLights:
		if (LightCapacity < _requiredLightCount)
		{
			LightCapacity = _requiredLightCount;
			if (!ResizeLightBuffers())
			{
				Logger.LogError("Failed to prepare BufLights!");
				return false;
			}
			_outRecreatedBufLights = true;
		}

		// BufShadowMatrices:
		if (ShadowMatrixCapacity < _requiredShadowMatrixCount)
		{
			ShadowMatrixCapacity = _requiredShadowMatrixCount;
			if (!ResizeShadowMatrixBuffers())
			{
				Logger.LogError("Failed to prepare BufShadowMatrices!");
				return false;
			}
			_outRecreatedBufShadowMatrices = true;
		}

		LightCount = _requiredLightCount;

		return true;
	}

	public bool SetLightData(uint _lightIdx, in LightSourceData _data)
	{
		if (_lightIdx >= LightCapacity)
		{
			return false;
		}

		lightDataBuffer[_lightIdx] = _data;
		return true;
	}

	public bool SetShadowProjectionMatrix (uint _shadowMapIdx, in Matrix4x4 _mtxWorld2Clip)
	{
		if (_shadowMapIdx >= ShadowMatrixCapacity)
		{
			return false;
		}

		shadowMatrixBuffer[_shadowMapIdx] = _mtxWorld2Clip;
		return true;
	}

	public bool EndPrepare(CommandList? _cmdList = null)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot finalize preparing disposed light data buffers for use!");
			return false;
		}

		// BufLights:
		try
		{
			if (_cmdList != null)
			{
				_cmdList.UpdateBuffer(BufLights, 0, lightDataBuffer);
			}
			else
			{
				core.Device.UpdateBuffer(BufLights, 0, lightDataBuffer);
			}
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to upload light data to GPU buffer!", ex);
			return false;
		}

		// BufShadowMatrices:
		try
		{
			if (_cmdList != null)
			{
				_cmdList.UpdateBuffer(BufShadowMatrices, 0, shadowMatrixBuffer);
			}
			else
			{
				core.Device.UpdateBuffer(BufShadowMatrices, 0, shadowMatrixBuffer);
			}
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to upload light data to GPU buffer!", ex);
			return false;
		}

		return true;
	}

	private bool ResizeLightBuffers()
	{
		if (IsDisposed) return false;

		BufLights?.Dispose();

		if (lightDataBuffer.Length < LightCapacity)
		{
			lightDataBuffer = new LightSourceData[LightCapacity];
		}

		const uint elementByteSize = LightSourceData.packedByteSize;
		uint bufferByteSize = elementByteSize * LightCapacity;

		BufferDescription bufferDesc = new(
			bufferByteSize,
			BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
			elementByteSize);

		try
		{
			BufLights = core.MainFactory.CreateBuffer(ref  bufferDesc);
			BufLights.Name = $"BufLights_Capacity={LightCapacity}";

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create light data buffer!", ex);
			return false;
		}
	}

	private bool ResizeShadowMatrixBuffers()
	{
		if (IsDisposed) return false;

		BufShadowMatrices?.Dispose();

		if (shadowMatrixBuffer.Length < ShadowMatrixCapacity)
		{
			shadowMatrixBuffer = new Matrix4x4[LightCapacity];
			Array.Fill(shadowMatrixBuffer, Matrix4x4.Identity);
		}

		const uint elementByteSize = 4 * sizeof(float);
		uint bufferByteSize = elementByteSize * ShadowMatrixCapacity;

		BufferDescription bufferDesc = new(
			bufferByteSize,
			BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
			elementByteSize);

		try
		{
			BufShadowMatrices = core.MainFactory.CreateBuffer(ref bufferDesc);
			BufShadowMatrices.Name = $"BufShadowMatrices_Capacity={LightCapacity}";

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create shadow projection matrix buffer!", ex);
			return false;
		}
	}

	#endregion
}
