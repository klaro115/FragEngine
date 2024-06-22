using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public static class DdsImporter
{
	#region Methods

	public static bool ImportImage(Stream _byteStream, out TextureData? _outTextureData)
	{
		if (_byteStream == null)
		{
			Logger.Instance?.LogError("Cannot import DDS texture from null stream!");
			_outTextureData = null;
			return false;
		}
		if (!_byteStream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import DDS texture from write-only stream!");
			_outTextureData = null;
			return false;
		}

		using BinaryReader reader = new(_byteStream);

		// Try reading the file's structure and data from stream:
		if (!DdsFile.Read(reader, out DdsFile? file) || file is null)
		{
			Logger.Instance?.LogError("Failed to read DDS file structure and contents!");
			_outTextureData = null;
			return false;
		}

		// Try parsing header data to get a consistent description of the texture:
		if (!DdsTextureDescription.TryCreateDescription(file.fileHeader, file.dxt10Header, out DdsTextureDescription desc))
		{
			_outTextureData = null;
			return false;
		}

		// Assemble texture data description:
		_outTextureData = new()
		{
			width = desc.width,
			height = desc.height,
			depth = desc.depth,
			arraySize = desc.arraySize,
			isCubemap = file.dxt10Header is not null && file.dxt10Header.IsTextureCube,
			channelCount = desc.channelCount,
			isSRgb = desc.isRgbColorSpace && !desc.isLinearColorSpace,
			mipmaps = (byte)desc.mipMapCount,
			bitsPerPixel = desc.pixelByteSize,

			pixelFormat = desc.pixelFormat,
			dxgiFormat = desc.dxgiFormat,
		};

		// Read pixel data of first/main resource:
		uint totalByteSize = desc.CalculateTotalByteSize();
		_outTextureData.pixelData = new byte[totalByteSize];

		try
		{
			reader.Read(_outTextureData.pixelData, 0, (int)totalByteSize);
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read pixel data of DDS texture!", ex);
			return false;
		}

		return true;
	}

	#endregion
}
