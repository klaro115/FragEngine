using FragEngine3.Graphics.Components.Data;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
	public sealed class Light : Component
	{
		#region Types

		public enum LightType : uint
		{
			Point			= 0,
			Spot,
			Directional,
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
		public struct LightSourceData
		{
			public Vector3 color;
			public float intensity;
			public Vector3 position;
			public uint type;
			public Vector3 direction;
			public float spotAngleAcos;

			public const int byteSize = 3 * 3 * sizeof(float) + 2 * sizeof(float) + sizeof(uint);	// 48 bytes
		}

		#endregion
		#region Constructors

		public Light(SceneNode _node) : base(_node)
		{
			node.scene.drawManager.RegisterLight(this);
		}

		#endregion
		#region Fields

		private LightType type = LightType.Point;

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
		/// Gets the world space position of the light source. Only relevant for point and spot lights.
		/// </summary>
		public Vector3 WorldPosition => node.WorldPosition;
		/// <summary>
		/// Gets the world space diretion of light rays coming off this light source. Only relevant for spot and directional lights.
		/// </summary>
		public Vector3 Direction => Vector3.Transform(Vector3.UnitZ, node.WorldRotation);

		#endregion
		#region Methods

		public override void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData)
		{
			if (_eventType == SceneEventType.OnNodeDestroyed ||
				_eventType == SceneEventType.OnDestroyComponent)
			{
				node.scene.drawManager.UnregisterLight(this);
			}
		}

		/// <summary>
		/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
		/// </summary>
		public LightSourceData GetLightSourceData()
		{
			return new()
			{
				color = new Vector3(lightColor.R, lightColor.G, lightColor.B),
				intensity = lightIntensity,
				position = WorldPosition,
				type = (uint)type,
				direction = type != LightType.Point ? Direction : Vector3.UnitZ,
				spotAngleAcos = type == LightType.Spot ? MathF.Acos(spotAngleRad * 0.5f) : 0,
			};
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
