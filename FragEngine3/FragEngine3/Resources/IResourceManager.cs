namespace FragEngine3.Resources;

public interface IResourceManager
{
	#region Methods

	bool AddFile(ResourceFileHandle _handle);

	/// <summary>
	/// Registers a new resource, identified by its resource handle, with this resource manager.
	/// </summary>
	/// <param name="_handle">A handle identifying and describing the new resource. Must be non-null
	/// and valid, a resource may not be registered twice.</param>
	/// <returns>True if the given resource handle is valid and was registered successfully, false
	/// otherwise or if another handle with the same resource key already exists.</returns>
	bool AddResource(ResourceHandle _handle);

	#endregion
}
