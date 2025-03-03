using Veldrid;

namespace FragEngine3.Graphics.Utility;

/// <summary>
/// Helper class for creating and updating constant buffers.
/// </summary>
public static class ConstantBufferUtility
{
	#region Methods

	/// <summary>
	/// Tries to update the contents of a constant buffer, or creates a new one first.
	/// </summary>
	/// <typeparam name="T">A type that represents the data layout and contents of the constant buffer.</typeparam>
	/// <param name="_graphicsCore">The graphics core that shall be used for creating and updating the constant buffer.</param>
	/// <param name="_resourceKey">A resource key that uniquely identifies the material for which the constant buffer is created. May not be null.</param>
	/// <param name="_constantBufferByteSize">The maximum size of the constant buffer and its contents, in bytes.</param>
	/// <param name="_constantBuffer">A reference to the constant buffer whose content we wish to update. If null, a new constant buffer is created first.</param>
	/// <param name="_constantBufferData">A reference to the content data that we want to upload to the constant buffer in GPU memory.</param>
	/// <returns>True if the buffer was successfully recreated, and its contents updated, false otherwise.</returns>
	public static bool CreateOrUpdateConstantBuffer<T>(GraphicsCore _graphicsCore, string _resourceKey, uint _constantBufferByteSize, ref DeviceBuffer? _constantBuffer, ref T _constantBufferData) where T : unmanaged
	{
		// Ensure the constant buffer is not null:
		if (_constantBuffer is null)
		{
			BufferDescription bufferDesc = new(_constantBufferByteSize, BufferUsage.UniformBuffer);

			try
			{
				_constantBuffer = _graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				_constantBuffer.Name = $"{typeof(T).Name}_{_resourceKey}";
			}
			catch (Exception ex)
			{
				_graphicsCore.graphicsSystem.Engine.Logger.LogException($"Failed to create constant buffer of type '{typeof(T).Name}'! (Resource key: '{_resourceKey}')", ex);
				return false;
			}
		}

		// Upload new content data to buffer in GPU memory:
		return UpdateConstantBuffer(_graphicsCore, _resourceKey, ref _constantBuffer, ref _constantBufferData);
	}

	/// <summary>
	/// Tries to update the contents of a constant buffer.
	/// </summary>
	/// <typeparam name="T">A type that represents the data layout and contents of the constant buffer.</typeparam>
	/// <param name="_graphicsCore">The graphics core that shall be used for updating the constant buffer.</param>
	/// <param name="_resourceKey">A resource key that uniquely identifies the material that owns the constant buffer. May not be null.</param>
	/// <param name="_constantBuffer">A reference to the constant buffer whose content we wish to update. May not be null.</param>
	/// <param name="_constantBufferData">A reference to the content data that we want to upload to the constant buffer in GPU memory.</param>
	/// <returns>True if the buffer's contents were updated successfully, false otherwise.</returns>
	public static bool UpdateConstantBuffer<T>(GraphicsCore _graphicsCore, string _resourceKey, ref DeviceBuffer _constantBuffer, ref T _constantBufferData) where T : unmanaged
	{
		try
		{
			_graphicsCore.Device.UpdateBuffer(_constantBuffer, 0, ref _constantBufferData);
			return true;
		}
		catch (Exception ex)
		{
			_graphicsCore.graphicsSystem.Engine.Logger.LogException($"Failed to update contents of constant buffer of type '{typeof(T).Name}'! (Resource key: '{_resourceKey}')", ex);
			return false;
		}
	}

	#endregion
}
