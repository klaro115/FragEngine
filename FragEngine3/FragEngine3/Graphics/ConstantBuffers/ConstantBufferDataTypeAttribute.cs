namespace FragEngine3.Graphics.ConstantBuffers;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class ConstantBufferDataTypeAttribute(ConstantBufferType _constantBufferType, uint _byteSize) : Attribute
{
	#region Fields

	public readonly ConstantBufferType constantBufferType = _constantBufferType;
	public readonly uint byteSize = _byteSize;

	#endregion
}
