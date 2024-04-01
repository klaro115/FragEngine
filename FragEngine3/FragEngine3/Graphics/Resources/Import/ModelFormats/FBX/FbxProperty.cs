namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public abstract class FbxProperty(FbxPropertyType _type, int _valueCount)
{
	#region Fields

	public readonly FbxPropertyType type = _type;
	public readonly int valueCount = _valueCount;

	#region Properties
	#endregion

	public abstract object Value { get; }

	#endregion
	#region Methods

	public override string ToString()
	{
		return $"Type: {type}, Count: {valueCount}, Value: {Value?.ToString() ?? "NULL"}";
	}

	#endregion
}

public sealed class FbxPropertyRaw(byte[] _rawBytes) : FbxProperty(FbxPropertyType.RawBytes, _rawBytes!.Length)
{
	#region Fields

	public readonly byte[] rawBytes = _rawBytes ?? Array.Empty<byte>();

	#endregion
	#region Properties

	public override object Value => rawBytes;

	#endregion
}

public sealed class FbxPropertyArray(FbxProperty[] _properties) : FbxProperty(FbxPropertyType.PropertyArray, _properties.Length)
{
	#region Fields

	public readonly FbxProperty[] properties = _properties;

	#endregion
	#region Properties

	public override object Value => properties;

	#endregion
}

public sealed class FbxProperty<T>(T _value, FbxPropertyType _primitiveType) : FbxProperty(_primitiveType, 1) where T : unmanaged
{
	#region Fields

	public readonly T value = _value;

	public readonly List<T>? values = null;

	#endregion
	#region Properties

	public override object Value => value!;

	#endregion
}
