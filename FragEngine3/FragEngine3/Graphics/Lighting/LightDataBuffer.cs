using FragEngine3.EngineCore;
using FragEngine3.Graphics.Lighting.Data;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

public sealed class LightDataBuffer : IDisposable
{
	#region Constructors

	public LightDataBuffer(GraphicsCore _core, uint _initialLightCapacity = 1)
	{
		core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		Capacity = Math.Max(_initialLightCapacity, 1);

		lightDataBuffer = new LightSourceData[Capacity];

		ResizeLightBuffers();
	}

	~LightDataBuffer()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	public event Action? OnRecreatedLightDataBufferEvent = null;

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	private LightSourceData[] lightDataBuffer;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public DeviceBuffer BufLights { get; private set; } = null!;

	public uint Count { get; private set; } = 0;
	public uint Capacity { get; private set; } = 0;

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
	}

	public bool PrepareBufLights(uint _requiredLightCount, out bool _outRecreatedBufLights)
	{
		_outRecreatedBufLights = false;

		if (IsDisposed)
		{
			Logger.LogError("Cannot begin preparing disposed light data buffers for use!");
			return false;
		}

		// BufLights:
		if (Capacity < _requiredLightCount)
		{
			Capacity = _requiredLightCount;
			if (!ResizeLightBuffers())
			{
				Logger.LogError("Failed to prepare BufLights!");
				return false;
			}
			_outRecreatedBufLights = true;
			OnRecreatedLightDataBufferEvent?.Invoke();
		}

		Count = _requiredLightCount;

		return true;
	}

	public bool SetLightData(uint _lightIdx, in LightSourceData _data)
	{
		if (_lightIdx >= Capacity)
		{
			return false;
		}

		lightDataBuffer[_lightIdx] = _data;
		return true;
	}

	public bool FinalizeBufLights(CommandList? _cmdList = null)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot finalize preparing disposed light data buffers for use!");
			return false;
		}

		// BufLights:
		try
		{
			Span<LightSourceData> span = lightDataBuffer.AsSpan(0, (int)Count);
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

		return true;
	}

	private bool ResizeLightBuffers()
	{
		if (IsDisposed) return false;

		BufLights?.Dispose();

		if (lightDataBuffer.Length < Capacity)
		{
			lightDataBuffer = new LightSourceData[Capacity];
		}

		const uint elementByteSize = LightSourceData.packedByteSize;
		uint bufferByteSize = elementByteSize * Capacity;

		BufferDescription bufferDesc = new(
			bufferByteSize,
			BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
			elementByteSize);

		try
		{
			BufLights = core.MainFactory.CreateBuffer(ref  bufferDesc);
			BufLights.Name = $"BufLights_Capacity={Capacity}";

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create light data buffer!", ex);
			return false;
		}
	}

	#endregion
}
