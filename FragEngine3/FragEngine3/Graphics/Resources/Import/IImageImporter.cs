using FragEngine3.Graphics.Resources.Data;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface for all importer types that can read and process images or textures from file.
/// </summary>
public interface IImageImporter : IGraphicsResourceImporter
{
	#region Properties

	/// <summary>
	/// Gets the minimum supported bit depth per color channel.
	/// </summary>
	public uint MinimumBitDepth { get; }
	/// <summary>
	/// Gets the maximum supported bit depth per color channel.
	/// </summary>
	public uint MaximumBitDepth { get; }

	#endregion
	#region Methods

	bool ImportImageData(in ImporterContext _importCtx, Stream _byteStream, out RawImageData? _outRawImage);

	#endregion
}
