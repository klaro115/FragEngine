namespace FragAssetFormats.Geometry.FBX;

internal abstract class FbxProperty(FbxPropertyType _type, int _valueCount)
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

internal sealed class FbxPropertyRaw(byte[] _rawBytes) : FbxProperty(FbxPropertyType.RawBytes, _rawBytes != null ? _rawBytes.Length : 0)
{
	#region Fields

	public readonly byte[] rawBytes = _rawBytes ?? [];

	#endregion
	#region Properties

	public override object Value => rawBytes;

	#endregion
	#region Methods

	public override string ToString() => $"byte[{valueCount}]";

	#endregion
}

internal sealed class FbxPropertyString(string _text) : FbxProperty(FbxPropertyType.String, _text != null ? _text.Length : 0)
{
	#region Fields

	public readonly string text = _text ?? string.Empty;

	#endregion
	#region Properties

	public override object Value => text;

	#endregion
	#region Methods

	public override string ToString() => $"\"{text}\"";

	#endregion
}

internal sealed class FbxPropertyArray<T>(T[] _values) : FbxProperty(FbxPropertyType.PropertyArray, _values.Length) where T : unmanaged
{
	#region Fields

	public readonly T[] values = _values;

	#endregion
	#region Properties

	public override object Value => values;

	#endregion
	#region Methods

	public override string ToString() => $"{typeof(T).Name}[{valueCount}]";

	#endregion
}

internal sealed class FbxProperty<T>(T _value, FbxPropertyType _primitiveType) : FbxProperty(_primitiveType, 1) where T : unmanaged
{
	#region Fields

	public readonly T value = _value;

	public readonly List<T>? values = null;

	#endregion
	#region Properties

	public override object Value => value!;

	#endregion
	#region Methods

	public override string ToString() => $"{typeof(T).Name}={value}";

	#endregion
}
