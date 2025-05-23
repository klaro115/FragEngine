﻿using System.IO.Compression;
using System.IO.Hashing;
using FragEngine3.EngineCore;
using FragEngine3.Resources.Data;

namespace FragEngine3.Resources;

/// <summary>
/// Handle type for referencing data files from which resources' data may be loaded.<para/>
/// Each resource file may contain one or more resources, with single-resource files often being uncompressed
/// immediate asset files using standard file formats (ex.: *.OBJ for 3D models, *.HLSL for shaders). The
/// resource file is identified by an accompanying resource descriptor file, which is a JSON-serialized object
/// of type '<see cref="ResourceFileData"/>', which contains a list of all resources contained within a data
/// file.<para/>
/// There are 3 major types of resource files: single-resource files, compressed batch files, and block-compressed
/// batch files. The first will contain only one resource and the asset type can generally be derived from the
/// asset file's extension. Batched asset files are compressed archives of one or more resources, with a regular
/// contiguously compressed variant, and a block-compressed variant. Block-compression allows for more efficient
/// random access to resources located within the file, whilst contiguous compression may offer better compressed
/// size while being perfectly suitable for interdependent groups of resources. Cached decompressed file data may
/// be discarded by calling '<see cref="Unload"/>'.
/// </summary>
public sealed class ResourceFileHandle : IEquatable<ResourceFileHandle>
{
	#region Constructors

	public ResourceFileHandle(ResourceFileData _data, ResourceSource _dataFileSource, string _resourceFilePath)
	{
		if (_data == null) throw new ArgumentNullException(nameof(_data), "Resource file handle data may not be null!");

		resourceFilePath = _resourceFilePath ?? throw new ArgumentNullException(nameof(_resourceFilePath), "Resource file path may not be null!");
		dataFilePath = _data.DataFilePath;
		dataFileType = _data.DataFileType;
		dataFileSource = _dataFileSource;
		dataFileSize = _data.DataFileSize;

		uncompressedFileSize = _data.UncompressedFileSize;
		blockSize = _data.BlockSize;
		blockCount = _data.BlockCount;
		
		if (_data.Resources != null)
		{
			int resourceCount = Math.Min(_data.ResourceCount, _data.Resources.Length);
			string[] resourceKeys = new string[resourceCount];
			for (int i = 0; i < resourceCount; i++)
			{
				resourceKeys[i] = _data.Resources[i].ResourceKey ?? string.Empty;
			}
			resources = resourceKeys;
		}
		else
		{
			resources = none.resources;
		}
	}

	private ResourceFileHandle()
	{
		resourceFilePath = string.Empty;
		dataFilePath = string.Empty;
		dataFileType = ResourceFileType.None;
		dataFileSource = ResourceSource.Runtime;
		dataFileSize = 0;

		uncompressedFileSize = 0;
		blockSize = 0;
		blockCount = 0;

		resources = [];
	}

	#endregion
	#region Fields

	// File details:
	public readonly string resourceFilePath = string.Empty;
	public readonly string dataFilePath = string.Empty;
	public readonly ResourceFileType dataFileType = ResourceFileType.Single;
	public readonly ResourceSource dataFileSource;
	public readonly ulong dataFileSize;
	public readonly ulong dataFileHash;	// xxHash3 checksum as a big-endian 64-bit number.

	// Compression details:
	public readonly ulong uncompressedFileSize;
	public readonly ulong blockSize;
	public readonly uint blockCount;

	// Content details:
	public readonly string[] resources;
	private byte[]? dataBuffer = null;

	private static readonly ResourceFileHandle none = new();

	#endregion
	#region Properties

	public bool IsValid => !string.IsNullOrEmpty(resourceFilePath) && !string.IsNullOrEmpty(dataFilePath) && dataFileSize != 0 && ResourceCount != 0;
	public bool IsLoaded => LoadState == ResourceLoadState.Loaded;

	public int ResourceCount => resources != null ? resources.Length : 0;

	public ResourceLoadState LoadState { get; internal set; } = ResourceLoadState.NotLoaded;

	public string Key => resourceFilePath;

	/// <summary>
	/// Gets an invalid empty resource file handle. This should be used instead of null, to prevent null errors as much as possible.
	/// </summary>
	public static ResourceFileHandle None => none;

