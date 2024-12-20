namespace FragEngine3.UI.Bindings;

public delegate TValue FuncDelegateBindingGetter<TValue>();
public delegate void FuncDelegateBindingSetter<TValue>(TValue _newValue, UiBindingValueSource _source);

public sealed class UiDelegateBinding<TValue>(FuncDelegateBindingGetter<TValue> _funcGetter, FuncDelegateBindingSetter<TValue> _funcSetter) : UiBinding<TValue>
{
	#region Fields

	private readonly FuncDelegateBindingGetter<TValue> funcGetter = _funcGetter;
	private readonly FuncDelegateBindingSetter<TValue> funcSetter = _funcSetter;

	#endregion
	#region Methods

	public override TValue GetValue() => funcGetter();

	public override bool SetValue(TValue _newValue, UiBindingValueSource _source)
	{
		if (!AllowedSourceMask.HasFlag(_source)) return false;

		funcSetter(_newValue, _source);
		SourceOfLastChange = _source;
		NotifyValueChanged(_newValue, _source);
		return true;
	}

	public override string ToString()
	{
		return $"DelegateBinding<{typeof(TValue).Name}> | get/set";
	}

	#endregion
}
