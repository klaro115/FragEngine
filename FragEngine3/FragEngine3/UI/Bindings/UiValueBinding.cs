namespace FragEngine3.UI.Bindings;

public sealed class UiValueBinding<TValue>(TValue _initialValue) : UiBinding<TValue>
{
	#region Properties

	public TValue Value { get; private set; } = _initialValue;

	#endregion
	#region Methods

	public override TValue GetValue() => Value;

	public override bool SetValue(TValue _newValue, UiBindingValueSource _source)
	{
		if (!AllowedSourceMask.HasFlag(_source)) return false;

		Value = _newValue;
		SourceOfLastChange = _source;
		NotifyValueChanged(_newValue, _source);
		return true;
	}

	public override string ToString()
	{
		return $"ValueBinding<{typeof(TValue).Name}> | Value={Value?.ToString() ?? "NULL"}";
	}

	#endregion
}
