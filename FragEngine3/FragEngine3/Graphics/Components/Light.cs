using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
    public sealed class Light : Component			//TODO: Consider splitting this into different components based on type.
	{
		#region Constructors

		/// <summary>
		/// Creates a new light source component and registers it with its host scene.
		/// </summary>
		/// <param name="_node">The scene node which the component will be attached to.</param>
		public Light(SceneNode _node) : base(_node)
		{
			core = node.scene.GraphicsStack?.Core ?? node.scene.engine.GraphicsSystem.graphicsCore;

			node.scene.drawManager.RegisterLight(this);

			lightInstance = new PointLightInstance(core);
		}

		#endregion
		#region Fields

		internal readonly GraphicsCore core;

		private LightInstance lightInstance;

		/// <summary>
		/// Priority rating to indicate which light sources are more important. Higher priority lights will
		/// be drawn first, lower priority light may be ignored as their impact on a mesh may be negligable.
		/// </summary>
		public int lightPriority = 1;
		/// <summary>
		/// Bit mask for all layers that can be affected by this light source.
		/// </summary>
		public uint layerMask = 0xFFu;

		#endregion
		#region Constants

		private static readonly SceneEventType[] sceneEventTypes =
		[
			SceneEventType.OnNodeDestroyed,
			SceneEventType.OnDestroyComponent,
		];

		#endregion
		#region Properties

		public override SceneEventType[] GetSceneEventList() => sceneEventTypes;

		/// <summary>
		/// Gets or sets the emission shape of this light source.
		/// </summary>
		public LightType Type
		{
			get => lightInstance != null ? lightInstance.Type : LightType.Point;
			set
			{
				if (lightInstance.Type != value)
				{
					LightData? data = null;
					lightInstance?.SaveToData(out data);
					lightInstance?.Dispose();

					lightInstance = value switch
					{
						LightType.Spot => new SpotLightInstance(core),
						LightType.Directional => new DirectionalLightInstance(core),
						_ => new PointLightInstance(core),
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
		/// <summary>
		/// Gets the maximum range out to which this light source produces any noticeable brightness. 
		/// </summary>
		public float MaxLightRange => lightInstance.MaxLightRange;

		/// <summary>
		/// Gets or sets the angle in which spot lights cast their light, in radians.
		/// </summary>
		public float SpotAngleRadians
		{
			get => lightInstance is SpotLightInstance spotInstance ? spotInstance.SpotAngleRadians : MathF.PI;
			set { if (lightInstance is SpotLightInstance spotInstance) spotInstance.SpotAngleRadians = value; }
		}
		/// <summary>
		/// Gets or sets the angle in which spot lights cast their light, in degrees.
		/// </summary>
		public float SpotAngleDegrees
		{
			get => lightInstance is SpotLightInstance spotInstance ? spotInstance.SpotAngleDegrees : MathF.PI;
			set { if (lightInstance is SpotLightInstance spotInstance) spotInstance.SpotAngleDegrees = value; }
		}

		/// <summary>
		/// Gets or sets whether this light source should cast shadows.<para/>
		/// NOTE: If true, before scene cameras are drawn, a shadow map will be rendered for this light source.
		/// When changing this value to false, the shadow map and its framebuffer will be disposed. This flag
		/// may not be changed during the engine's drawing stage.<para/>
		/// LIMITATION: Point lights cannot casts shadows at this stage. Use spot or directional lights if shadows
		/// are required.
		/// </summary>
		public bool CastShadows
		{
			get => lightInstance.CastShadows;
			set => lightInstance.CastShadows = value;
		}

		/// <summary>
		/// Gets or sets the number of shadow cascades to create and render for this light source.
		/// Directional and spot lights only. Must be a value between 0 and 4, where 0 disables cascades for this light.
		/// </summary>
		public uint ShadowCascades
		{
			get => lightInstance.ShadowCascades;
			set => lightInstance.ShadowCascades = value;
		}

		/// <summary>
		/// A bias for shadow map evaluation in the shader, which is implemented as a distance offset away from a mesh's surface.
		/// Setting this value too low may cause stair-stepping artifacts in lighting calculations.
		/// </summary>
		public float ShadowBias
		{
			get => lightInstance.ShadowBias;
			set => lightInstance.ShadowBias = value;
		}

		/// <summary>
		/// Gets the world space position of the light source. Only relevant for point and spot lights.
		/// </summary>
		public Vector3 WorldPosition => node.WorldPosition;
		/// <summary>
		/// Gets the world space diretion of light rays coming off this light source. Only relevant for spot and directional lights.
		/// </summary>
		public Vector3 Direction => Vector3.Transform(Vector3.UnitZ, node.WorldRotation);

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			base.Dispose(_disposing);

			lightInstance.Dispose();
		}

		public override void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData)
		{
			if (_eventType == SceneEventType.OnNodeDestroyed ||
				_eventType == SceneEventType.OnDestroyComponent)
			{
				node.scene.drawManager.UnregisterLight(this);
			}
		}

		public static int CompareLightsForSorting(Light _a, Light _b)
		{
			int weightA = _a.lightPriority + (_a.CastShadows ? 1000 : 0);
			int weightB = _b.lightPriority + (_b.CastShadows ? 1000 : 0);
			return weightB.CompareTo(weightA);
		}

		/// <summary>
		/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
		/// </summary>
		public LightSourceData GetLightSourceData()
		{
			lightInstance.worldPose = node.WorldTransformation;
			return lightInstance.GetLightSourceData();
		}

		public bool BeginDrawShadowMap(
			in SceneContext _sceneCtx,
			float _shadingFocalPointRadius,
			uint _newShadowMapIdx)
		{
			lightInstance.worldPose = node.WorldTransformation;
			lightInstance.worldPose.scale = Vector3.One;

			return lightInstance.BeginDrawShadowMap(
				in _sceneCtx,
				_shadingFocalPointRadius,
				_newShadowMapIdx);
		}

		public bool BeginDrawShadowCascade(
			in SceneContext _sceneCtx,
			in CommandList _cmdList,
			in DeviceBuffer _dummyBufLights,
			Vector3 _shadingFocalPoint,
			uint _cascadeIdx,
			out CameraPassContext _outCameraPassCtx,
			bool _rebuildResSetCamera = false,
			bool _texShadowMapsHasChanged = false)
		{
			lightInstance.worldPose = node.WorldTransformation;
			lightInstance.worldPose.scale = Vector3.One;

			return lightInstance.BeginDrawShadowCascade(
				in _sceneCtx,
				in _cmdList,
				in _dummyBufLights,
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
			return !IsDisposed;
		}

		/// <summary>
		/// Check whether light emitted by this light source has any chance of being seen by a given camera.
		/// </summary>
		/// <param name="_camera">The camera whose pixels may or may not be illuminated by this light source.</param>
		/// <returns>True if this instance's light could possible be seen by the camera, false otherwise.</returns>
		public bool CheckVisibilityByCamera(in Camera _camera) => lightInstance.CheckVisibilityByCamera(_camera);

		public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
		{
			if (string.IsNullOrEmpty(_componentData.SerializedData))
			{
				Logger.LogError("Cannot load light from null or blank serialized data!");
				return false;
			}

			// Deserialize camera data from component data's serialized data string:
			if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out LightData? data) || data == null)
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
			layerMask = data.LayerMask;

			if (!lightInstance.LoadFromData(in data))
			{
				return false;
			}

			lightInstance.worldPose = node.WorldTransformation;

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

			data.LayerMask = layerMask;

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
}