	#endregion
	#region Methods

	public void Unload()
	{
		LoadState = ResourceLoadState.NotLoaded;
		dataBuffer = null;
	}

	/// <summary>
	/// Tries to open a file stream for reading the raw byte data from the resource file this resource is loaded from.
	/// </summary>
	/// <param name="_engine">The engine instance for which the data is read.</param>
	/// <param name="_dataOffset">The byte offset from the start of the resource file.</param>
	/// <param name="_dataSize">The (compressed) byte size of the resource's data in the file.</param>
	/// <param name="_outStream">Outputs a stream for reading resource data from. This stream must be closed and disposed by the caller.</param>
	/// <param name="_outStreamLength">Outputs the maximum number of bytes that may be read from the stream.</param>
	/// <returns>True if a stream could be opened, false if the file couldn't be accessed, or if decompression failed.</returns>
	/// <exception cref="NotImplementedException">Block-compression is not implemented yet.</exception>
	public bool TryOpenDataStream(Engine _engine, ulong _dataOffset, ulong _dataSize, out Stream _outStream, out ulong _outStreamLength)
	{
		if (LoadState == ResourceLoadState.Loaded && dataBuffer is not null)
		{
			_outStream = new MemoryStream(dataBuffer);
			_outStreamLength = 0;
			return true;
		}

		// Retarget data file to 
		bool pathWasAdjusted = GetPlatformAdjustedDataFilePath(_engine, out string adjustedFilePath);
		string actualDataFilePath = pathWasAdjusted ? adjustedFilePath : dataFilePath;

		if (!File.Exists(actualDataFilePath))
		{
			Logger.Instance?.LogError($"Resource data file '{actualDataFilePath}' does not exist!");
			_outStream = null!;
			_outStreamLength = 0;
			return false;
		}

		switch (dataFileType)
		{
			case ResourceFileType.Single:
				{
					_outStream = File.OpenRead(actualDataFilePath);
					_outStreamLength = (ulong)_outStream.Length;
					return true;
				}
			case ResourceFileType.Batch_Compressed:
				{
					using FileStream compressedStream = File.OpenRead(actualDataFilePath);
					using DeflateStream decompressedStream = new(compressedStream, CompressionMode.Decompress);
					using MemoryStream resultStream = new();

					decompressedStream.CopyTo(resultStream);
					dataBuffer = resultStream.ToArray();

					LoadState = ResourceLoadState.Loaded;

					_outStream = new MemoryStream(dataBuffer, (int)_dataOffset, (int)_dataSize);
					_outStreamLength = _dataSize;
					return true;
				}
			case ResourceFileType.Batch_BlockCompressed:
				{
					throw new NotImplementedException("Resource block compression is not supported at this time.");
				}
			default:
				break;
		}

		_outStream = null!;
		_outStreamLength = 0;
		return false;
	}

