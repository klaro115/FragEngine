using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ImageFormats;
using FragEngine3.Graphics.Resources.Import.Utility;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public static class ImageImporter
{
	#region Methods

	public static bool ImportImageData(
		ResourceManager _resourceManager,
		ResourceHandle _handle,
		out RawImageData? _outImageData)
	{
		if (_handle == null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Resource handle for raw image import may not be null or invalid!");
			_outImageData = null;
			return false;
		}
		if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot load raw image using null or disposed resource manager!");
			_outImageData = null;
			return false;
		}

		Logger logger = _handle.resourceManager.engine.Logger ?? Logger.Instance!;

		// Retrieve the file that this resource is loaded from:
		ResourceFileHandle fileHandle;
		if (string.IsNullOrEmpty(_handle.fileKey))
		{
			if (!_handle.resourceManager.GetFileWithResource(_handle.fileKey, out fileHandle))
			{
				logger.LogError($"Could not find any resource data file containing resource handle '{_handle}'!");
				_outImageData = null;
				return false;
			}
		}
		else
		{
			if (!_handle.resourceManager.GetFile(_handle.fileKey, out fileHandle))
			{
				logger.LogError($"Resource data file for resource handle '{_handle}' does not exist!");
				_outImageData = null;
				return false;
			}
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(_resourceManager, _handle.dataOffset, _handle.dataSize, out stream, out _))
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

	public static bool ImportImageData(
		Stream _stream,
		string _formatExt,
		string? _importFlags,
		out RawImageData? _outImageData)
	{
		if (_stream == null || !_stream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import raw image data from null or write-only stream!");
			_outImageData = null;
			return false;
		}
		if (string.IsNullOrWhiteSpace(_formatExt))
		{
			Logger.Instance?.LogError("Cannot import raw image data using unspecified image file format extension!");
			_outImageData = null;
			return false;
		}

		_formatExt = _formatExt.ToLowerInvariant();

		if (_formatExt == ".bmp")
		{
			return BitmapImporter.ImportImage(_stream, out _outImageData);
		}
		else if (_formatExt == ".qoi")
		{
			return QoiImporter.ImportImage(_stream, out _outImageData);
		}
		//...
		else if (MagickImporter.SupportsFormat(_formatExt))
		{
			var importFlags = MagickImporter.ParseImportFlags(_importFlags);
			return MagickImporter.ImportImage(_stream, importFlags, out _outImageData);
		}
		else
		{
			Logger.Instance?.LogError($"Unknown image file format extension '{_formatExt}', cannot import raw image data!");
			_outImageData = null;
			return false;
		}
	}

	#endregion
}
