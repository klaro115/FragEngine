namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public abstract class FbxProperty
{
	#region Constructors

	protected FbxProperty(FbxPropertyType _type, int _valueCount)
	{
		type = _type;
		valueCount = _valueCount;
	}

	#endregion
	#region Fields

	public readonly FbxPropertyType type;
	public readonly int valueCount;

	#region Properties
	#endregion

	public abstract object Value { get; }

	#endregion
}

public sealed class FbxPropertyRaw : FbxProperty
{
	#region Constructors

	public FbxPropertyRaw(byte[] _rawBytes) : base(FbxPropertyType.RawBytes, _rawBytes.Length)
	{
		rawBytes = _rawBytes ?? Array.Empty<byte>();
	}

	#endregion
	#region Fields

	public readonly byte[] rawBytes;

	#endregion
	#region Properties

	public override object Value => rawBytes;

	#endregion
}

public sealed class FbxPropertyArray : FbxProperty
{
	#region Constructors

	public FbxPropertyArray(FbxProperty[] _properties) : base(FbxPropertyType.PropertyArray, _properties.Length)
	{
		properties = _properties;
	}

	#endregion
	#region Fields

	public readonly FbxProperty[] properties;

	#endregion
	#region Properties

	public override object Value => properties;

	#endregion
}

public sealed class FbxProperty<T> : FbxProperty where T : unmanaged
{
	#region Constructors

	public FbxProperty(T _value, FbxPropertyType _primitiveType) : base(_primitiveType, 1)
	{
		value = _value;
	}

	#endregion
	#region Fields

	public readonly T value;

	public readonly List<T>? values = null;

	#endregion
	#region Properties

	public override object Value => value!;

	#endregion
}
