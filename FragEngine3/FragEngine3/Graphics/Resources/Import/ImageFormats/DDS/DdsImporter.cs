using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public static class DdsImporter
{
	#region Methods

	public static bool ImportImage(Stream _byteStream, out RawImageData? _outRawImage)
	{
		if (_byteStream == null)
		{
			Logger.Instance?.LogError("Cannot import DDS texture from null stream!");
			_outRawImage = null;
			return false;
		}
		if (!_byteStream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import DDS texture from write-only stream!");
			_outRawImage = null;
			return false;
		}

		using BinaryReader reader = new(_byteStream);

		// Try reading the file's structure and data from stream:
		if (!DdsFile.Read(reader, out DdsFile? file) || file is null)
		{
			Logger.Instance?.LogError("Failed to read DDS file structure and contents!");
			_outRawImage = null;
			return false;
		}

		// Try parsing header data to get a consistent description of the texture:
		if (!DdsTextureDescription.TryCreateDescription(file.fileHeader, file.dxt10Header, out DdsTextureDescription desc))
		{
			_outRawImage = null;
			return false;
		}

		_outRawImage = new()
		{
			width = desc.width,
			height = desc.height,
			channelCount = desc.channelCount,
			isSRgb = desc.isRgbColorSpace && !desc.isLinearColorSpace,
			mipmaps = (byte)desc.mipMapCount,
			bitsPerPixel = desc.pixelByteSize,
		};

		//TODO 1: Parse pixel data.
		//TODO 2: Add import support for block-compressed textures.

		return true;
	}

	#endregion
}
