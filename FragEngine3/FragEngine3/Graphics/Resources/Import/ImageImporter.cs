using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.Utility;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public sealed class ImageImporter(ResourceManager _resourceManager, GraphicsCore _graphicsCore) : BaseResourceImporter<IImageImporter>(_resourceManager, _graphicsCore)
{
	#region Methods

	public bool ImportImageData(
		ResourceManager _resourceManager,
		ResourceHandle _handle,
		out RawImageData? _outImageData)
	{
		if (IsDisposed)
		{
			logger.LogError($"Cannot import image data using disposed {nameof(ImageImporter)}!");
			_outImageData = null;
			return false;
		}

		if (!TryGetResourceFile(_handle, out ResourceFileHandle fileHandle))
		{
			_outImageData = null;
			return false;
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(_resourceManager.engine, _handle.dataOffset, _handle.dataSize, out stream, out _))
			{
				logger.LogError($"Failed to open file stream for resource handle '{_handle}'!");
				_outImageData = null;
				return false;
			}

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(fileHandle.dataFilePath);

			if (!ImportImageData(stream, formatExt, _handle.importFlags, out _outImageData))
			{
				logger.LogError($"Failed to import raw image data for resource handle '{_handle}'!");
				_outImageData = null;
				return false;
			}
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to import raw image data for resource handle '{_handle}'!", ex);
			_outImageData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}

		// Check for further pre-processing instructions in import flags, then return result:
		return ImageImportFlagParser.ApplyImportFlags(_outImageData!, _handle.importFlags);
	}

	public bool ImportImageData(
		Stream _stream,
		string _formatExt,
		string? _importFlags,
		out RawImageData? _outImageData)
	{
		if (_stream is null || !_stream.CanRead)
		{
			logger.LogError("Cannot import raw image data from null or write-only stream!");
			_outImageData = null;
			return false;
		}
		if (string.IsNullOrWhiteSpace(_formatExt))
		{
			logger.LogError("Cannot import raw image data using unspecified image file format extension!");
			_outImageData = null;
			return false;
		}

		_formatExt = _formatExt.ToLowerInvariant();

		if (!importerFormatDict.TryGetValue(_formatExt, out IImageImporter? importer))
		{
			logger.LogError($"Unsupported 2D file format extension '{_formatExt}', cannot import image data!");
			_outImageData = null;
			return false;
		}

		bool success = importer.ImportImageData(in importCtx, _stream, out _outImageData);
		return success;
	}

	#endregion
}
