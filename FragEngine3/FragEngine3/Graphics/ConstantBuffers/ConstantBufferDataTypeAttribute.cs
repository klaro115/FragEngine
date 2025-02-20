namespace FragEngine3.Graphics.ConstantBuffers;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class ConstantBufferDataTypeAttribute(ConstantBufferType _constantBufferType) : Attribute
{
	#region Fields

	public readonly ConstantBufferType constantBufferType = _constantBufferType;

	#endregion
}