	/// <summary>
	/// Try to read all of a resoure's raw byte data from the data file.<para/>
	/// If a compressed file is already loaded and in memory, this is a very fast copy operation. In all other cases,
	/// the resource data is read from file and decompressed as required.
	/// </summary>
	/// <param name="_handle">A resource handle describing the resource whose data we wish to read.</param>
	/// <param name="_outBytes">Outputs an array of byte data from which the resource may be loaded. Empty on failure.</param>
	/// <param name="_outByteCount">Outputs the total number of bytes that hold the requested resource data.</param>
	/// <returns>True if the resource's byte data could be read, false otherwise.</returns>
	public bool TryReadResourceBytes(ResourceHandle _handle, out byte[] _outBytes, out int _outByteCount)
	{
		if (_handle?.resourceManager is null)
		{
			Logger.Instance?.LogError("Cannot read data bytes for null resource handle!");
			_outBytes = [];
			_outByteCount = 0;
			return false;
		}

		// For batched data files, check if the resource handle's source position is valid:
		if (dataFileType == ResourceFileType.Batch_Compressed ||
			dataFileType == ResourceFileType.Batch_BlockCompressed)
		{
			ulong totalDataSize = LoadState == ResourceLoadState.Loaded && dataBuffer is not null
				? (ulong)dataBuffer.Length
				: uncompressedFileSize;

			if (_handle.dataOffset + _handle.dataSize > totalDataSize)
			{
				Logger.Instance?.LogError($"Data bytes offset and size for resource handle '{_handle}' are out-of-bounds!");
				_outBytes = [];
				_outByteCount = 0;
				return false;
			}
		}

		// If uncompressed file data is already loaded and ready to go, copy from there:
		if (LoadState == ResourceLoadState.Loaded && dataBuffer is not null)
		{
			_outBytes = new byte[_handle.dataSize];
			_outByteCount = (int)_handle.dataSize;

			dataBuffer.CopyTo(_outBytes, (int)_handle.dataOffset);
			return true;
		}

		// Try reading all resource data via a stream:
		Stream? stream = null;
		try
		{
			if (!TryOpenDataStream(_handle.resourceManager.engine, _handle.dataOffset, _handle.dataSize, out stream, out ulong streamLength))
			{
				Logger.Instance?.LogError($"Failed to open data stream of file handle '{Key}' at data offset {_handle.dataOffset} and data size {_handle.dataSize}!");
				_outBytes = [];
				_outByteCount = 0;
				return false;
			}

			// Write stream output to byte buffer:
			ulong expectedDataSize;
			if (streamLength != 0)
			{
				expectedDataSize = streamLength;
			}
			else if (dataFileType == ResourceFileType.Single && dataFileSize != 0)
			{
				expectedDataSize = dataFileSize;
			}
			else
			{
				expectedDataSize = _handle.dataSize;
			}

			_outBytes = new byte[expectedDataSize];
			_outByteCount = stream.Read(_outBytes, 0, (int)expectedDataSize);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to open data stream of file handle '{Key}' and read bytes for resource handle '{_handle}'!", ex);
			_outBytes = [];
			_outByteCount = 0;
			return false;
		}
		finally
		{
			stream?.Close();
		}
	}

	public bool GetPlatformAdjustedDataFilePath(Engine _engine, out string _outAdjustedPath)
	{
		if (string.IsNullOrEmpty(dataFilePath))
		{
			_outAdjustedPath = string.Empty;
			return false;
		}
		if (_engine is null)
		{
			Logger.Instance?.LogError("Cannot get platform adjusted data file path using null engine reference!");
			_outAdjustedPath = string.Empty;
			return false;
		}
		if (dataFileType != ResourceFileType.Single ||
			ResourceCount != 1 ||
			!_engine.ResourceManager.GetResource(resources[0], out ResourceHandle resHandle))
		{
			_outAdjustedPath = dataFilePath;
			return false;
		}
		
		PlatformSystem platformSystem = _engine.PlatformSystem;
		ResourceType resourceType = resHandle.resourceType;

		switch (resourceType)
		{
			case ResourceType.Unknown:
			case ResourceType.Ignored:
				_outAdjustedPath = string.Empty;
				return false;
			case ResourceType.Shader:
				{
					// No extension? File format cannot be identified:
					string? fileExtension = Path.GetExtension(dataFilePath)?.ToLowerInvariant();
					if (string.IsNullOrEmpty(fileExtension))
					{
						Logger.Instance?.LogError($"Could not adjust file format extension for data file '{dataFilePath}' of shader resource '{Key}'!");
						_outAdjustedPath = string.Empty;
						return false;
					}

					// FSHA will handle platform-specific import automatically:
					if (fileExtension == ".fsha")
					{
						_outAdjustedPath = dataFilePath;
						return true;
					}

					// Swap extension to API-specific shading language:
					EnginePlatformFlag graphicsFlag = platformSystem.PlatformFlags.GetGraphicsFlags();
					string platformFileExtension = graphicsFlag switch
					{
						EnginePlatformFlag.GraphicsAPI_D3D => ".hlsl",
						EnginePlatformFlag.GraphicsAPI_Vulkan => ".spv",
						EnginePlatformFlag.GraphicsAPI_Metal => ".metal",
						_ => string.Empty,
					};
					_outAdjustedPath = Path.ChangeExtension(dataFilePath, platformFileExtension);
					return true;
				}
			default:
				_outAdjustedPath = dataFilePath;
				return true;
		}

		// Adjust path to use the resource's most likely platform-specific extension:
		//return platformSystem.AdjustForPlatformSpecificFileExtension(resourceType, dataFilePath, out _outAdjustedPath);
	}

