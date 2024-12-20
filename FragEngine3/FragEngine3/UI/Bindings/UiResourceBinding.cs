using FragEngine3.EngineCore;
using FragEngine3.Resources;

namespace FragEngine3.UI.Bindings;

public sealed class UiResourceBinding<TValue> : UiBinding<TValue> where TValue : Resource
{
	#region Constructors

	public UiResourceBinding(ResourceHandle? _initialValue, bool _loadImmediatelyIfNoReady = true, bool _loadResourceWhenSet = true)
	{
		loadImmediatelyIfNoReady = _loadImmediatelyIfNoReady;
		loadResourceWhenSet = _loadResourceWhenSet;
		Handle = _initialValue ?? ResourceHandle.None;
	}

	public UiResourceBinding(string _resourceKey, bool _loadImmediatelyIfNoReady = true, bool _loadResourceWhenSet = true)
	{
		loadImmediatelyIfNoReady = _loadImmediatelyIfNoReady;
		loadResourceWhenSet = _loadResourceWhenSet;
		Handle = ResourceHandle.None;

		SetValueFromResourceKey(_resourceKey, UiBindingValueSource.Init);
	}

	#endregion
	#region Fields


	public readonly bool loadImmediatelyIfNoReady;
	public readonly bool loadResourceWhenSet;

	#endregion
	#region Properties

	public ResourceHandle Handle { get; private set; } = ResourceHandle.None;
	public bool IsLoaded => Handle is not null && Handle.IsLoaded;

	#endregion
	#region Methods

	public override TValue GetValue()
	{
		Resource? resource = Handle.GetResource(loadImmediatelyIfNoReady, true);
		
		if (resource is not null && resource is not TValue)
		{
			Logger.Instance?.LogError($"Error! Resource handle '{Handle}' does not match binding type '{typeof(TValue).Name}'!");
			return default!;
		}
		return (resource as TValue)!;
	}

	public override bool SetValue(TValue _newValue, UiBindingValueSource _source)
	{
		if (!AllowedSourceMask.HasFlag(_source)) return false;

		return !string.IsNullOrEmpty(_newValue?.resourceKey)
			? SetValueFromResourceKey(_newValue.resourceKey, _source)
			: SetValueFromHandle(ResourceHandle.None, _source);
	}

	public override bool SetValueObject(object _newValue, UiBindingValueSource _source)
	{
		if (!AllowedSourceMask.HasFlag(_source)) return false;

		if (_newValue is TValue resource)
		{
			return SetValue(resource, _source);
		}
		if (_newValue is ResourceHandle newHandle)
		{
			return SetValueFromHandle(newHandle, _source);
		}
		if (_newValue is string resourceKey)
		{
			return SetValueFromResourceKey(resourceKey, _source);
		}
		return false;
	}

	private bool SetValueFromResourceKey(string _resourceKey, UiBindingValueSource _source)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			return SetValueFromHandle(ResourceHandle.None, _source);
		}

		ResourceManager? resourceManager = Engine.Instance?.ResourceManager;    //TODO: Try fetching resource manager via handle first.
		if (resourceManager is null)
		{
			Logger.Instance?.LogError($"Error! No resource manager could be found for loading binding of type '{typeof(TValue).Name}'!");
			return false;
		}

		if (!resourceManager.GetResource(_resourceKey, out ResourceHandle newHandle) || !newHandle.IsValid)
		{
			Logger.Instance?.LogError($"Error! No resource handle was found for key '{_resourceKey}' and binding of type '{typeof(TValue).Name}'!");
			return false;
		}

		return SetValueFromHandle(newHandle, _source);
	}

	private bool SetValueFromHandle(ResourceHandle _newHandle, UiBindingValueSource _source)
	{
		if (Handle is null || !Handle.IsValid)
		{
			Handle = ResourceHandle.None;
		}

		Handle = _newHandle;
		SourceOfLastChange = _source;

		if (loadResourceWhenSet && !IsLoaded)
		{
			Handle.Load(false);
		}
		NotifyValueChanged((TValue)Handle.GetResource(false, false)!, _source);
		return true;
	}

	public override string ToString()
	{
		return $"ResourceBinding<{typeof(TValue).Name}> | Resource='{Handle ?? ResourceHandle.None}'";
	}

	#endregion
}
