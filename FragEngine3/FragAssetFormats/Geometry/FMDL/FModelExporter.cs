using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Export;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Resources;

namespace FragAssetFormats.Geometry.FMDL;

public sealed class FModelExporter : IModelExporter
{
	#region Fields

	private static readonly string[] supportedFormatExtensions = [ ".fmdl" ];

	#endregion
	#region Properties

	// GEOMETRY SUPPORT:

	public MeshVertexDataFlags SupportedVertexData => MeshVertexDataFlags.ALL;

	public bool Supports16BitIndices => true;
	public bool Supports32BitIndices => true;

	// ANIMATION SUPPORT:

	public bool CanExportBlendTargets => false;
	public bool CanExportAnimations => false;
	public bool CanExportMaterials => false;
	public bool CanExportTextures => false;

	#endregion
	#region Methods

	public IReadOnlyCollection<string> GetSupportedFileFormatExtensions() => supportedFormatExtensions;

	public bool ExportShaderData(in ImporterContext _exportCtx, MeshSurfaceData _surfaceData, Stream _outputResourceStream)
	{
		if (_exportCtx is null)
		{
			Console.WriteLine("Error! Cannot write model data using null export context!");
			return false;
		}
		if (_outputResourceStream is null)
		{
			_exportCtx.Logger.LogError("Cannot write model data to null binary writer!");
			return false;
		}
		if (!_outputResourceStream.CanWrite)
		{
			_exportCtx.Logger.LogError("Cannot write model data to read-only stream!");
			return false;
		}

		//TODO: Prepare vertex blocks.
		//TODO: Prepare index blocks.
		//TODO: Calculate block sizes and offsets => update in header.


		FModelHeader fileHeader = new()
		{
			// Format info:
			magicNumbers = FModelHeader.MAGIC_NUMBERS,
			formatVersion = FModelHeader.FormatVersion.Current,

			// Geometry info:
			vertexCount = (uint)_surfaceData.VertexCount,
			triangleCount = (uint)_surfaceData.TriangleCount,
			reserved = 0u,

			// Compression info:
			isVertexDataCompressed = false,
			isIndexDataCompressed = false,
		};

		using BinaryWriter writer = new(_outputResourceStream);

		if (!fileHeader.WriteFmdlHeader(in _exportCtx, writer))
		{
			_exportCtx.Logger.LogError("Failed to write format header of FMDL 3D model to file!");
			return false;
		}

		// TODO: Write block data to file.

		return true;
	}

	public IEnumerator<ResourceHandle> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream)
	{
		yield break;
	}

	#endregion
}
