using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Materials;

public sealed class DefaultSurfaceMaterial : MaterialNew
{
	#region Types

	[Flags]
	private enum DirtyFlags
	{
		CBDefaultSurface	= 1,
		BoundResources		= 2,
		//...

		ALL					= CBDefaultSurface | BoundResources
	}

	#endregion
	#region Constructors

	public DefaultSurfaceMaterial(GraphicsCore _graphicsCore, ResourceHandle _resourceHandle, MaterialDataNew _data) : base(_graphicsCore, _resourceHandle, _data)
	{
		BufferDescription constantBufferDesc = new(CBDefaultSurface.packedByteSize, BufferUsage.UniformBuffer);
		cbDefaultSurface = graphicsCore.MainFactory.CreateBuffer(ref constantBufferDesc);
		cbDefaultSurface.Name = $"{CBDefaultSurface.NAME_IN_SHADER}_{resourceKey}";
	}

	#endregion
	#region Fields

	private DirtyFlags dirtyFlags = DirtyFlags.ALL;

	private CBDefaultSurface cbDefaultSurfaceData = default;
	private DeviceBuffer? cbDefaultSurface = null;

	private ResourceSet[] resourceSets = [];

	#endregion
	#region Properties

	//...

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		IsDisposed = true;

		cbDefaultSurface?.Dispose();
		
		//...
	}

	public void MarkDirty() => dirtyFlags = DirtyFlags.ALL;

	public override bool CreatePipeline(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, MeshVertexDataFlags _vertexDataFlags, out PipelineState? _outPipelineState)
	{
		//TODO
		throw new NotImplementedException();
	}

	public override bool Prepare(in SceneContext _sceneCtx, in CameraPassContext _cameraCtx, out ResourceSet[]? _outResourceSets)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot prepare default surface material that has already been disposed!");
			_outResourceSets = null;
			return false;
		}

		// Update constant buffers:
		if (dirtyFlags.HasFlag(DirtyFlags.CBDefaultSurface))
		{
			try
			{
				graphicsCore.Device.UpdateBuffer(cbDefaultSurface, 0, ref cbDefaultSurfaceData);
			}
			catch (Exception ex)
			{
				logger.LogException("Failed to update contents of default surface constant buffer!", ex);
				_outResourceSets = null;
				return false;
			}
			dirtyFlags &= ~DirtyFlags.CBDefaultSurface;
		}

		// Update bound resource sets:
		if (dirtyFlags.HasFlag(DirtyFlags.BoundResources))
		{
			//TODO

			dirtyFlags &= ~DirtyFlags.BoundResources;
		}

		_outResourceSets = resourceSets;
		return true;
	}

	public override IEnumerator<ResourceHandle> GetResourceDependencies()
	{
		//TODO: Yield all referenced textures, buffers, samplers, and other resources.

		if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
		{
			yield return handle;
		}
	}

	#endregion
}
