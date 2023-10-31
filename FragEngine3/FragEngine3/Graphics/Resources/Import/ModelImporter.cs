using FragEngine3.Resources;
using FragEngine3.Graphics.Data;

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
				Console.WriteLine("Error! Resource handle for model import may not be null or invalid!");
				_outSurfaceData = null;
				return false;
			}
			if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
			{
				Console.WriteLine("Error! Cannot load model using null or disposed resource manager!");
				_outSurfaceData = null;
				return false;
			}

			// Retrieve the file that this resource is loaded from:
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				Console.WriteLine($"Error! Could not find source file for resource handle '{_handle}'!");
				_outSurfaceData = null;
				return false;
			}

			Stream stream = null!;
			try
			{
				// Open file stream:
				if (!fileHandle.TryOpenDataStream(_handle.fileOffset, _handle.fileSize, out stream))
				{
					Console.WriteLine($"Error! Failed to open file stream for resource handle '{_handle}'!");
					_outSurfaceData = null;
					return false;
				}

				// Import from stream, identifying file format from extension:
				string formatExt = Path.GetExtension(_handle.Key);

				return ImportModelData(stream, formatExt, out _outSurfaceData);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to import model data for resource handle '{_handle}'!\nException type: '{ex.GetType()}'\nExcepion message: '{ex.Message}'");
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
				Console.WriteLine("Error! Cannot import model data from null or write-only stream!");
				_outSurfaceData = null;
				return false;
			}
			if (string.IsNullOrWhiteSpace(_formatExt))
			{
				Console.WriteLine("Error! Cannot import model data using unspecified 3D file format extension!");
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
				Console.WriteLine($"Error! Unknown 3D file format extension '{_formatExt}', cannot import model data!");
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
				Console.WriteLine($"Error! Failed to set vertex data on mesh for resource '{_handle}'!");
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
				Console.WriteLine($"Error! Failed to set indeex data on mesh for resource '{_handle}'!");
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

