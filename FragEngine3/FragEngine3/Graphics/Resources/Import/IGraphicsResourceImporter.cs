namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Interface for all importer types that can read and process graphics resources from file.
/// </summary>
public interface IGraphicsResourceImporter
{
	#region Methods

	/// <summary>
	/// Gets a read-only collection of all file formats that are supported by this importer, by their file extension.
	/// </summary>
	/// <returns>A collection of file extensions. All entries are lower-case and include the leading period.</returns>
	IReadOnlyCollection<string> GetSupportedFileFormatExtensions();

	/// <summary>
	/// Gets an enumerator for iterating over all subresources that are bundled within a resource file.<para/>
	/// NOTE: The order of iteration is always from lowest to highest number of dependencies, meaning that the main
	/// resource of a file will always be returned last, and any bundled textures or animations will be returned first.
	/// </summary>
	/// <param name="_importCtx">The importer context that provides instructions on how to read and process resource
	/// data.</param>
	/// <param name="_resourceFileStream">A stream from which the resource may be read. The stream's current position
	/// must be at the start of the resource file data when calling this method.</param>
	/// <param name="_resourceKeyBase">The base string upon which the resource keys of all resources in this resource file are based.
	/// The expected naming format is: <code>key = "&lt;resourceKeyBase&gt;_&lt;subResourceName&gt;"</code></param>
	/// <param name="_fileExtension">Optional. The file extension of the resource file that is being parsed. Some importers may require this to identify the exact file format.</param>
	/// <returns>An enumerator for iterating over sub- and main resources contained within a resource file's stream.</returns>
	IEnumerator<string> EnumerateSubresources(ImporterContext _importCtx, Stream _resourceFileStream, string _resourceKeyBase, string? _fileExtension = null);

	#endregion
}
