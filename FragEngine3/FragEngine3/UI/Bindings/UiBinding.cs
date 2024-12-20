using FragEngine3.Resources;

namespace FragEngine3.UI.Bindings;

public abstract class UiBinding
{
	#region Properties

	public UiBindingValueSource AllowedSourceMask { get; init; } = UiBindingValueSource.ALL;
	public UiBindingValueSource SourceOfLastChange { get; protected set; } = UiBindingValueSource.Init;

	#endregion
	#region Methods

	public abstract object? GetValueObject();
	public abstract bool SetValueObject(object _newValue, UiBindingValueSource _source);
	
	#endregion
}

public abstract class UiBinding<TValue> : UiBinding
{
	#region Events

	public event Action<TValue, UiBindingValueSource>? OnValueChanged = null;

	#endregion
	#region Methods

	public abstract TValue GetValue();
	public override object? GetValueObject() => GetValue();
	
	public abstract bool SetValue(TValue _newValue, UiBindingValueSource _source);
	public override bool SetValueObject(object _newValue, UiBindingValueSource _source)
	{
		return _newValue is TValue typedValue && SetValue(typedValue, _source);
	}

	protected void NotifyValueChanged(in TValue _newValue, UiBindingValueSource _valueSource) => OnValueChanged?.Invoke(_newValue, _valueSource);

	#endregion
	#region Methods Creators

	public static UiBinding<TValue>? Create(
		TValue _initialValue,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
	{
		return new UiValueBinding<TValue>(_initialValue) { AllowedSourceMask = _allowedSourceMask };
	}

	public static UiBinding<TValue>? Create<TRoot>(
		TRoot _rootObject,
		string _memberPath,
		bool _allowPrivateMembers = false,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TRoot : class
	{
		return new UiPathBinding<TRoot, TValue>(_rootObject, _memberPath, _allowPrivateMembers) { AllowedSourceMask = _allowedSourceMask };
	}

	public static UiBinding<TValue>? Create(
		FuncDelegateBindingGetter<TValue> _funcGetter,
		FuncDelegateBindingSetter<TValue> _funcSetter,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
	{
		return new UiDelegateBinding<TValue>(_funcGetter, _funcSetter) { AllowedSourceMask = _allowedSourceMask };
	}

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
	public static UiBinding<TValue>? Create<TValue>(
		ResourceHandle? _initialValue,
		bool _loadImmediatelyIfNoReady = true,
		bool _loadResourceWhenSet = true,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TValue : Resource
	{
		return new UiResourceBinding<TValue>(_initialValue, _loadImmediatelyIfNoReady, _loadResourceWhenSet) { AllowedSourceMask = _allowedSourceMask };
	}

	public static UiBinding<TValue>? Create<TValue>(
		string _initialResourceKey,
		bool _loadImmediatelyIfNoReady = true,
		bool _loadResourceWhenSet = true,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TValue : Resource
	{
		return new UiResourceBinding<TValue>(_initialResourceKey, _loadImmediatelyIfNoReady, _loadResourceWhenSet) { AllowedSourceMask = _allowedSourceMask };
	}
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type

	#endregion
}
