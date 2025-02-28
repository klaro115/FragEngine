using FragEngine3.Resources;

namespace FragEngine3.UI.Bindings;

/// <summary>
/// Base type for data bindings used by the UI system.
/// Bindings allow the synchronization of data between UI controls and an underlying model or data source.
/// </summary>
public abstract class UiBinding
{
	#region Properties
	
	/// <summary>
	/// Gets a bit mask of all value sources that are permitted by this binding.
	/// </summary>
	public UiBindingValueSource AllowedSourceMask { get; init; } = UiBindingValueSource.ALL;
	/// <summary>
	/// Gets the source of the most recent data that was set on the binding.
	/// </summary>
	public UiBindingValueSource SourceOfLastChange { get; protected set; } = UiBindingValueSource.Init;

	#endregion
	#region Methods

	/// <summary>
	/// Gets the binding's current value,
	/// </summary>
	/// <returns></returns>
	public abstract object? GetValueObject();

	/// <summary>
	/// Tries to set the binding's value.
	/// </summary>
	/// <param name="_newValue">A new value object.</param>
	/// <param name="_source">The type of the value's source, whether a UI control or the model.</param>
	/// <returns>True if the value could be set, false otherwise.</returns>
	public abstract bool SetValueObject(object _newValue, UiBindingValueSource _source);

	#endregion
	#region Methods Creators

