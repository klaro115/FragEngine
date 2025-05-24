using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Lighting.Data;
using FragEngine3.Graphics.Lighting.Instances;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components;

/// <summary>
/// A component that represents a light source.<para/>
/// Note: This component is essentially a wrapper around a '<see cref="Light"/>', that is attached to a scene node
/// through component logic.
/// </summary>
[ComponentBackingType(typeof(Light))]
public sealed class LightComponent : Component, ILightSource, IOnNodeDestroyedListener, IOnComponentRemovedListener
{
	#region Constructors

	/// <summary>
	/// Creates a new light source component and registers it with its host scene.
	/// </summary>
	/// <param name="_node">The scene node which the component will be attached to.</param>
	public LightComponent(SceneNode _node) : base(_node)
	{
		GraphicsCore = node.scene.GraphicsStack?.Core ?? node.scene.engine.GraphicsSystem.graphicsCore;

		node.scene.drawManager.RegisterLight(this);

		lightInstance = new PointLight(GraphicsCore);
	}

	#endregion
	#region Fields

	private Light lightInstance;

	#endregion
	#region Properties

	public bool IsVisible => !IsDisposed && node.IsEnabledInHierarchy();

	public int LightPriority { get; set; } = 1;

	public uint LayerMask
	{
		get => lightInstance.LayerMask;
		set => lightInstance.LayerMask = value;
	}

	public LightType Type
	{
		get => lightInstance is not null ? lightInstance.Type : LightType.Point;
		set
		{
			if (lightInstance.Type != value)
			{
				LightData? data = null;
				lightInstance?.SaveToData(out data);
				lightInstance?.Dispose();

				lightInstance = value switch
				{
					LightType.Spot => new SpotLight(GraphicsCore),
					LightType.Directional => new DirectionalLight(GraphicsCore),
					_ => new PointLight(GraphicsCore),
				};
				if (data is not null)
				{
					lightInstance.LoadFromData(in data);
				}
			}
		}
	}

	/// <summary>
	/// Gets or sets the intensity of light emitted by this light source. TODO: Figure out which unit to use for this.
	/// </summary>
	public float LightIntensity
	{
		get => lightInstance.LightIntensity;
		set => lightInstance.LightIntensity = value;
	}
	public float MaxLightRange => lightInstance.MaxLightRange;

	/// <summary>
	/// Gets or sets the angle in which spot lights cast their light, in radians.
	/// </summary>
	public float SpotAngleRadians
	{
		get => lightInstance is SpotLight spotInstance ? spotInstance.SpotAngleRadians : MathF.PI;
		set { if (lightInstance is SpotLight spotInstance) spotInstance.SpotAngleRadians = value; }
	}
	/// <summary>
	/// Gets or sets the angle in which spot lights cast their light, in degrees.
	/// </summary>
	public float SpotAngleDegrees
	{
		get => lightInstance is SpotLight spotInstance ? spotInstance.SpotAngleDegrees : MathF.PI;
		set { if (lightInstance is SpotLight spotInstance) spotInstance.SpotAngleDegrees = value; }
	}

	public bool CastShadows
	{
		get => lightInstance.CastShadows;
		set => lightInstance.CastShadows = value;
	}

	public uint ShadowCascades
	{
		get => lightInstance.ShadowCascades;
		set => lightInstance.ShadowCascades = value;
	}

	/// <summary>
	/// A bias for shadow map evaluation in the shader, which is implemented as a distance offset away from a mesh's surface.
	/// Setting this value too low may cause stair-stepping artifacts in lighting calculations, commonly referred to as "shadow acne".
	/// </summary>
	public float ShadowNormalBias
	{
		get => lightInstance.ShadowNormalBias;
		set => lightInstance.ShadowNormalBias = value;
	}

	/// <summary>
	/// A bias for shadow map evaluation in shadow projection, which is implemented as a distance offset towards the light source.
	/// Setting this value too high may cause "Peter-Panning" artifacts in lighting calculations, where objects appear detached from their shadow.
	/// </summary>
	public float ShadowDepthBias
	{
		get => lightInstance.ShadowDepthBias;
		set => lightInstance.ShadowDepthBias = value;
	}

	public bool IsStaticLight
	{
		get => lightInstance.IsStaticLight;
		set => lightInstance.IsStaticLight = value;
	}

	public bool IsStaticLightDirty => lightInstance.IsStaticLightDirty;

