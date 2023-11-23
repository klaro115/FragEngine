using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid.Sdl2;

namespace FragEngine3.EngineCore.Test
{
	public sealed class TestApplicationLogic : ApplicationLogic
	{
		#region Methods

		// STARTUP:

		protected override bool RunStartupLogic()
		{
			return true;
		}

		// SHUTDOWN:

		protected override bool RunShutdownLogic()
		{
			return true;
		}

		// LOADING:

		protected override bool BeginLoadingState()
		{
			// Create an empty scene:
			Scene scene = new(Engine, "Test")
			{
				UpdatedInEngineStates = EngineState.Running,
				DrawnInEngineStates = EngineState.Running,
			};

			Engine.SceneManager.AddScene(scene);

			return true;
		}

		protected override bool EndLoadingState()
		{
			// Create scene's graphics stack:
			ForwardPlusLightsStack stack = new(Engine.GraphicsSystem.graphicsCore);

			if (Engine.SceneManager.MainScene != null)
			{
				Engine.SceneManager.MainScene.GraphicsStack = stack;
			}

			return true;
		}

		// UNLOADING:

		protected override bool BeginUnloadingState()
		{
			return true;
		}

		// RUNNING:

		protected override bool BeginRunningState()
		{
			// Import 3D models:
			if (Engine.ResourceManager.GetResource("Cube.obj", out ResourceHandle handle))
			{
				handle.Load(true);
			}
			if (Engine.ResourceManager.GetResource("Rabbit.obj", out handle))
			{
				handle.Load(true);
			}

			Scene scene = Engine.SceneManager.MainScene!;

			// Create a camera:
			SceneNode cameraNode = scene.rootNode.CreateChild("Camera");
			if (cameraNode.CreateComponent(out Camera? camera) && camera != null)
			{
				Sdl2Window window = Engine.GraphicsSystem.graphicsCore.Window;
				camera.ResolutionX = (uint)window.Width;
				camera.ResolutionY = (uint)window.Height;

				camera.FieldOfViewDegrees = 60.0f;
				camera.NearClipPlane = 0.1f;
				camera.FarClipPlane = 1000.0f;

				camera.clearBackground = true;
				camera.clearColor = Graphics.Color32.Cornflower;
				camera.clearDepth = 1.0e+8f;
			}

			// Create a directional light:
			SceneNode lightNode = scene.rootNode.CreateChild("Directional Light");
			if (lightNode.CreateComponent(out Light? light) && light != null)
			{
				light.Type = Light.LightType.Directional;
				light.node.WorldPosition = new Vector3(0, 5, 0);
				light.node.WorldRotation = Quaternion.CreateFromYawPitchRoll(45, 45, 0);
			}

			// Create a static mesh renderer displaying a default-shaded cube:
			SceneNode cubeNode = scene.rootNode.CreateChild("Cube");
			cubeNode.LocalPosition = new(0.4f, -0.5f, 5.0f);
			cubeNode.LocalScale = Vector3.One;
			if (cubeNode.CreateComponent(out StaticMeshRenderer? cubeRenderer) && cubeRenderer != null)
			{
				cubeRenderer.SetMesh("Cube.obj");
				cubeRenderer.SetMaterial("Default.mtl");
			}

			return true;
		}

		public override bool UpdateRunningState()
		{
			return true;
		}

		protected override bool EndRunningState()
		{
			return true;
		}

		#endregion
	}
}
