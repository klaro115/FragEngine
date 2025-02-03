using FragEngine3.EngineCore.Input;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using System.Numerics;
using Veldrid;

namespace TestApp.Camera;

/// <summary>
/// A simple camera flight component.<para/>
/// Controls:<para/>
/// - WASD keys to move<para/>
/// - QE to go up or down<para/>
/// - RMB to look around
/// </summary>
/// <param name="_node"></param>
internal sealed class CameraFlightComponent(SceneNode _node) : Component(_node), IOnLateUpdateListener
{
	#region Fields

	private readonly InputManager inputManager = _node.scene.engine.InputManager;
	private readonly TimeManager timeManager = _node.scene.engine.TimeManager;

	private float cameraYaw = 0.0f;
	private float cameraPitch = 0.0f;

	#endregion
	#region Methods

	public bool OnLateUpdate()
	{
		float deltaTime = (float)timeManager.DeltaTime.TotalSeconds;

		Pose p =node.LocalTransformation;
		Vector3 inputWASD = inputManager.GetKeyAxesSmoothed(InputAxis.WASD);
		Vector3 localMovement = new Vector3(inputWASD.X, inputWASD.Z, inputWASD.Y) * deltaTime;
		if (inputManager.GetKey(Key.ShiftLeft))
		{
			localMovement *= 3;
		}
		Vector3 cameraMovement = p.TransformDirection(localMovement);
		p.Translate(cameraMovement);

		if (inputManager.GetMouseButton(MouseButton.Right))
		{
			const float DEG2RAD = MathF.PI / 180.0f;
			const float mouseDegreesPerPixel = 0.1f;
			Vector2 mouseMovement = inputManager.MouseMovement * mouseDegreesPerPixel;
			cameraYaw += mouseMovement.X;
			cameraPitch = Math.Clamp(cameraPitch + mouseMovement.Y, -89, 89);
			p.rotation = Quaternion.CreateFromYawPitchRoll(cameraYaw * DEG2RAD, cameraPitch * DEG2RAD, 0);
		}

		node.LocalTransformation = p;
		return true;
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		return true;
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		_componentData = new()
		{
			SerializedData = string.Empty,
		};
		return true;
	}

	#endregion
}
