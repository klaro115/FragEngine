using FragEngine3.Graphics.Components.Data;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Graphics.Components
{
	public sealed class Camera : Component
	{
		#region Constructors

		public Camera(SceneNode _node) : base(_node)
		{
			//...
		}

		#endregion
		#region Fields

		// Resolution:
		private int resolutionX = 640;
		private int resolutionY = 480;

		// Projection:
		private float nearClipPlane = 0.01f;
		private float farClipPlane = 1000.0f;
		private float fieldOfViewRad = 60.0f * Deg2Rad;

		private static Camera? mainCamera = null;

		private static object lockObj = new();

		#endregion
		#region Constants

		private const float Rad2Deg = 180.0f / MathF.PI;
		private const float Deg2Rad = MathF.PI / 180.0f;
		
		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the width of the camera's output images in pixels. Must be a value between 1 and 8192.<para/>
		/// NOTE: Some GPU architectures may support only a limited set of output resolutions, though most shouldn't
		/// complain so long as the with is divisible by 8.
		/// </summary>
		public int ResolutionX
		{
			get => resolutionX;
			set => resolutionX = Math.Clamp(value, 1, 8192);
		}
		/// <summary>
		/// Gets or sets the height of the camera's output images in pixels. Must be a value between 1 and 8192.
		/// </summary>
		public int ResolutionY
		{
			get => resolutionY;
			set => resolutionY = Math.Clamp(value, 1, 8192);
		}

		/// <summary>
		/// Gets or sets the nearest clipping distance of the camera; geometry closer than this can't be rendered
		/// and will be clipped. Must be larger than 0 and less than the value of <see cref="FarClipPlane"/>.
		/// </summary>
		public float NearClipPlane
		{
			get => nearClipPlane;
			set => nearClipPlane = Math.Clamp(value, 0.001f, Math.Min(farClipPlane, 99999.9f));
		}
		/// <summary>
		/// Gets or sets the far clipping distance of the camera; geometry further away than this can't be rendered
		/// and will be clipped. Must be larger <see cref="NearClipPlane"/> and less than 100K.
		/// </summary>
		public float FarClipPlane
		{
			get => farClipPlane;
			set => farClipPlane = Math.Clamp(value, Math.Min(nearClipPlane, 0.002f), 100000.0f);
		}

		/// <summary>
		/// Gets or sets the field of view angle in degrees.
		/// </summary>
		public float FieldOfViewDegrees
		{
			get => fieldOfViewRad * Rad2Deg;
			set => fieldOfViewRad = Math.Clamp(value, 0.001f, 179.0f) * Deg2Rad;
		}
		/// <summary>
		/// Gets or sets the field of view angle in radians.
		/// </summary>
		public float FieldOfViewRadians
		{
			get => fieldOfViewRad;
			set => fieldOfViewRad = Math.Clamp(value, 0.001f * Deg2Rad, 179.0f * Deg2Rad);
		}

		/// <summary>
		/// Gets or sets whether this camera is the engine's main camera. This is a global property, so don't set
		/// this if you have multiple scenes that each expect their own main camera.
		/// </summary>
		public bool IsMainCamera
		{
			get => mainCamera == this && !IsDisposed;
			set
			{
				if (IsMainCamera == value) return;
				lock(lockObj)
				{
					mainCamera = value ? this : null;
				}
			}
		}

		/// <summary>
		/// Gets the engine's currently assigned main camera. This may be null if no camera has been marked as the
		/// global main camera, or if the main camera has been disposed. Only rely on this property if your game
		/// only ever has one dedicated main camera across all scenes that can concurrently be active and loaded.
		/// </summary>
		public static Camera? MainCamera
		{
			get { lock (lockObj) { return mainCamera != null && mainCamera.IsMainCamera ? mainCamera : null; }; }
		}

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsMainCamera = false;

			base.Dispose(_disposing);
		}

		public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElementData> _idDataMap)
		{
			if (string.IsNullOrEmpty(_componentData.SerializedData))
			{
				Logger.LogError("Cannot load camera from null or blank serialized data!");
				return false;
			}

			// Deserialize camera data from component data's serialized data string:
			if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out CameraData? data) || data == null)
			{
				Logger.LogError("Failed to deserialize camera component data from JSON!");
				return false;
			}
			if (!data.IsValid())
			{
				Logger.LogError("Deserialize camera component data is invalid!");
				return false;
			}

			resolutionX = data.ResolutionX;
			resolutionY = data.ResolutionY;

			nearClipPlane = data.NearClipPlane;
			farClipPlane = data.FarClipPlane;
			FieldOfViewDegrees = data.FieldOfViewDegrees;

			return true;
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			CameraData data = new()
			{
				ResolutionX = resolutionX,
				ResolutionY = resolutionY,

				NearClipPlane = nearClipPlane,
				FarClipPlane = farClipPlane,
				FieldOfViewDegrees = FieldOfViewDegrees,
			};

			if (!Serializer.SerializeToJson(data, out string dataJson))
			{
				Logger.LogError("Failed to serialize camera component data to JSON!");
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
