namespace FragEngine3.Graphics.Resources.Materials.Internal;

internal enum MaterialResourceMappingType
{
	BoundResource,
	ConstantBufferValue,
}

internal readonly struct MaterialResourceMapping(MaterialResourceMappingType _mappingType, int _offsetOrIndex)
{
	public readonly MaterialResourceMappingType mappingType = _mappingType;
	public readonly int offsetOrIndex = _offsetOrIndex;

	public override string ToString()
	{
		return $"Type: {mappingType}, Offset/Index: {offsetOrIndex}";
	}
}
