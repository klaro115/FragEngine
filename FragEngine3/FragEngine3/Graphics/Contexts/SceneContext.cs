using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Contexts;

/// <summary>
/// Context type containing resources and parameters that are shared by all renderers across an entire scene.
/// </summary>
/// <param name="_lightCount">Total number of active lights in the scene.</param>
/// <param name="_lightCountShadowMapped">Total number of shadow-casting lights in the scene.</param>
public sealed class SceneContext(uint _lightCount, uint _lightCountShadowMapped)
{
	#region Fields

	// References:
	public Scene Scene { get; init; } = null!;

	// Scene resources:
	public ResourceLayout ResLayoutCamera { get; init; } = null!;
	public ResourceLayout ResLayoutObject { get; init; } = null!;
	public DeviceBuffer CbScene { get; init; } = null!;
	public LightDataBuffer DummyLightDataBuffer { get; init; } = null!;
	public ShadowMapArray ShadowMapArray { get; init; } = null!;
	public ushort SceneResourceVersion { get; init; }

	// Parameters:
	public readonly uint lightCount = _lightCount;
	public readonly uint lightCountShadowMapped = Math.Min(_lightCountShadowMapped, _lightCount);

	#endregion
}