	/// <summary>
	/// Gets a serializable description object through which a file handle may be represented in save data.
	/// </summary>
	/// <param name="_resourceManager">The resource manager in which this file handle was registered.</param>
	/// <param name="_outData">Outputs a data object that may be serialized into a "*.fres" resource file,
	/// or null, if the data generation fails.</param>
	/// <returns>True if resource file data was generated successfully, false otherwise.</returns>
	public bool GetResourceFileData(ResourceManager _resourceManager, out ResourceFileData? _outData)
	{
		if (_resourceManager is null || _resourceManager.IsDisposed)
		{
			_outData = null;
			return false;
		}

		ResourceHandleData[] resourceData = new ResourceHandleData[ResourceCount];
		for (int i = 0; i < ResourceCount; ++i)
		{
			if (!_resourceManager.GetResource(resources[i], out ResourceHandle handle) || !handle.GetResourceHandleData(out resourceData[i]))
			{
				_outData = null;
				return false;
			}
		}

		_outData = new()
		{
			DataFilePath = dataFilePath,
			DataFileType = dataFileType,
			DataFileSize = dataFileSize,

			UncompressedFileSize = uncompressedFileSize,
			BlockSize = blockSize,
			BlockCount = blockCount,

			ResourceCount = ResourceCount,
			Resources = resourceData,
		};
		return true;
	}

	/// <summary>
	/// Tries to calculate a hash checksum from the data file using the xxHash3 algorithm. Note that this is a slow-ish process, because the
	/// entire data file has to be read into memory, and a hash calculated from it.<para/>
	/// HASH ALGORITHM: xxHash3 on GitHub: https://github.com/Cyan4973/xxHash<para/>
	/// IMPLEMENTATION: https://learn.microsoft.com/en-us/dotnet/api/system.io.hashing.xxhash3?view=net-8.0
	/// </summary>
	/// <param name="_dataFilePath">File path of the data file we want to hash.</param>
	/// <param name="_outHashValue">Outputs a 64-bit integer containing the calculated hash value.</param>
	/// <returns>True if a hash could be calculated for the given data file, false otherwise.</returns>
	public static bool CalculateDataFileHash(string _dataFilePath, out ulong _outHashValue, out ulong _outFileSize)
	{
		// Verify parameters:
		if (string.IsNullOrEmpty(_dataFilePath))
		{
			Logger.Instance?.LogError("Cannot calculate resource data file hash using null or blank file path!");
			_outHashValue = 0;
			_outFileSize = 0;
			return false;
		}
		if (!File.Exists(_dataFilePath))
		{
			Logger.Instance?.LogError($"Cannot calculate resource data file hash; file path does not exist! File path: '{_dataFilePath}'");
			_outHashValue = 0;
			_outFileSize = 0;
			return false;
		}

		// Read the entire data file into memory:
		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(_dataFilePath);
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to read data file while calculating resource file hash! File path: '{_dataFilePath}'", ex);
			_outHashValue = 0;
			_outFileSize = 0;
			return false;
		}

		// Calculate hash value and return success:
		_outHashValue = XxHash3.HashToUInt64(bytes.AsSpan());
		_outFileSize = (ulong)bytes.Length;
		return true;
	}

	/// <summary>
	/// Calculates a hash checksum for the data file, then compares it against the value in <see cref="dataFileHash"/>.
	/// </summary>
	/// <returns>True if the data file's hash could be calculated and matched ours, false otherwise.</returns>
	public bool VerifyDataFileHash()
	{
		if (!CalculateDataFileHash(dataFilePath, out ulong calculatedHash, out _))
		{
			return false;
		}

		// Compare hashes and return result:
		return calculatedHash == dataFileHash;
	}

	public bool Equals(ResourceFileHandle? other) => ReferenceEquals(this, other) || string.CompareOrdinal(other?.Key, Key) == 0;
	public override bool Equals(object? obj) => obj is ResourceFileHandle other && Equals(other);
	public override int GetHashCode() => base.GetHashCode();

	public override string ToString()
	{
		return $"{dataFilePath ?? "NULL"} ({dataFileType}, {LoadState}) [Size: {uncompressedFileSize}, BS: {blockSize}, BC: {blockCount}]";
	}

	#endregion
}
