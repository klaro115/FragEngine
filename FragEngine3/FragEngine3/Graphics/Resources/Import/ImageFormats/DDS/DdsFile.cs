﻿using FragEngine3.EngineCore;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

/// <summary>
/// This type represents the DDS file structure.<para/>
/// Reference Link: https://learn.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide
/// </summary>
public sealed class DdsFile
{
	#region Fields

	public DdsFileHeader fileHeader = new();
	public DdsDxt10Header? dxt10Header = null;

	public byte[] data = [];		// Main surface data.
	public byte[]? data2 = null;	// Additional surface data. (mipmaps, cubemap faces, depth layers, etc.)
	//...

	#endregion
	#region Methods

	public bool IsValid(bool _checkRootLevelOnly = false)
	{
		bool result =
			fileHeader is not null;
		//...

		if (result && !_checkRootLevelOnly)
		{
			result &=
				fileHeader!.IsValid() &&
				(!fileHeader.HasDxt10Header || (dxt10Header is not null && dxt10Header.IsValid()));
			//...
		}
		return result;
	}

	public Format GetSurfaceFormat()
	{
		if (dxt10Header is not null)
		{
			return dxt10Header.dxgiFormat;
		}

		//TODO

		return Format.Unknown;	//TEMP
	}

	public uint CalculatePitch()
	{
		Format format = GetSurfaceFormat();

		// Block-compressed formats:
		if (format.IsBlockCompressed())
		{
			uint blockSize = format.GetCompressionBlockSize();
			return Math.Max((fileHeader.width + 3) / 4, 1) * blockSize;
		}

		// 2-channel-alternating and 4:2:2 sampled formats:
		if (format == Format.R8G8_B8G8_UNorm || format == Format.YUY2) // Note: UYVY is not defined in enum.
		{
			return ((fileHeader.width + 1) >> 1) * 4;
		}

		// Other formats:
		uint bitsPerPixel = (uint)format.GetBitsPerPixel();
		return (fileHeader.width * bitsPerPixel + 7) / 8;
	}

	public static bool Read(BinaryReader _reader, out DdsFile? _outFile)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read DDS file header using null binary reader!");
			_outFile = null;
			return false;
		}

		// Try reading file header:
		if (!DdsFileHeader.Read(_reader, out DdsFileHeader fileHeader))
		{
			Logger.Instance?.LogError("Cannot read DDS file; failed to read file header!");
			_outFile = null;
			return false;
		}

		// Depending on pixel format, read DXT10 header next:
		DdsDxt10Header? dxt10Header = null;
		if (fileHeader.HasDxt10Header && !DdsDxt10Header.Read(_reader, out dxt10Header))
		{
			Logger.Instance?.LogError("Cannot read DDS file; failed to read DXT10 header!");
			_outFile = null;
			return false;
		}

		//TODO

		// Assemble file structure:
		_outFile = new()
		{
			fileHeader = fileHeader,
			dxt10Header = dxt10Header,
			//...
		};

		// Check superficial data validity and return success:
		bool isFileValid = _outFile.IsValid(true);
		return isFileValid;
	}

	public bool Write(BinaryWriter _writer)
	{
		// Check if local data is valid:
        if (!IsValid(false))
        {
			Logger.Instance?.LogError("Cannot write invalid DDS file data to stream!");
			return false;
        }

		// Try writing file header:
		if (!fileHeader.Write(_writer))
		{
			Logger.Instance?.LogError("Cannot write DDS file; failed to write file header!");
			return false;
		}
		
		// If available and needed, write
		if (fileHeader.HasDxt10Header)
		{
			if (dxt10Header is null)
			{
				Logger.Instance?.LogError("Cannot write DDS file; DXT10 header is missing!");
				return false;
			}
			if (!dxt10Header.Write(_writer))
			{
				Logger.Instance?.LogError("Cannot write DDS file; failed to write DXT10 header!");
				return false;
			}
		}

		//TODO

		return true;
    }

	#endregion
}