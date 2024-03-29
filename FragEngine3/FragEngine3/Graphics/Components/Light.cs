﻿using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
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

		internal readonly GraphicsCore core;

		private LightType type = LightType.Point;
		private bool castShadows = false;
		private uint shadowCascadeCount = 0;

		/// <summary>
		/// Priority rating to indicate which light sources are more important. Higher priority lights will
		/// be drawn first, lower priority light may be ignored as their impact on a mesh may be negligable.
		/// </summary>
		public int lightPriority = 1;
		/// <summary>
		/// Bit mask for all layers that can be affected by this light source.
		/// </summary>
		public uint layerMask = 0xFFu;

		// Light settings:
		public RgbaFloat lightColor = RgbaFloat.White;
		private float lightIntensity = 1.0f;
		private float maxLightRange = 1.0e+8f;
		private float maxLightRangeSq = 1.0e+8f;
		private float spotAngleRad = 30.0f * DEG2RAD;

		// Shadow settings:
		private float shadowBias = 0.02f;

		// Shadow resources:
		private CameraInstance? shadowCameraInstance = null;
		private ShadowCascadeResources[]? shadowCascades = null;
		private uint shadowMapIdx = 0;

		private static readonly bool rotateProjectionAlongCamera = false;

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
					shadowCameraInstance = null;
					DisposeShadowCascades();
				}
			}
		}

		public uint ShadowCascades
		{
			get => shadowCascadeCount;
			set => shadowCascadeCount = type != LightType.Point ? Math.Min(value, 4) : 0;
		}

		public float ShadowBias
		{
			get => shadowBias;
			set => shadowBias = Math.Clamp(value, 0, 10);
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
			DisposeShadowCascades();
		}

		private void DisposeShadowCascades()
		{
			if (shadowCascades != null)
			{
				foreach (ShadowCascadeResources cascade in shadowCascades)
				{
					cascade.Dispose();
				}
				shadowCascades = null;
			}
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
				//mtxShadowWorld2Clip = CastShadows
				//	? shadowCascades![0].mtxShadowWorld2Clip
				//	: Matrix4x4.Identity,
				shadowMapIdx = shadowMapIdx,
				shadowBias = shadowBias,
				shadowCascades = ShadowCascades,
				shadowCascadeRange = ShadowMapUtility.directionalLightSize,
			};
		}

		public bool BeginDrawShadowMap(
			in SceneContext _sceneCtx,
			float _shadingFocalPointRadius,
			uint _newShadowMapIdx)
		{
			if (IsDisposed)
			{
				Logger.LogError("Can't begin drawing shadow map for disposed light source!");
				return false;
			}
			if (!CastShadows)
			{
				return false;
			}
			if (_sceneCtx.texShadowMaps == null || _sceneCtx.texShadowMaps.IsDisposed)
			{
				Logger.LogError("Can't begin drawing shadow map using null shadow map texture array!");
				return false;
			}

			shadowMapIdx = _newShadowMapIdx;

			// Ensure shadow cascades are all ready to go:
			if (shadowCascades == null || shadowCascades.Length < shadowCascadeCount + 1)
			{
				DisposeShadowCascades();

				shadowCascades = new ShadowCascadeResources[shadowCascadeCount + 1];
				for (uint i = 0; i < shadowCascadeCount + 1; ++i)
				{
					shadowCascades[i] = new ShadowCascadeResources(this, i);
				}
			}

			// Ensure a camera instance is ready for drawing the scene:
			if (shadowCameraInstance == null || shadowCameraInstance.IsDisposed)
			{
				if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
					in core,
					null,
					Matrix4x4.Identity,
					type == LightType.Directional,
					_shadingFocalPointRadius,
					spotAngleRad,
					ref shadowCameraInstance))
				{
					return false;
				}
			}

			return true;
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
			// Select the right shadow cascade resource container:
			_cascadeIdx = type == LightType.Directional
				? Math.Min(_cascadeIdx, shadowCascadeCount)
				: 0;

			ShadowCascadeResources cascade = shadowCascades![_cascadeIdx];

			// Recalculate projection for this cascade:
			RecalculateShadowProjectionMatrix(_shadingFocalPoint, _cascadeIdx, out cascade.mtxShadowWorld2Clip);

			// Update framebuffer, constant buffers and resource sets:
			if (!cascade.UpdateResources(
				in _sceneCtx,
				in _dummyBufLights,
				in shadowCameraInstance!,
				shadowMapIdx,
				_rebuildResSetCamera,
				_texShadowMapsHasChanged,
				out bool _,
				out bool _))
			{
				Logger.LogError($"Failed to update shadow cascade resources for cascade {_cascadeIdx} of shadow map index {shadowMapIdx}!");
				_outCameraPassCtx = null!;
				return false;
			}

			if (!shadowCameraInstance!.SetOverrideFramebuffer(cascade.ShadowMapFrameBuffer, true))
			{
				Logger.LogError($"Failed to set framebuffer for shadow cascade {_cascadeIdx} of shadow map index {shadowMapIdx}!");
				_outCameraPassCtx = null!;
				return false;
			}

			// Bind framebuffers and clear targets:
			if (!shadowCameraInstance!.BeginDrawing(_cmdList, true, false, out _))
			{
				Logger.LogError("Failed to begin drawing light component's shadow map!");
				_outCameraPassCtx = null!;
				return false;
			}

			// Assemble context object for renderers to reference when issuing draw calls:
			_outCameraPassCtx = new(
				shadowCameraInstance!,
				_cmdList,
				cascade.ShadowMapFrameBuffer!,
				cascade.ShadowResSetCamera!,
				cascade.ShadowCbCamera!,
				_dummyBufLights,
				0,
				shadowMapIdx,
				0,
				0,
				in cascade.mtxShadowWorld2Clip);

			return true;
		}

		public bool EndDrawShadowCascade()
		{
			return !IsDisposed && shadowCameraInstance != null && shadowCameraInstance.EndDrawing();
		}

		public bool EndDrawShadowMap()
		{
			return !IsDisposed && shadowCameraInstance != null;
		}

		private void RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, uint _shadowCascadeIdx, out Matrix4x4 _outMtxShadowWorld2Clip)
		{
			switch (type)
			{
				case LightType.Point:
					{
						// NOTE: Not supported at this time, as there is no linear way of evenly projecting a sphere surface to a square framebuffer.
						// Yes, I know that cubemaps exist, but I kind of don't feel like doing that just yet. Might repurpose cascade-like mapping for it though...
						_outMtxShadowWorld2Clip = Matrix4x4.Identity;
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
						_outMtxShadowWorld2Clip = mtxWorld2Local * mtxLocal2Clip;
					}
					break;
				case LightType.Directional:
					{
						float maxDirectionalRange = ShadowMapUtility.directionalLightSize * MathF.Floor(MathF.Pow(2, _shadowCascadeIdx));

						Vector3 lightDir = Direction;
						Quaternion worldRot = node.WorldRotation;
						
						// Orient light map projection to have its pixel grid roughly aligned with the camera's direction:
						if (rotateProjectionAlongCamera && Camera.MainCamera != null)
						{
							Vector3 worldUp = Vector3.Transform(Vector3.UnitY, worldRot);
							Vector3 cameraDir = Camera.MainCamera.node.WorldForward;

							Vector3 lightDirProj = Vector3.Normalize(VectorExt.ProjectToPlane(cameraDir, lightDir));
							float cameraRotAngle = VectorExt.Angle(worldUp, lightDirProj, true);
							Quaternion cameraRot = Quaternion.CreateFromAxisAngle(lightDir, cameraRotAngle);
							worldRot = cameraRot * worldRot;
						}

						// Transform from a world space position (relative to a given focal point), to orthographics projection space, to shadow map UV coordinates:
						Vector3 posOrigin = _shadingFocalPoint - lightDir * maxDirectionalRange * 0.5f;
						Pose originPose = new(posOrigin, worldRot, Vector3.One, false);
						if (!Matrix4x4.Invert(originPose.Matrix, out Matrix4x4 mtxWorld2Local))
						{
							mtxWorld2Local = Matrix4x4.Identity;
						}
						Matrix4x4 mtxLocal2Clip = Matrix4x4.CreateOrthographicLeftHanded(maxDirectionalRange, maxDirectionalRange, 0.01f, maxDirectionalRange);     //TODO [later]: this works, but it's pretty bad.
						_outMtxShadowWorld2Clip = mtxWorld2Local * mtxLocal2Clip;
					}
					break;
				default:
					{
						_outMtxShadowWorld2Clip = Matrix4x4.Identity;
					}
					break;
			}

			_outMtxShadowWorld2Clip *= Matrix4x4.CreateScale(1, -1, 1);
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
			ShadowCascades = data.ShadowCascades;
			shadowBias = data.ShadowBias;

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
				ShadowCascades = shadowCascadeCount,
				ShadowBias = shadowBias,
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
