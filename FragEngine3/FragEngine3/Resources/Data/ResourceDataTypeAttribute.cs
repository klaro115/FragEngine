namespace FragEngine3.Resources.Data;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ResourceDataTypeAttribute(Type resourceType) : Attribute
{
	#region Fields

	public readonly Type resourceType = resourceType ?? typeof(object);
	
	#endregion
}
