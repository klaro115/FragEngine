using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Utility;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
    public sealed class Light : Component
	{
		#region Types

		/// <summary>
		/// Enumeration of different supported light types.
		/// This dictates how a light works, in which direction and from which origin light rays are cast.
		/// </summary>
		public enum LightType : uint
		{
			/// <summary>
			/// Point-shaped light source. All light rays are cast uniformely in all directions, starting
			/// from a single point.
			/// </summary>
			Point			= 0,
			/// <summary>
			/// Cone-shaped light source. All light rays are cast from a single point and in one general
			/// direction, with rays distributed evenly across a given angle.
			/// </summary>
			Spot			= 1,
			/// <summary>
			/// Directional sun-like light source. Light rays are cast following a single direction across
			/// the entire world space. The light does not attenuate over increasing distances, and is not
			/// tied to the source's position.
			/// </summary>
			Directional		= 2,
		}

		#endregion
		#region Constructors

		/// <summary>
		/// Creates a new light source component and registers it with its host scene.
		/// </summary>
		/// <param name="_node">The scene node which the component will be attached to.</param>
		public Light(SceneNode _node) : base(_node)
		{
			core = node.scene.GraphicsStack?.Core ?? node.scene.engine.GraphicsSystem.graphicsCore;

			node.scene.drawManager.RegisterLight(this);
		}

		#endregion
		#region Fields

		private readonly GraphicsCore core;

		private LightType type = LightType.Point;
		private bool castShadows = false;

		/// <summary>
		/// Priority rating to indicate which light sources are more important. Higher priority lights will
		/// be drawn first, lower priority light may be ignored as their impact on a mesh may be negligable.
		/// </summary>
		public int lightPriority = 1;
		/// <summary>
		/// Bit mask for all layers that can be affected by this light source.
		/// </summary>
		public uint layerMask = 0xFFu;

		public RgbaFloat lightColor = RgbaFloat.White;
		private float lightIntensity = 1.0f;
		private float maxLightRange = 1.0e+8f;
		private float maxLightRangeSq = 1.0e+8f;
		private float spotAngleRad = 30.0f * DEG2RAD;

		// Shadow maps:
		private CameraInstance? shadowCameraInstance = null;
		private Framebuffer? shadowMapFrameBuffer = null;
		private DeviceBuffer? shadowCbCamera = null;
		private ResourceSet? shadowResSetCamera = null;
		private Matrix4x4 mtxShadowWorld2Clip = Matrix4x4.Identity;
		private uint shadowMapIdx = 0;

		#endregion
		#region Constants

		private const float MIN_LIGHT_INTENSITY = 0.001f;

		private const float DEG2RAD = MathF.PI / 180.0f;
		private const float RAD2DEG = 180.0f / MathF.PI;

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
			get => type;
			set
			{
				type = value;
				maxLightRange = type != LightType.Directional ? MathF.Sqrt(lightIntensity / MIN_LIGHT_INTENSITY) : 1.0e+8f;
				maxLightRangeSq = maxLightRange * maxLightRange;
			}
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
			get => castShadows && type != LightType.Point;
			set
			{
				castShadows = value;
				if (!castShadows)
				{
					shadowMapIdx = 0;

					shadowCameraInstance?.Dispose();
					shadowMapFrameBuffer?.Dispose();
					shadowCbCamera?.Dispose();
					shadowCameraInstance = null;
					shadowMapFrameBuffer = null;
					shadowCbCamera = null;
				}
			}
		}

		/// <summary>
		/// Gets or sets the intensity of light emitted by this light source. TODO: Figure out which unit to use for this.
		/// </summary>
		public float LightIntensity
		{
			get => lightIntensity;
			set
			{
				lightIntensity = Math.Max(value, 0.0f);
				maxLightRange = type != LightType.Directional ? MathF.Sqrt(lightIntensity / MIN_LIGHT_INTENSITY) : 1.0e+8f;
				maxLightRangeSq = maxLightRange * maxLightRange;
			}
		}
		/// <summary>
		/// Gets the maximum range out to which this light source produces any noticeable brightness. 
		/// </summary>
		public float MaxLightRange => maxLightRange;
		/// <summary>
		/// Gets the squared maximum range out to which this light source produces any noticeable brightness. 
		/// </summary>
		public float MaxLightRangeSquared => maxLightRangeSq;

		/// <summary>
		/// Gets or sets the angle in which spot lights cast their light, in radians.
		/// </summary>
		public float SpotAngleRadians
		{
			get => spotAngleRad;
			set => spotAngleRad = Math.Max(value, 0.0f);
		}
		/// <summary>
		/// Gets or sets the angle in which spot lights cast their light, in degrees.
		/// </summary>
		public float SpotAngleDegrees
		{
			get => spotAngleRad * RAD2DEG;
			set => spotAngleRad = Math.Max(value, 0.0f) * DEG2RAD;
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

			shadowCameraInstance?.Dispose();
			shadowMapFrameBuffer?.Dispose();
			shadowCbCamera?.Dispose();
			shadowResSetCamera?.Dispose();
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
			int weightA = _a.lightPriority + (_a.castShadows ? 1000 : 0);
			int weightB = _b.lightPriority + (_b.castShadows ? 1000 : 0);
			return weightB.CompareTo(weightA);
		}

		/// <summary>
		/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
		/// </summary>
		public LightSourceData GetLightSourceData()
		{
			float spotMinDot = type == LightType.Spot
				? MathF.Cos(spotAngleRad * 0.5f)
				: 0;

			return new()
			{
				color = new Vector3(lightColor.R, lightColor.G, lightColor.B),
				intensity = lightIntensity,
				position = WorldPosition,
				type = (uint)type,
				direction = type != LightType.Point ? Direction : Vector3.UnitZ,
				spotMinDot = spotMinDot,
				mtxShadowWorld2Clip = mtxShadowWorld2Clip,
				shadowMapIdx = shadowMapIdx,
				lightMaxRange = maxLightRange,
			};
		}

		public bool BeginDrawShadowMap(
			in SceneContext _sceneCtx,
			in CommandList _cmdList,
			in DeviceBuffer _dummyBufLights,
			Vector3 _shadingFocalPoint,
			float _shadingFocalPointRadius,
			uint _newShadowMapIdx,
			out CameraPassContext _outCameraPassCtx,
			bool _rebuildResSetCamera = false,
			bool _texShadowMapsHasChanged = false)
		{
			if (IsDisposed)
			{
				_outCameraPassCtx = null!;
				Logger.LogError("Can't begin drawing shadow map for disposed light source!");
				return false;
			}
			if (!CastShadows)
			{
				_outCameraPassCtx = null!;
				return false;
			}
			if (_sceneCtx.texShadowMaps == null || _sceneCtx.texShadowMaps.IsDisposed)
			{
				Logger.LogError("Can't begin drawing shadow map using null shadow map texture array!");
				_outCameraPassCtx = null!;
				return false;
			}

			// Recalculate projection matrix for 
			RecalculateShadowProjectionMatrix(_shadingFocalPoint, _shadingFocalPointRadius);
			shadowMapIdx = _newShadowMapIdx;

			// Ensure render targets are created and assigned:
			if (_texShadowMapsHasChanged || shadowMapFrameBuffer == null || shadowMapFrameBuffer.IsDisposed)
			{
				_rebuildResSetCamera = true;

				shadowCameraInstance?.Dispose();
				shadowCameraInstance = null;

				FramebufferAttachmentDescription depthTargetDesc = new(_sceneCtx.texShadowMaps, shadowMapIdx, 0);
				FramebufferDescription shadowMapFrameBufferDesc = new(depthTargetDesc, []);

				try
				{
					shadowMapFrameBuffer = core.MainFactory.CreateFramebuffer(ref shadowMapFrameBufferDesc);
					shadowMapFrameBuffer.Name = $"Framebuffer_ShadowMap_Layer{shadowMapIdx}";
				}
				catch (Exception ex)
				{
					shadowMapFrameBuffer?.Dispose();
					shadowMapFrameBuffer = null;
					Logger.LogException("Failed to create framebuffer for drawing light component's shadow map!", ex);
					_outCameraPassCtx = null!;
					return false;
				}
			}

			// Ensure a camera instance is ready for drawing the scene:
			if (shadowCameraInstance == null || shadowCameraInstance.IsDisposed)
			{
				if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
					in core,
					in shadowMapFrameBuffer,
					in mtxShadowWorld2Clip,
					type == LightType.Directional,
					_shadingFocalPointRadius,
					spotAngleRad,
					ref shadowCameraInstance))
				{
					_outCameraPassCtx = null!;
					return false;
				}
			}
			if (_texShadowMapsHasChanged)
			{
				shadowCameraInstance!.SetOverrideFramebuffer(shadowMapFrameBuffer, true);
			}

			// Update or create global constant buffer with scene and camera information for the shaders:
			if (!CameraUtility.UpdateConstantBuffer_CBCamera(
				in shadowCameraInstance!,
				node.WorldTransformation,
				in mtxShadowWorld2Clip,
				shadowMapIdx,
				0,
				0,
				ref shadowCbCamera,
				out bool cbCameraChanged))
			{
				Logger.LogError("Failed to update camera constant buffer for drawing light component's shadow map!");
				_outCameraPassCtx = null!;
				return false;
			}
			_rebuildResSetCamera |= cbCameraChanged;

			// Camera's default resource set:
			if (!CameraUtility.UpdateOrCreateCameraResourceSet(
				in core,
				in _sceneCtx,
				in shadowCbCamera!,
				in _dummyBufLights!,
				ref shadowResSetCamera,
				_rebuildResSetCamera))
			{
				Logger.LogError("Failed to allocate or update camera's default resource set!");
				_outCameraPassCtx = null!;
				return false;
			}

			// Assemble context object for renderers to reference when issuing draw calls:
			_outCameraPassCtx = new(
				shadowCameraInstance!,
				_cmdList,
				shadowMapFrameBuffer,
				shadowResSetCamera!,
				shadowCbCamera!,
				_dummyBufLights,
				0,
				_newShadowMapIdx,
				0,
				0);

			// Bind framebuffers and clear targets:
			if (!shadowCameraInstance!.BeginDrawing(_cmdList, true, false, out _))
			{
				Logger.LogError("Failed to begin drawing light component's shadow map!");
				return false;
			}

			return true;
		}

		public bool EndDrawShadowMap()
		{
			return !IsDisposed && shadowCameraInstance != null && shadowCameraInstance.EndDrawing();
		}

		private void RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, float _shadingFocalPointRadius)
		{
			switch (type)
			{
				case LightType.Point:
					{
						// NOTE: Not supported at this time, as there is no linear way of evenly projecting a sphere surface to a square framebuffer.
						mtxShadowWorld2Clip = Matrix4x4.Identity;
					}
					break;
				case LightType.Spot:
					{
						// Transform from a world space position, to the light's local space, to perspective projection clip space, to shadow map UV coordinates:
						if (!Matrix4x4.Invert(node.WorldTransformation.Matrix, out Matrix4x4 mtxWorld2Local))
						{
							mtxWorld2Local = Matrix4x4.Identity;
						}
						Matrix4x4 mtxLocal2Clip = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(spotAngleRad, 1, 0.01f, MaxLightRange);
						mtxShadowWorld2Clip = mtxWorld2Local * mtxLocal2Clip;
					}
					break;
				case LightType.Directional:
					{
						// Transform from a world space position (relative to a given focal point), to orthographics projection space, to shadow map UV coordinates:
						float maxDirectionalRange = _shadingFocalPointRadius;
						Matrix4x4 mtxWorld2Focal = Matrix4x4.CreateTranslation(_shadingFocalPoint - Direction * maxDirectionalRange * 0.5f);
						Matrix4x4 mtxFocal2Clip = Matrix4x4.CreateOrthographicLeftHanded(_shadingFocalPointRadius, _shadingFocalPointRadius, 0.01f, maxDirectionalRange);
						mtxShadowWorld2Clip = mtxWorld2Focal * mtxFocal2Clip;
					}
					break;
				default:
					{
						mtxShadowWorld2Clip = Matrix4x4.Identity;
					}
					break;
			}

			mtxShadowWorld2Clip *= Matrix4x4.CreateScale(1, -1, 1);
		}

		/// <summary>
		/// Check whether light emitted by this light source has any chance of being seen by a given camera.
		/// </summary>
		/// <param name="_camera">The camera whose pixels may or may not be illuminated by this light source.</param>
		/// <returns>True if this instance's light could possible be seen by the camera, false otherwise.</returns>
		public bool CheckVisibilityByCamera(in Camera _camera)
		{
			if (_camera == null) return false;

			return true;	//TEMP / TODO [later]
		}

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

			type = data.Type;
			layerMask = data.LayerMask;

			lightColor = data.LightColor;
			lightIntensity = data.LightIntensity;
			SpotAngleDegrees = data.SpotAngleDegrees;

			CastShadows = data.CastShadows;

			// Re-register camera with the scene:
			node.scene.drawManager.UnregisterLight(this);
			return node.scene.drawManager.RegisterLight(this);
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			LightData data = new()
			{
				Type = type,
				LayerMask = layerMask,

				LightColor = lightColor,
				LightIntensity = lightIntensity,
				SpotAngleDegrees = SpotAngleDegrees,

				CastShadows = castShadows,
			};

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
