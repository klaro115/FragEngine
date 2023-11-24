using FragEngine3.Resources;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import
{
    public static class ModelImporter
	{
		#region Methods

		public static bool ImportModelData(
			ResourceHandle _handle,
			out MeshSurfaceData? _outSurfaceData
			/* out ... */)
		{
			if (_handle == null || !_handle.IsValid)
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
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				logger.LogError("Could not find source file for resource handle '{_handle}'!");
				_outSurfaceData = null;
				return false;
			}

			Stream stream = null!;
			try
			{
				// Open file stream:
				if (!fileHandle.TryOpenDataStream(_handle.fileOffset, _handle.fileSize, out stream))
				{
					logger.LogError("Failed to open file stream for resource handle '{_handle}'!");
					_outSurfaceData = null;
					return false;
				}

				// Import from stream, identifying file format from extension:
				string formatExt = Path.GetExtension(_handle.Key);

				return ImportModelData(stream, formatExt, out _outSurfaceData);
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
		}

		public static bool ImportModelData(
			Stream _stream,
			string _formatExt,
			out MeshSurfaceData? _outSurfaceData
			/* out ... */)
		{
			if (_stream == null || !_stream.CanRead)
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

			bool useFullVertexDef = _surfaceData.HasExtendedVertexData;

			//TODO: Determine which mesh type to create, based on which data was provided.

			// Create static mesh:
			_outMesh = new StaticMesh(_handle, _core, useFullVertexDef);

			bool success = true;

			// Set vertex data:
			success &= _outMesh.SetBasicGeometry(_surfaceData.verticesBasic);
			if (useFullVertexDef)
			{
				success &= _outMesh.SetExtendedGeometry(_surfaceData.verticesExt!);
			}
			if (!success)
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
}

