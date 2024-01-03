using FragEngine3.EngineCore.Input;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Utility;
using System.Numerics;
using Veldrid;
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
			if (Engine.ResourceManager.GetResource("Mtl_DefaultSurface", out handle))
			{
				handle.Load(true);
			}
			if (Engine.ResourceManager.GetResource("Mtl_TestMaterial", out handle))
			{
				handle.Load(true);
			}
			if (Engine.ResourceManager.GetResource("ForwardPlusLight_Composition_PS", out handle))
			{
				handle.Load(true);
			}

			Scene scene = Engine.SceneManager.MainScene!;

			// Create a camera:
			SceneNode cameraNode = scene.rootNode.CreateChild("Camera");
			cameraNode.LocalPosition = new(0, 0, -5);
			cameraNode.LocalRotation = Quaternion.Identity;
			cameraNode.LocalScale = Vector3.One;
			//if (cameraNode.CreateComponent(out Camera? camera, RenderMode.Opaque) && camera != null)
			if (cameraNode.CreateComponent(out Camera? camera) && camera != null)
			{
				Sdl2Window window = Engine.GraphicsSystem.graphicsCore.Window;
				Graphics.Cameras.CameraSettings cameraSettings = new()
				{
					ResolutionX = (uint)window.Width,
					ResolutionY = (uint)window.Height,
					//ColorFormat = PixelFormat.B8_G8_R8_A8_UNorm,
					//ColorFormat = PixelFormat.R16_G16_B16_A16_UNorm,

					ProjectionType = CameraProjectionType.Perspective,
					FieldOfViewDegrees = 60.0f,
					NearClipPlane = 0.1f,
					FarClipPlane = 1000.0f,
					OrthographicSize = 5,
			
					ClearColor = true,
					ClearDepth = true,
					ClearColorValue = RgbaFloat.CornflowerBlue,
					ClearDepthValue = 1.0f,
				};

				camera.Settings = cameraSettings;
				camera.IsMainCamera = true;
			}

			// Create a directional light:
			SceneNode lightNode = scene.rootNode.CreateChild("Directional Light");
			if (lightNode.CreateComponent(out Light? light) && light != null)
			{
				light.Type = Light.LightType.Directional;
				light.node.WorldPosition = new Vector3(0, 5, 0);
				light.node.SetRotationFromYawPitchRoll(45, 45, 0, true, true);
			}

			SceneNode rabbitNode = scene.rootNode.CreateChild("Rabbit");
			rabbitNode.LocalPosition = new Vector3(0, -0.25f, 0);
			rabbitNode.LocalRotation = Quaternion.Identity;
			rabbitNode.LocalScale = Vector3.One * 5;
			rabbitNode.SetEnabled(false);
			if (rabbitNode.CreateComponent(out StaticMeshRenderer? rabbitRenderer) && rabbitRenderer != null)
			{
				rabbitRenderer.SetMesh("Rabbit.obj");
				rabbitRenderer.SetMaterial("Mtl_DefaultSurface");
				//rabbitRenderer.SetMaterial("Mtl_TestMaterial");
			}

			MeshPrimitiveFactory.CreateCubeMesh("Cube", Engine, new(1, 1, 1), false, out _, out _, out ResourceHandle cubeHandle);
			SceneNode cubeNode = scene.rootNode.CreateChild("Cube");
			cubeNode.LocalPosition = Vector3.Zero;
			cubeNode.LocalRotation = Quaternion.Identity;
			cubeNode.LocalScale = Vector3.One;
			cubeNode.SetEnabled(false);
			if (cubeNode.CreateComponent(out StaticMeshRenderer? cubeRenderer) && cubeRenderer != null)
			{
				//cubeRenderer.SetMesh("Cube.obj");
				cubeRenderer.SetMesh(cubeHandle);
				//cubeRenderer.SetMaterial("Mtl_DefaultSurface");
				cubeRenderer.SetMaterial("Mtl_TestMaterial");
			}

			// Create two-sided quad:
			MeshSurfaceData quadData = new()
			{
				verticesBasic =
				[
					new BasicVertex(new(-1, -1, 0), new(0, 0, -1), new(0, 0)),
					new BasicVertex(new( 1, -1, 0), new(0, 0, -1), new(1, 0)),
					new BasicVertex(new(-1,  1, 0), new(0, 0, -1), new(0, 1)),
					new BasicVertex(new( 1,  1, 0), new(0, 0, -1), new(1, 1)),
				],
				indices16 =
				[
					0, 2, 1,
					2, 3, 1,
				],
			};
			StaticMesh quadMesh = new("Quad", Engine, false, out ResourceHandle quadHandle);
			quadMesh.SetGeometry(in quadData);
			SceneNode quadNode = scene.rootNode.CreateChild("Quad");
			quadNode.LocalTransformation = Pose.Identity;
			quadNode.LocalPosition = new(0, 0, 2);
			quadNode.LocalScale = Vector3.One * 5;
			//quadNode.SetEnabled(false);
			if (quadNode.CreateComponent(out StaticMeshRenderer? quadRenderer) && quadRenderer != null)
			{
				quadRenderer.SetMesh(quadHandle);
				quadRenderer.SetMaterial("Mtl_DefaultSurface");
				//quadRenderer.SetMaterial("Mtl_TestMaterial");
			}

			return true;
		}

		public override bool UpdateRunningState()
		{
			if (Engine.InputManager.GetMouseButtonUp(Veldrid.MouseButton.Right) ||
				Engine.InputManager.GetKeyUp(Veldrid.Key.Escape))
			{
				Engine.Exit();
			}

			Scene scene = Engine.SceneManager.MainScene!;
			
			if (scene.FindNode("Rabbit", out SceneNode? rabbitNode) && rabbitNode != null)
			{
				float radPerSec = 2 * MathF.PI / 10;
				float deltaTime = (float)Engine.TimeManager.DeltaTime.TotalSeconds;
				//float time = (float)Engine.TimeManager.RunTime.TotalSeconds;
			
				Pose localPose = rabbitNode.LocalTransformation;
				//localPose.position = new(0, -5, MathF.Sin(time) + 10);
				localPose.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, radPerSec * deltaTime));
				//localPose.scale = Vector3.One * 0.01f;
				rabbitNode.LocalTransformation = localPose;
			}

			if (scene.FindNode("Cube", out SceneNode? quadNode) && quadNode != null)
			{
				float dt = (float)Engine.TimeManager.DeltaTime.TotalSeconds * 5;
				Pose localPose = quadNode.LocalTransformation;
				Vector3 input = Engine.InputManager.GetKeyAxesSmoothed(InputAxis.WASD);
				localPose.Rotate(Quaternion.CreateFromYawPitchRoll(input.X * dt, input.Y * dt, input.Z * dt));
				quadNode.LocalTransformation = localPose;
			}

			//const float DEG2RAD = MathF.PI / 180.0f;
			//const float radius = 2;
			//float angle = (float)Engine.TimeManager.RunTime.TotalSeconds * 90 * DEG2RAD;
			//SceneNode camNode = Camera.MainCamera!.node;
			//camNode.LocalPosition = new(MathF.Cos(angle) * radius, 0, -5);
			//camNode.LocalPosition = new(MathF.Cos(angle) * radius, 0, MathF.Sin(angle) * radius);
			//camNode.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle);
			//Matrix4x4 mtxLookAt = Matrix4x4.CreateLookAtLeftHanded(camNode.LocalPosition, Vector3.Zero, Vector3.UnitY);
			//camNode.LocalRotation = Quaternion.CreateFromRotationMatrix(mtxLookAt);

			return true;
		}

		public override bool DrawRunningState()
		{
			return true;
		}

		protected override bool EndRunningState()
		{
			// Save scene to file when the game exits:
			try
			{
				string saveDirPath = Path.Combine(Engine.ResourceManager.fileGatherer.applicationPath, "saves");
				if (!Directory.Exists(saveDirPath))
				{
					Directory.CreateDirectory(saveDirPath);
				}
				string saveFilePath = Path.Combine(saveDirPath, "test.json");
				SceneSerializer.SaveSceneToFile(Engine.SceneManager.MainScene!, saveFilePath, out _, false);
			}
			catch (Exception ex)
			{
				Engine.Logger.LogException("Failed to save scene to file!", ex);
			}

			return true;
		}

		#endregion
	}
}
