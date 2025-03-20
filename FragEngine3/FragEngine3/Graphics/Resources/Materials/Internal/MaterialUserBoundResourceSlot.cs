using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials.Internal;

/// <summary>
/// Base class for a slot for a user-bound resource.
/// </summary>
public abstract class MaterialUserBoundResourceSlot
{
	#region Constructors

	internal MaterialUserBoundResourceSlot(BindableResource?[] _boundResources, int _boundResourceIndex, ResourceKind _resourceKind, Action _funcMarkDirty)
	{
		boundResources = _boundResources ?? throw new ArgumentNullException(nameof(_boundResources), "Array of bound resources may not be null!");
		funcMarkDirty = _funcMarkDirty ?? throw new ArgumentNullException(nameof(_funcMarkDirty), "Function delegate to mark resource set as dirty may not be null!");

		if (_boundResourceIndex < 0 || _boundResourceIndex >= boundResources.Length)
		{
			throw new IndexOutOfRangeException("Position index in array of bound resources is out of range!");
		}

		boundResourceIndex = _boundResourceIndex;
		resourceKind = _resourceKind;
	}

	#endregion
	#region Fields

	protected readonly BindableResource?[] boundResources;
	private readonly Action funcMarkDirty;

	/// <summary>
	/// An index in the material's user-bound resource set that this slot maps to.
	/// </summary>
	public readonly int boundResourceIndex;
	/// <summary>
	/// The kind of graphics resource that can be bound through this slot.
	/// </summary>
	public readonly ResourceKind resourceKind;

	protected ResourceHandle resourceHandle = ResourceHandle.None;

	/// <summary>
	/// Gets the resource key of the slot's bound resource. Null if the slot's value is unassigned, or if the value was not set using a resource handle.
	/// </summary>
	public string? ResourceKey => resourceHandle.IsValid ? resourceHandle.resourceKey : null;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the currently bound resource.
	/// </summary>
	public BindableResource? Resource
	{
		get => boundResources[boundResourceIndex];
		protected set
		{
			BindableResource? prevValue = boundResources[boundResourceIndex];
			if (prevValue != value)
			{
				boundResources[boundResourceIndex] = value;
				funcMarkDirty();
			}
		}
	}

	#endregion
	#region Methods

	/// <summary>
	/// Creates a new resource slot.
	/// </summary>
	/// <param name="_boundResources">An array of bound resources. This slot will map to an index position within this array.</param>
	/// <param name="_boundResourceIndex">The index within the resources array that this slot maps to.</param>
	/// <param name="_funcMarkDirty">Delegate for a method that marks the resource array as dirty. Use this to notify materials about the changed resource set.</param>
	/// <param name="_resourceKind">The type of graphics resource that this slot can contain.</param>
	/// <param name="_outSlot">Outputs a new resource slot, or null, on failure.</param>
	/// <returns>True if a new resource slot could be created, false otherwise.</returns>
	/// <exception cref="ArgumentNullException">'<see cref="_boundResources"/>' or '<see cref="_funcMarkDirty"/>' may not be null.</exception>
	/// <exception cref="IndexOutOfRangeException">'<see cref="_boundResourceIndex"/>' array index may not be negative or out of bounds.</exception>
	internal static bool CreateSlot(BindableResource?[] _boundResources, int _boundResourceIndex, Action _funcMarkDirty, ResourceKind _resourceKind, out MaterialUserBoundResourceSlot? _outSlot)
	{
		_outSlot = _resourceKind switch
		{
			ResourceKind.UniformBuffer				=> new MaterialUserBoundResourceSlot<DeviceBuffer>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			ResourceKind.StructuredBufferReadWrite	=> new MaterialUserBoundResourceSlot<DeviceBuffer>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			ResourceKind.StructuredBufferReadOnly	=> new MaterialUserBoundResourceSlot<DeviceBuffer>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			ResourceKind.TextureReadOnly			=> new MaterialUserBoundResourceSlot<Texture>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			ResourceKind.TextureReadWrite			=> new MaterialUserBoundResourceSlot<Texture>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			ResourceKind.Sampler					=> new MaterialUserBoundResourceSlot<Sampler>(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty),
			_										=> null,
		};
		return _outSlot is not null;
	}

	public bool SetValue(BindableResource _newValue)
	{
		resourceHandle = ResourceHandle.None;
		Resource = _newValue;
		return Resource == _newValue;
	}

	public abstract bool SetValue(ResourceHandle _handle);

	#endregion
}

/// <summary>
/// A generic typed slot for a user-bound resource.
/// </summary>
/// <typeparam name="T">The resource type, must implement <see cref="BindableResource"/>.</typeparam>
/// <param name="_boundResources">The material's array of user-bound resources; this slot will map to one of this array's elements.</param>
/// <param name="_boundResourceIndex">The index of the element that this resource slot maps to.</param>
/// <param name="_funcMarkDirty">A callback method delegate that is called internally whenever the resource's value has changed.
/// This will let the material know that the resource set is dirty and needs to be rebuilt.</param>
public sealed class MaterialUserBoundResourceSlot<T>(BindableResource?[] _boundResources, int _boundResourceIndex, ResourceKind _resourceKind, Action _funcMarkDirty) :
	MaterialUserBoundResourceSlot(_boundResources, _boundResourceIndex, _resourceKind, _funcMarkDirty)
	where T : class, BindableResource
{
	#region Fields

	private T? value = null;

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets the value of the currently bound resource.
	/// </summary>
	public T? Value
	{
		get => value;
		set
		{
			this.value = value;
			Resource = value;
			resourceHandle = ResourceHandle.None;
		}
	}

	public ResourceHandle ResourceHandle
	{
		get => resourceHandle;
		set => SetValue(value);
	}

	#endregion
	#region Methods

	public override bool SetValue(ResourceHandle _handle)
	{
		if (_handle is null || !_handle.IsValid)
		{
			Value = null;
			return true;
		}

		// Try to load the resource immediately:
		Resource? resource = _handle.GetResource(true);
		if (resource is null)
		{
			_handle.resourceManager.engine.Logger.LogError($"Failed to load resource '{_handle.resourceKey}'! (User-bound slot '{this}')");
			return false;
		}

		// Assign resource value, ensuring compatibility with resource kind:
		switch (resourceKind)
		{
			case ResourceKind.UniformBuffer:
			case ResourceKind.StructuredBufferReadOnly:
			case ResourceKind.StructuredBufferReadWrite:
				{
					//TODO [later]: Buffer-type engine resources are not implemented yet.
					throw new NotImplementedException("Buffer-type engine resources are not implemented yet");
				}
			case ResourceKind.TextureReadOnly:
			case ResourceKind.TextureReadWrite:
				if (resource is TextureResource texResource)
				{
					Resource = texResource.Texture;
					value = texResource.Texture as T;
					return true;
				}
				break;
			case ResourceKind.Sampler:
				{
					_handle.resourceManager.engine.Logger.LogError($"Sampler-type resources cannot be assigned from resource handle! (User-bound slot: '{this}')");
					return false;
				}
			default:
				break;
		}
		return false;
	}

	public override string ToString()
	{
		string valueTxt = value is not null ? value.ToString()! : "NULL";
		return $"Resource index: {boundResourceIndex}, Type: '{typeof(T).Name}', Value: '{valueTxt}'";
	}

	#endregion
}
