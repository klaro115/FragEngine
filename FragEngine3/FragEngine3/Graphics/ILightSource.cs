﻿using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Lighting.Data;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics;

public interface ILightSource : IDisposable
{
	#region Properties

	/// <summary>
	/// Gets whether this light source has been disposed and should no longer be used or referenced.
	/// </summary>
	bool IsDisposed { get; }
	/// <summary>
	/// Gets whether this light source is currently visible and valid. Only lights that return true
	/// will be asked to draw shadow maps and provide lighting data.
	/// </summary>
	bool IsVisible { get; }

	/// <summary>
	/// Priority rating to indicate which light sources are more important. Higher priority lights will
	/// be drawn first, lower priority light may be ignored as their impact on a mesh may be negligable.
	/// </summary>
	int LightPriority { get; }

	/// <summary>
	/// Bit mask for all layers that can be affected by this light source.
	/// </summary>
	uint LayerMask { get; }

	/// <summary>
	/// Gets or sets the emission shape of this light source.
	/// </summary>
	LightType Type { get; }

	/// <summary>
	/// Gets the maximum range out to which this light source produces any noticeable brightness.
	/// </summary>
	float MaxLightRange { get; }

	/// <summary>
	/// Gets or sets whether this light source should cast shadows.<para/>
	/// NOTE: If true, before scene cameras are drawn, a shadow map will be rendered for this light source.
	/// When changing this value to false, the shadow map and its framebuffer will be disposed. This flag
	/// may not be changed during the engine's drawing stage.<para/>
	/// LIMITATION: Point lights cannot casts shadows at this stage. Use spot or directional lights if shadows
	/// are required.
	/// </summary>
	public bool CastShadows { get; }

	/// <summary>
	/// Gets the number of shadow cascades to create and render for this light source.
	/// Directional and spot lights only. Must be a value between 0 and 4, where 0 disables cascades for this light.
	/// The number of shadow maps rendered for this light will be this value plus one, starting with the full-resolution map (0),
	/// followed by increasing cascades (1..4).
	/// </summary>
	uint ShadowCascades { get; }

	/// <summary>
	/// Gets whether this is a static light source. Static lights render shadow maps only once (or on demand),
	/// and then re-use that shadow map for all subsequent frames, drastically reducing shadow rendering overhead.<para/>
	/// LIMITATION: Static lights do not support having multiple cascades at this stage. <see cref="ShadowCascades"/> should
	/// be set to 0 to avoid projection artefacts.
	/// </summary>
	bool IsStaticLight { get; }

	/// <summary>
	/// For static lights, gets whether this light has a redraw scheduled or in progress. If false, the graphics stack
	/// may skip issuing any draw calls for shadow maps pertaining to this static light source.
	/// </summary>
	bool IsStaticLightDirty { get; }

	/// <summary>
	/// Gets the graphics core this light source was created with.
	/// </summary>
	GraphicsCore GraphicsCore { get; }

	#endregion
	#region Methods

	/// <summary>
	/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
	/// </summary>
	public LightSourceData GetLightSourceData();

	bool BeginDrawShadowMap(in SceneContext _sceneCtx, float _shadingFocalPointRadius, uint _newShadowMapIdx);
	bool EndDrawShadowMap();

	bool BeginDrawShadowCascade(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		Vector3 _shadingFocalPoint,
		uint _cascadeIdx,
		out CameraPassContext _outCameraPassCtx,
		bool _rebuildResSetCamera = false,
		bool _texShadowMapsHasChanged = false);

	bool EndDrawShadowCascade();

	/// <summary>
	/// Check whether light emitted by this light source has any chance of being seen by a given camera.
	/// </summary>
	/// <param name="_camera">The camera whose pixels may or may not be illuminated by this light source.</param>
	/// <returns>True if this instance's light could possibly be seen by the camera, false otherwise.</returns>
	bool CheckVisibilityByCamera(in CameraComponent _camera);

	/// <summary>
	/// Check whether light emitted by this light source has any chance of reaching a given renderer.
	/// </summary>
	/// <param name="_renderer">The renderer that may or may not be in range of the light source.</param>
	/// <returns>True if this instance's light could possibly illuminate the renderer, false otherwise.</returns>
	bool CheckIsRendererInRange(in IPhysicalRenderer _renderer);

	public static int CompareLightsForSorting(ILightSource _a, ILightSource _b)
	{
		int weightA = _a.LightPriority + (_a.CastShadows ? 1000 : 0);
		int weightB = _b.LightPriority + (_b.CastShadows ? 1000 : 0);
		return weightB.CompareTo(weightA);
	}

	#endregion
}