	/// <summary>
	/// Creates a new binding around a <see cref="Resource"/>.<para/>
	/// This type of binding allows the attachement of graphics resources or other data using the engine's resource system.
	/// The resource is loaded on-demand either immediately or asynchronously, if it isn't loaded already by the time of
	/// first use.
	/// </summary>
	/// <typeparam name="TValue">The type of the bound variable, property, or data source. This must be a <see cref="Resource"/>.</typeparam>
	/// <param name="_initialValue">A resource handle from which the initial resource value may be loaded.</param>
	/// <param name="_loadImmediatelyIfNoReady">Whether to block and load the resource immediately, if it isn't loaded already by
	/// the time its value is first used. If false, the resource will be loaded asynchronously instead.</param>
	/// <param name="_loadResourceWhenSet">Whether to load the resource immediately upon setting it as the binding's value. If false,
	/// the resource will be loaded on-demand by the view instead.</param>
	/// <param name="_allowedSourceMask">A bit mask of permitted value source types.</param>
	/// <returns>A new resource binding.</returns>
	public static UiBinding<TValue>? Create<TValue>(
		ResourceHandle? _initialValue,
		bool _loadImmediatelyIfNoReady = true,
		bool _loadResourceWhenSet = true,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TValue : Resource
	{
		return new UiResourceBinding<TValue>(_initialValue, _loadImmediatelyIfNoReady, _loadResourceWhenSet) { AllowedSourceMask = _allowedSourceMask };
	}

	/// <summary>
	/// Creates a new binding around a <see cref="Resource"/>.<para/>
	/// This type of binding allows the attachement of graphics resources or other data using the engine's resource system.
	/// The resource is loaded on-demand either immediately or asynchronously, if it isn't loaded already by the time of
	/// first use.
	/// </summary>
	/// <typeparam name="TValue">The type of the bound variable, property, or data source. This must be a <see cref="Resource"/>.</typeparam>
	/// <param name="_initialResourceKey">A resource key through which the initial resource value may be identified and loaded.</param>
	/// <param name="_loadImmediatelyIfNoReady">Whether to block and load the resource immediately, if it isn't loaded already by
	/// the time its value is first used. If false, the resource will be loaded asynchronously instead.</param>
	/// <param name="_loadResourceWhenSet">Whether to load the resource immediately upon setting it as the binding's value. If false,
	/// the resource will be loaded on-demand by the view instead.</param>
	/// <param name="_allowedSourceMask">A bit mask of permitted value source types.</param>
	/// <returns>A new resource binding.</returns>
	public static UiBinding<TValue>? Create<TValue>(
		string _initialResourceKey,
		bool _loadImmediatelyIfNoReady = true,
		bool _loadResourceWhenSet = true,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TValue : Resource
	{
		return new UiResourceBinding<TValue>(_initialResourceKey, _loadImmediatelyIfNoReady, _loadResourceWhenSet) { AllowedSourceMask = _allowedSourceMask };
	}

	#endregion
}

/// <summary>
/// Generic base type for data bindings used by the UI system.
/// </summary>
/// <typeparam name="TValue">The type of the bound variable, property, or data source.</typeparam>
public abstract class UiBinding<TValue> : UiBinding
{
	#region Types

	/// <summary>
	/// Method delegate for listening to a binding's value change event.
	/// </summary>
	/// <param name="_newValue">The binding's new value.</param>
	/// <param name="_source">The source of the value.</param>
	public delegate void FuncOnValueChanged(TValue _newValue, UiBindingValueSource _source);

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the binding's value has changed.
	/// </summary>
	public event FuncOnValueChanged? OnValueChanged = null;

	#endregion
	#region Methods

	/// <summary>
	/// Gets the binding's current valze.
	/// </summary>
	/// <returns></returns>
	public abstract TValue GetValue();
	public override object? GetValueObject() => GetValue();

	/// <summary>
	/// Tries to set the binding's value.
	/// </summary>
	/// <param name="_newValue">A new value.</param>
	/// <param name="_source">The type of the value's source, whether a UI control or the model.</param>
	/// <returns>True if the value could be set, false otherwise.</returns>
	public abstract bool SetValue(TValue _newValue, UiBindingValueSource _source);
	public override bool SetValueObject(object _newValue, UiBindingValueSource _source)
	{
		return _newValue is TValue typedValue && SetValue(typedValue, _source);
	}

	protected void NotifyValueChanged(in TValue _newValue, UiBindingValueSource _valueSource) => OnValueChanged?.Invoke(_newValue, _valueSource);

	#endregion
	#region Methods Creators

	/// <summary>
	/// Creates a new binding around an arbitrary value object.
	/// </summary>
	/// <param name="_initialValue">The initial value to initialize the binding with.</param>
	/// <param name="_allowedSourceMask">A bit mask of permitted value source types.</param>
	/// <returns>A new value binding.</returns>
	public static UiBinding<TValue>? Create(
		TValue _initialValue,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
	{
		return new UiValueBinding<TValue>(_initialValue) { AllowedSourceMask = _allowedSourceMask };
	}

	/// <summary>
	/// Creates a new binding around a member path relative to a root object.<para/>
	/// The binding will resolve this path using reflection, which allows easy-to-setup string-based bindings with loose coupling.
	/// </summary>
	/// <typeparam name="TRoot">The type of the root object.</typeparam>
	/// <param name="_rootObject">A root object; the member path will be relative to this instance.</param>
	/// <param name="_memberPath">A string denoting a relative path from the root object, and leading down one or more member
	/// variables or properties. The names of all members are case-sensitive, and parts must be separated by period characters.</param>
	/// <param name="_allowPrivateMembers">Whether to allow private variables and properties along the member path.</param>
	/// <param name="_allowedSourceMask">A bit mask of permitted value source types.</param>
	/// <returns>A new path binding.</returns>
	public static UiBinding<TValue>? Create<TRoot>(
		TRoot _rootObject,
		string _memberPath,
		bool _allowPrivateMembers = false,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
		where TRoot : class
	{
		return new UiPathBinding<TRoot, TValue>(_rootObject, _memberPath, _allowPrivateMembers) { AllowedSourceMask = _allowedSourceMask };
	}

	/// <summary>
	/// Creates a new binding around a pair of getter-setter methods.<para/>
	/// This type of binding is very easy to set up programatically, and possibly best-performing and most type safe.
	/// </summary>
	/// <param name="_funcGetter">A method delegate for retrieving the value from the underlying model.</param>
	/// <param name="_funcSetter">A method delegate for setting the value on the underlying model.</param>
	/// <param name="_allowedSourceMask">A bit mask of permitted value source types.</param>
	/// <returns></returns>
	public static UiBinding<TValue>? Create(
		FuncDelegateBindingGetter<TValue> _funcGetter,
		FuncDelegateBindingSetter<TValue> _funcSetter,
		UiBindingValueSource _allowedSourceMask = UiBindingValueSource.ALL)
	{
		return new UiDelegateBinding<TValue>(_funcGetter, _funcSetter) { AllowedSourceMask = _allowedSourceMask };
	}

	#endregion
}
