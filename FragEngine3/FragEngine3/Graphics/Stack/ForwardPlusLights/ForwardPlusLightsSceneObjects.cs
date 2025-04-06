using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Lighting.Data;

namespace FragEngine3.Graphics.Stack.ForwardPlusLights;

internal sealed class ForwardPlusLightsSceneObjects
{
	#region Fields

	public readonly List<CameraComponent> activeCameras = new(2);
	public readonly List<ILightSource> activeLights = new(10);
	public readonly List<ILightSource> activeLightsShadowMapped = new(5);

	public readonly List<IRenderer> activeRenderersOpaque = new(64);
	public readonly List<IRenderer> activeRenderersTransparent = new(64);
	public readonly List<IRenderer> activeRenderersUI = new(64);
	public readonly List<IRenderer> activeShadowCasters = new(64);

	public LightSourceData[] activeLightData = [];

	#endregion
	#region Properties

	public uint ActiveCamerasCount => (uint)activeCameras.Count;
	public uint ActiveLightCount => (uint)activeLights.Count;
	public uint ActiveShadowMappedLightsCount => (uint)activeLightsShadowMapped.Count;
	public int ActiveShadowCasterCount => activeShadowCasters.Count;

	#endregion
	#region Methods

	public void Clear()
	{
		activeCameras.Clear();
		activeLights.Clear();
		activeLightsShadowMapped.Clear();
		activeRenderersOpaque.Clear();
		activeRenderersTransparent.Clear();
		activeRenderersUI.Clear();
		activeShadowCasters.Clear();
	}

	public void PrepareScene(
		in List<IRenderer> _renderers,
		in IList<CameraComponent> _cameras,
		in IList<ILightSource> _lights)
	{
		// Identify only active cameras and visible light sources:
		foreach (CameraComponent camera in _cameras)
		{
			if (!camera.IsDisposed && camera.layerMask != 0 && camera.node.IsEnabledInHierarchy())
			{
				activeCameras.Add(camera);
			}
		}

		foreach (ILightSource light in _lights)
		{
			// Skip disabled and overly dim light sources:
			if (light.IsDisposed || light.LayerMask == 0 || !light.IsVisible)
				continue;

			// Only retain sources whose light may be seen by an active camera:
			foreach (CameraComponent camera in activeCameras)
			{
				if (light.CheckVisibilityByCamera(in camera))
				{
					activeLights.Add(light);
					break;
				}
			}
		}
		// Sort lights, to prioritize shadow casters first, and higher priority lights second:
		activeLights.Sort(ILightSource.CompareLightsForSorting);
		foreach (ILightSource light in activeLights)
		{
			if (light.CastShadows)
			{
				activeLightsShadowMapped.Add(light);
			}
		}

		// Identify only visible renderers:
		foreach (IRenderer renderer in _renderers)
		{
			if (renderer.IsVisible)
			{
				//TODO [later]: We'll just take all renderers for now, but they need to be excluded/culled if not visible by any camera. => wait for spatial partitioning branch.

				List<IRenderer>? rendererList = renderer.RenderMode switch
				{
					RenderMode.Opaque => activeRenderersOpaque,
					RenderMode.Transparent => activeRenderersTransparent,
					RenderMode.UI => activeRenderersUI,
					_ => null,
				};
				rendererList?.Add(renderer);
			}
		}
		activeShadowCasters.AddRange(activeRenderersOpaque);
		activeShadowCasters.AddRange(activeRenderersTransparent);
	}

	public void PrepareLightSourceData()
	{
		if (activeLightData.Length < ActiveLightCount)
		{
			activeLightData = new LightSourceData[ActiveLightCount];
		}
		for (int i = 0; i < ActiveLightCount; ++i)
		{
			activeLightData[i] = activeLights[i].GetLightSourceData();
		}
	}

	#endregion
}
