using FragEngine3.Graphics.Lighting.Internal;
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
	public required Scene Scene { get; init; } = null!;

	// Scene resources:
	public required ResourceLayout ResLayoutCamera { get; init; } = null!;
	public required ResourceLayout ResLayoutObject { get; init; } = null!;
	public required DeviceBuffer CbScene { get; init; } = null!;
	public required LightDataBuffer DummyLightDataBuffer { get; init; } = null!;
	public required ShadowMapArray ShadowMapArray { get; init; } = null!;
	public required ushort SceneResourceVersion { get; init; }

	// Parameters:
	public readonly uint lightCount = _lightCount;
	public readonly uint lightCountShadowMapped = Math.Min(_lightCountShadowMapped, _lightCount);

	#endregion
}
