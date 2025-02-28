using FragEngine3.EngineCore;
using System.Reflection;

namespace FragEngine3.UI.Bindings.Internal;

internal abstract class UiBindingPathPart
{
	#region Properties

	public abstract string PartName { get; }

	#endregion
	#region Methods

	public abstract object? GetValue(object? _hostObject);
	public abstract void SetValue(object? _hostObject, object? _newValue);
	
	#endregion
}

internal sealed class UiBindingPathFieldPart(FieldInfo _field) : UiBindingPathPart
{
	#region Fields

	public readonly FieldInfo field = _field;

	#endregion
	#region Properties

	public override string PartName => field.Name;

	#endregion
	#region Methods

	public override object? GetValue(object? _hostObject) => field.GetValue(_hostObject);
	public override void SetValue(object? _hostObject, object? _newValue) => field.SetValue(_hostObject, _newValue);
	
	#endregion
}

internal sealed class UiBindingPathPropertyPart(PropertyInfo _property) : UiBindingPathPart
{
	#region Fields

	public readonly PropertyInfo property = _property;

	#endregion
	#region Properties

	public override string PartName => property.Name;

	#endregion
	#region Methods

	public override object? GetValue(object? _hostObject)
	{
		if (!property.CanRead)
		{
			Logger.Instance?.LogError($"Error! Cannot get value, property '{property}' is write-only!");
			return null;
		}
		return property.GetValue(_hostObject);
	}
	public override void SetValue(object? _hostObject, object? _newValue)
	{
		if (!property.CanWrite)
		{
			Logger.Instance?.LogError($"Error! Cannot set value, property '{property}' is read-only!");
			return;
		}
		property.SetValue(_hostObject, _newValue);
	}

	#endregion
}
