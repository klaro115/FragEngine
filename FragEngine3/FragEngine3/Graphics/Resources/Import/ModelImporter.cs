﻿using FragEngine3.Resources;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import.ModelFormats;

namespace FragEngine3.Graphics.Resources.Import;

public static class ModelImporter
{
	#region Methods

	public static bool ImportModelData(
		ResourceManager _resourceManager,
		ResourceHandle _handle,
		out MeshSurfaceData? _outSurfaceData
		/* out ... */)
	{
		if (_handle is null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Resource handle for model import may not be null or invalid!");
			_outSurfaceData = null;
			return false;
		}
		if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot load model using null or disposed resource manager!");
			_outSurfaceData = null;
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
				_outSurfaceData = null;
				return false;
			}
		}
		else
		{
			if (!_handle.resourceManager.GetFile(_handle.fileKey, out fileHandle))
			{
				logger.LogError($"Resource data file for resource handle '{_handle}' does not exist!");
				_outSurfaceData = null;
				return false;
			}
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(_resourceManager.engine, _handle.dataOffset, _handle.dataSize, out stream, out _))
			{
				logger.LogError($"Failed to open file stream for resource handle '{_handle}'!");
				_outSurfaceData = null;
				return false;
			}

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(fileHandle.dataFilePath);

			if (!ImportModelData(stream, formatExt, out _outSurfaceData))
			{
				logger.LogError($"Failed to import model data for resource handle '{_handle}'!");
				_outSurfaceData = null;
				return false;
			}
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to import model data for resource handle '{_handle}'!", ex);
			_outSurfaceData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}

		// Check for further pre-processing instructions in import flags:
		if (_outSurfaceData is not null && !string.IsNullOrEmpty(_handle.importFlags))
		{
			// Flip triangle vertex order, optinally flip normals and tangents: (turns the surfaces inside-out)
			if (_handle.importFlags.Contains(ImportFlagsConstants.MOD_FLIP_VERTEX_ORDER, StringComparison.Ordinal))
			{
				bool flipNormals = _handle.importFlags.Contains(ImportFlagsConstants.MOD_FLIP_NORMALS, StringComparison.Ordinal);
				bool flipTangents = _handle.importFlags.Contains(ImportFlagsConstants.MOD_FLIP_TANGENTS, StringComparison.Ordinal);

				_outSurfaceData.ReverseVertexOrder(flipNormals, flipTangents);
			}
		}
		return true;
	}

	public static bool ImportModelData(
		Stream _stream,
		string _formatExt,
		out MeshSurfaceData? _outSurfaceData
		/* out ... */)
	{
		if (_stream is null || !_stream.CanRead)
		{
			Logger.Instance?.LogError("Cannot import model data from null or write-only stream!");
			_outSurfaceData = null;
			return false;
		}
		if (string.IsNullOrWhiteSpace(_formatExt))
		{
			Logger.Instance?.LogError("Cannot import model data using unspecified 3D file format extension!");
			_outSurfaceData = null;
			return false;
		}

		_formatExt = _formatExt.ToLowerInvariant();

		if (_formatExt == ".obj")
		{
			return ObjImporter.ImportModel(_stream, out _outSurfaceData);
		}
		else if (_formatExt == ".fbx")
		{
			return FbxImporter.ImportModel(_stream, out _outSurfaceData);
		}
		//...
		else
		{
			Logger.Instance?.LogError($"Unknown 3D file format extension '{_formatExt}', cannot import model data!");
			_outSurfaceData = null;
			return false;
		}
	}

	public static bool CreateMesh(
		in ResourceHandle _handle,
		in GraphicsCore _core,
		in MeshSurfaceData _surfaceData,
		/* in ... */
		out Mesh? _outMesh)
	{
		if (_handle == null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Resource handle for mesh creation may not be null or invalid!");
			_outMesh = null;
			return false;
		}

		Logger logger = _handle.resourceManager.engine.Logger ?? Logger.Instance!;

		// Create mesh instance:
		_outMesh = new Mesh(_handle, _core);

		bool success = true;

		// Set vertex data:
		if (!_outMesh.SetVertexData(_surfaceData.verticesBasic, _surfaceData.verticesExt))
		{
			logger.LogError($"Failed to set vertex data on mesh for resource '{_handle}'!");
			goto abort;
		}

		// Set indices:
		if (_surfaceData.IndexFormat == Veldrid.IndexFormat.UInt16)
		{
			success &= _outMesh.SetIndexData(_surfaceData.indices16!);
		}
		else
		{
			success &= _outMesh.SetIndexData(_surfaceData.indices32!);
		}
		if (!success)
		{
			logger.LogError($"Failed to set indeex data on mesh for resource '{_handle}'!");
			goto abort;
		}

		// Return success and check if mesh is ready to go:
		return success && _outMesh.IsInitialized;

	abort:
		_outMesh?.Dispose();
		return false;
	}

	#endregion
}

