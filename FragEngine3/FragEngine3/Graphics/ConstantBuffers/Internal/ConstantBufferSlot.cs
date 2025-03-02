using FragEngine3.EngineCore;
using FragEngine3.Utility;
using Veldrid;

namespace FragEngine3.Graphics.ConstantBuffers.Internal;

/// <summary>
/// Wrapper around a constant buffer that may be bound to the graphics pipeline for rendering a material.<para/>
/// Use <see cref="SetValue{T}(in T)"/> to update constant values in an internal buffer; buffer contents are uploaded
/// to the GPU-side buffer just before the first draw call using the material is issued.
/// </summary>
public sealed record ConstantBufferSlot : IDisposable
{
	#region Constructors

	private ConstantBufferSlot(GraphicsCore _graphicsCore, DeviceBuffer _constantBuffer, uint _slotIndex, int _byteSize)
	{
		graphicsCore = _graphicsCore;
		constantBuffer = _constantBuffer;
		slotIndex = _slotIndex;
		byteBuffer = new byte[_byteSize];
	}

	~ConstantBufferSlot()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore;
	private readonly DeviceBuffer constantBuffer;

	/// <summary>
	/// The index of the constant buffer slot that this binds to on the graphics pipeline.
	/// </summary>
	public readonly uint slotIndex;
	private readonly byte[] byteBuffer;

	private static ConstantBufferSlot Invalid => new(null!, null!, 0u, 0);

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this object has been disposed already.
	/// </summary>
	public bool IsDisposed { get; private set; } = false;
	/// <summary>
	/// Gets whether the contents of the constant buffer have been changed and are pending for upload to the GPU.
	/// </summary>
	public bool IsDirty { get; private set; } = true;

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
		IsDirty = false;
		constantBuffer?.Dispose();
	}

	/// <summary>
	/// Updates the contents of the constant buffer.<para/>
	/// Note: Values are stored in an intermediate byte buffer first, and are only uploaded to GPU when <see cref="Prepare(out DeviceBuffer)"/> is called.<para/>
	/// Warning: This method does not check whether the generic data type matches the data layout the GPU buffer was created around.
	/// Access violation exceptions may occur if the generic type is larger than the allocated buffer!
	/// </summary>
	/// <typeparam name="T">The CPU-side data type of mirroring the constant buffer's GPU-side data layout.</typeparam>
	/// <param name="_newValue">The new contents of the constant buffer.</param>
	public unsafe void SetValue<T>(in T _newValue) where T : unmanaged
	{
		fixed (byte* pByteBuffer = byteBuffer)
		{
			T* pBuffer = (T*)pByteBuffer;
			*pBuffer = _newValue;
		}
		IsDirty = true;
	}

	/// <summary>
	/// Gets the current contents of the constant buffer.<para/>
	/// Warning: This method does not check whether the generic data type matches the data layout the GPU buffer was created around.
	/// Access violation exceptions may occur if the generic type is larger than the allocated buffer!
	/// </summary>
	/// <typeparam name="T">The CPU-side data type of mirroring the constant buffer's GPU-side data layout.</typeparam>
	/// <returns>The current contents of the constant buffer.</returns>
	public unsafe T GetValue<T>() where T : unmanaged
	{
		T value;
		fixed (byte* pByteBuffer = byteBuffer)
		{
			T* pBuffer = (T*)pByteBuffer;
			value = *pBuffer;
		}
		return value;
	}

	/// <summary>
	/// Prepares the constant buffer for binding to the graphics pipeline.<para/>
	/// This will upload the CB contents from the CPU-side intermediate buffer to the actual GPU buffer.
	/// </summary>
	/// <param name="_outConstantBuffer">Outputs the GPU-side constant buffer that may be bound to the graphics pipeline.</param>
	/// <returns>True if the constant buffer is up-to-date and ready for binding, false otherwise.</returns>
	public bool Prepare(out DeviceBuffer? _outConstantBuffer)
	{
		if (IsDisposed)
		{
			_outConstantBuffer = null;
			return false;
		}

		if (IsDirty)
		{
			IsDirty = false;

			try
			{
				graphicsCore.Device.UpdateBuffer(constantBuffer, 0, byteBuffer);
			}
			catch (Exception ex)
			{
				graphicsCore.graphicsSystem.Engine.Logger.LogException($"Failed to update contents of constant buffer in slot {slotIndex}!", ex);
				_outConstantBuffer = null;
				return false;
			}
		}

		_outConstantBuffer = constantBuffer;
		return true;
	}

	/// <summary>
	/// Tries to create a new constant buffer slot and its underlying buffers and resources.
	/// </summary>
	/// <typeparam name="T">A type that serves as the CPU-side data representation of the constant buffer's data layout. This must be an unmanaged struct.</typeparam>
	/// <param name="_graphicsCore">The graphics core to whose pipelines the constant buffer may be bound.</param>
	/// <param name="_slotIndex">The index of the constant buffer slot that this binds to.</param>
	/// <param name="_outSlot">Outputs a new slot that is ready for use by a material. On failure, an invalid slot is output instead.</param>
	/// <returns>True if the slot and its buffers could be created around the provided data type, false otherwise.</returns>
	public static bool CreateSlot<T>(GraphicsCore _graphicsCore, uint _slotIndex, out ConstantBufferSlot _outSlot) where T : unmanaged => CreateSlot(_graphicsCore, _slotIndex, typeof(T), out _outSlot);

	/// <summary>
	/// Tries to create a new constant buffer slot and its underlying buffers and resources.
	/// </summary>
	/// <param name="_graphicsCore">The graphics core to whose pipelines the constant buffer may be bound.</param>
	/// <param name="_slotIndex">The index of the constant buffer slot that this binds to.</param>
	/// <param name="_dataType">A type that serves as the CPU-side data representation of the constant buffer's data layout. This must be an unmanaged struct.</param>
	/// <param name="_outSlot">Outputs a new slot that is ready for use by a material. On failure, an invalid slot is output instead.</param>
	/// <returns>True if the slot and its buffers could be created around the provided data type, false otherwise.</returns>
	public static bool CreateSlot(GraphicsCore _graphicsCore, uint _slotIndex, Type _dataType, out ConstantBufferSlot _outSlot)
	{
		if (_graphicsCore is null || !_graphicsCore.IsInitialized)
		{
			Logger.Instance?.LogError("Cannot create constant buffer slot using null or uninitialized graphics core!");
			_outSlot = Invalid;
			return false;
		}

		Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

		// Try to determine the maximum byte size of the buffer:
		int byteSize;
		try
		{
			byteSize = StructSizeHelper.GetSizeOfStruct(_dataType);
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to determine size for constant buffer slot!", ex);
			_outSlot = Invalid;
			return false;
		}

		if (byteSize < 4)
		{
			logger.LogError("Failed to determine size for constant buffer slot!");
			_outSlot = Invalid;
			return false;
		}

		// Try to create the actual GPU resource:
		DeviceBuffer constantBuffer;
		try
		{
			BufferDescription cbDesc = new((uint)byteSize, BufferUsage.UniformBuffer);
			constantBuffer = _graphicsCore.MainFactory.CreateBuffer(ref cbDesc);
			constantBuffer.Name = _dataType.Name;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create constant buffer for slot {_slotIndex} and data type '{_dataType.Name}'!", ex);
			_outSlot = Invalid;
			return false;
		}

		// Create the actual slot and return success:
		_outSlot = new(_graphicsCore, constantBuffer, _slotIndex, byteSize);
		return true;
	}

	#endregion
}
