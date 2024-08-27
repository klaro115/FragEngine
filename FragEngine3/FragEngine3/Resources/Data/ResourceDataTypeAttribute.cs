namespace FragEngine3.Resources.Data;

/// <summary>
/// An attribute that shows that a type is used as a serialization format of another type's data.
/// </summary>
/// <param name="resourceType">The type for which the attributed type serves as serializable data representation.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ResourceDataTypeAttribute(Type resourceType) : Attribute
{
	#region Fields

	public readonly Type resourceType = resourceType ?? typeof(object);
	
	#endregion
}
