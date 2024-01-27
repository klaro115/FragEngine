using FragEngine3.Graphics.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Internal;

public sealed class PipelineState(Pipeline _pipeline, uint _version, MeshVertexDataFlags _vertexDataFlags, uint _vertexBufferCount) : IDisposable
{
	#region Constructors

	~PipelineState()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Pipeline pipeline = _pipeline ?? throw new ArgumentNullException(nameof(_pipeline), "Graphics pipeline may not be null!");

	public readonly uint version = _version;
	public readonly MeshVertexDataFlags vertexDataFlags = _vertexDataFlags | MeshVertexDataFlags.BasicSurfaceData;
	public readonly uint vertexBufferCount = Math.Max(_vertexBufferCount, 1);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		IsDisposed = true;

		pipeline.Dispose();
	}

	public override string ToString()
	{
		return $"Pipeline: '{pipeline}' (v{version}), VertexFlags: '{vertexDataFlags}', VertBufCount: {vertexBufferCount}";
	}

	#endregion
}