	/// <summary>
	/// Gets the world space position of the light source. Only relevant for point and spot lights.
	/// </summary>
	public Vector3 WorldPosition => node.WorldPosition;
	/// <summary>
	/// Gets the world space diretion of light rays coming off this light source. Only relevant for spot and directional lights.
	/// </summary>
	public Vector3 Direction => Vector3.Transform(Vector3.UnitZ, node.WorldRotation);

	public GraphicsCore GraphicsCore { get; private init; }

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		lightInstance.Dispose();
	}

	public void OnNodeDestroyed()
	{
		node.scene.drawManager.UnregisterLight(this);
	}
	public void OnComponentRemoved(Component _removedComponent)
	{
		if (_removedComponent == this)
		{
			node.scene.drawManager.UnregisterLight(this);
		}
	}

	public static int CompareLightsForSorting(LightComponent _a, LightComponent _b)
	{
		int weightA = _a.LightPriority + (_a.CastShadows ? 1000 : 0);
		int weightB = _b.LightPriority + (_b.CastShadows ? 1000 : 0);
		return weightB.CompareTo(weightA);
	}

	/// <summary>
	/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
	/// </summary>
	public LightSourceData GetLightSourceData()
	{
		lightInstance.WorldPose = node.WorldTransformation;
		return lightInstance.GetLightSourceData();
	}

	public bool BeginDrawShadowMap(
		in SceneContext _sceneCtx,
		float _shadingFocalPointRadius,
		uint _newShadowMapIdx)
	{
		lightInstance.WorldPose = node.WorldTransformation;

		return lightInstance.BeginDrawShadowMap(
			in _sceneCtx,
			_shadingFocalPointRadius,
			_newShadowMapIdx);
	}

	public bool BeginDrawShadowCascade(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		Vector3 _shadingFocalPoint,
		uint _cascadeIdx,
		out CameraPassContext _outCameraPassCtx,
		bool _rebuildResSetCamera = false,
		bool _texShadowMapsHasChanged = false)
	{
		return lightInstance.BeginDrawShadowCascade(
			in _sceneCtx,
			in _cmdList,
			_shadingFocalPoint,
			_cascadeIdx,
			out _outCameraPassCtx,
			_rebuildResSetCamera,
			_texShadowMapsHasChanged);
	}

	public bool EndDrawShadowCascade()
	{
		return lightInstance.EndDrawShadowCascade();
	}

	public bool EndDrawShadowMap()
	{
		return !IsDisposed && lightInstance.EndDrawShadowMap();
	}

	/// <summary>
	/// Check whether light emitted by this light source has any chance of being seen by a given camera.
	/// </summary>
	/// <param name="_camera">The camera whose pixels may or may not be illuminated by this light source.</param>
	/// <returns>True if this instance's light could possible be seen by the camera, false otherwise.</returns>
	public bool CheckVisibilityByCamera(in CameraComponent _camera) => lightInstance.CheckVisibilityByCamera(_camera);

	public bool CheckIsRendererInRange(in IPhysicalRenderer _renderer) => lightInstance.CheckIsRendererInRange(_renderer);

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		if (string.IsNullOrEmpty(_componentData.SerializedData))
		{
			Logger.LogError("Cannot load light from null or blank serialized data!");
			return false;
		}

		// Deserialize camera data from component data's serialized data string:
		if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out LightData? data) || data is null)
		{
			Logger.LogError("Failed to deserialize light component data from JSON!");
			return false;
		}
		if (!data.IsValid())
		{
			Logger.LogError("Deserialized light component data is invalid!");
			return false;
		}

		// Set values:
		LayerMask = data.LayerMask;

		if (!lightInstance.LoadFromData(in data))
		{
			return false;
		}

		lightInstance.WorldPose = node.WorldTransformation;

		// Re-register camera with the scene:
		node.scene.drawManager.UnregisterLight(this);
		return node.scene.drawManager.RegisterLight(this);
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		// Get values:
		if (!lightInstance.SaveToData(out LightData data))
		{
			_componentData = ComponentData.Empty;
			return false;
		}

		data.LayerMask = LayerMask;

		if (!Serializer.SerializeToJson(data, out string dataJson))
		{
			Logger.LogError("Failed to serialize light component data to JSON!");
			_componentData = ComponentData.Empty;
			return false;
		}

		_componentData = new ComponentData()
		{
			SerializedData = dataJson,
		};
		return true;
	}
	
	#endregion
}
