using FragEngine3.EngineCore;
using FragEngine3.Graphics.Contexts;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Internal;

public abstract class MaterialResources(GraphicsCore _graphicsCore, ResourceLayout _resourceLayout) : IDisposable
{
	#region Constructors

	~MaterialResources()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever the resource set has been (re)created or updated.
	/// Subscribers to this event should consider any previous resource sets as invalid and disposed whenever this event fires.
	/// </summary>
	public event Action<ResourceSet>? OnResourceSetChanged = null;

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore = _graphicsCore;
	public readonly ResourceLayout resourceLayout = _resourceLayout;

	protected ResourceSetDescription resourceSetDesc = default;
	protected ResourceSet? resourceSet = null;
	protected DeviceBuffer? constantBuffer = null;

	private readonly Dictionary<string, MaterialResourceMapping> valueNameMapping = [];	//TODO: Move this to default/generic child class implementation?

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	private Logger? Logger => graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	protected virtual void Dispose(bool _disposing)
	{
		IsDisposed = true;

		constantBuffer?.Dispose();
		resourceSet?.Dispose();
	}

	public bool PrepareResources(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, bool _forceRebuildResources, out ResourceSet? _outResourceSet, out bool _outResourceSetChanged)
	{
		_outResourceSetChanged = false;
		if (IsDisposed)
		{
			Logger?.LogError("Cannot prepare resources for disposed material resources instance!");
			_outResourceSet = null;
			return false;
		}

		// Update or create the constant buffer:
		if (!UpdateConstantBuffer(
			in _sceneCtx,
			in _cameraCtx,
			_forceRebuildResources,
			out bool cbChanged))
		{
			Logger?.LogError("Failed to update constant buffer for material resources instance!");
			_outResourceSet = null;
			return false;
		}
		_forceRebuildResources |= cbChanged;

		// Update or recreate the resource set description:
		if (!UpdateBoundResources(
			in _sceneCtx,
			in _cameraCtx,
			_forceRebuildResources,
			out _outResourceSetChanged))
		{
			Logger?.LogError("Failed to update bound resources for material resources instance!");
			_outResourceSet = null;
			return false;
		}
		_outResourceSetChanged |= resourceSet is null;

		// Recreate resource set if needed:
		if (_outResourceSetChanged)
		{
			resourceSet?.Dispose();

			try
			{
				resourceSet = graphicsCore.MainFactory.CreateResourceSet(ref resourceSetDesc);
				resourceSet.Name = $"ResSetObject_{resourceLayout.Name ?? string.Empty}";
			}
			catch (Exception ex)
			{
				Logger?.LogException("Failed to create resource set for material resources instance!", ex);
				_outResourceSet = null;
				return false;
			}

			// Notify any dependent systems about the new resource set:
			OnResourceSetChanged?.Invoke(resourceSet);
		}

		_outResourceSet = resourceSet!;
		return true;
	}

	protected abstract bool UpdateConstantBuffer(
		in SceneContext _sceneCtx,
		in CameraPassContext _cameraCtx,
		bool _forceRebuildConstantBuffer,
		out bool _outConstantBufferRecreated);

	protected abstract bool UpdateBoundResources(
		in SceneContext _sceneCtx,
		in CameraPassContext _cameraCtx,
		bool _forceRebuildResourceSet,
		out bool _outResourceSetChanged);

	#endregion
}
