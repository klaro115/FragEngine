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
		#region Fields

		private float cameraYaw = 0.0f;
		private float cameraPitch = 0.0f;

		#endregion
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
			Engine.ResourceManager.GetAndLoadResource("Mtl_TestMaterial", true, out _);
			Engine.ResourceManager.GetAndLoadResource("Mtl_DefaultSurface", true, out _);
			Engine.ResourceManager.GetAndLoadResource("ForwardPlusLight_Composition_PS", true, out _);

			Scene scene = Engine.SceneManager.MainScene!;
			
			// Set ambient lighting:
			scene.settings.AmbientLightIntensityLow = new(0.18f, 0.16f, 0.12f);
			scene.settings.AmbientLightIntensityMid = new(0.15f, 0.15f, 0.15f);
			scene.settings.AmbientLightIntensityHigh = new(0.17f, 0.17f, 0.25f);

			// Create a camera:
			if (SceneSpawner.CreateCamera(scene, true, out Camera camera))
			{
				camera.node.LocalPosition = new Vector3(0, 0, -3);
				camera.node.LocalRotation = Quaternion.Identity;
				camera.node.LocalScale = Vector3.One;

				Sdl2Window window = Engine.GraphicsSystem.graphicsCore.Window;
				camera.Settings = new()
				{
					ResolutionX = (uint)window.Width,
					ResolutionY = (uint)window.Height,
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
				camera.IsMainCamera = true;
				camera.MarkDirty();
			}

			// Create a directional light:
			if (SceneSpawner.CreateLight(scene, Light.LightType.Directional, out Light light))
			{
				light.node.WorldPosition = new Vector3(0, 5, 0);
				light.node.SetRotationFromYawPitchRoll(22.5f, 45, 0, true, true);
				light.node.SetEnabled(false);

				light.CastShadows = false;
			}
			if (SceneSpawner.CreateLight(scene, Light.LightType.Directional, out light))
			{
				light.node.WorldPosition = new Vector3(0, 5, 0);
				light.node.SetRotationFromYawPitchRoll(-70, -30, 0, true, true);
				light.node.SetEnabled(false);
			}
			// Create a spot light:
			if (SceneSpawner.CreateLight(scene, Light.LightType.Spot, out light))
			{
				light.node.WorldPosition = new Vector3(0, 0, -3);
				light.node.LocalRotation = Quaternion.Identity;
				//light.node.SetEnabled(false);

				light.LightIntensity = 15;
				light.SpotAngleDegrees = 35;
				light.CastShadows = true;
			}
			if (SceneSpawner.CreateLight(scene, Light.LightType.Spot, out light))
			{
				light.node.WorldPosition = new Vector3(-4, 5, -4);
				light.node.SetRotationFromYawPitchRoll(22.5f, 45, 0, true, true);
				//light.node.SetEnabled(false);

				//light.lightColor = RgbaFloat.Red;
				light.LightIntensity = 15;
				light.SpotAngleDegrees = 30;
				light.CastShadows = true;
			}

			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer rabbit))
			{
				rabbit.node.Name = "Rabbit";
				rabbit.node.LocalPosition = new Vector3(0, -1.5f, 2);
				rabbit.node.LocalRotation = Quaternion.Identity;
				rabbit.node.LocalScale = Vector3.One * 3;
				//rabbit.node.SetEnabled(false);

				rabbit.SetMesh("Rabbit.obj");
				rabbit.SetMaterial("Mtl_DefaultSurface");
				rabbit.DontDrawUnlessFullyLoaded = true;
			}

			MeshPrimitiveFactory.CreateCubeMesh("Cube", Engine, new(1, 1, 1), false, out _, out _, out ResourceHandle cubeHandle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer cube))
			{
				cube.node.Name = "Cube";
				cube.node.LocalPosition = new Vector3(2.5f, 0, 2);
				cube.node.SetRotationFromYawPitchRoll(45, 45, 0, true, true);
				cube.node.LocalScale = Vector3.One;
				//cube.node.SetEnabled(false);

				cube.SetMesh(cubeHandle);
				cube.SetMaterial("Mtl_DefaultSurface");
			}

			MeshPrimitiveFactory.CreateCylinderMesh("Cylinder", Engine, 0.5f, 2, 32, false, out _, out _, out ResourceHandle cylinderHandle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer cylinder))
			{
				cylinder.node.Name = "Cylinder";
				cylinder.node.LocalPosition = new Vector3(-2.5f, 0, 2);
				cylinder.node.LocalRotation = Quaternion.Identity;
				cylinder.node.LocalScale = Vector3.One;
				//cylinder.node.SetEnabled(false);

				cylinder.SetMesh(cylinderHandle);
				cylinder.SetMaterial("Mtl_DefaultSurface");
			}

			MeshPrimitiveFactory.CreateConeMesh("Cone", Engine, 0.75f, 1, 32, false, out _, out _, out ResourceHandle coneHandle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer cone))
			{
				cone.node.Name = "Cone";
				cone.node.LocalPosition = new Vector3(0, 1.5f, 2);
				cone.node.LocalRotation = Quaternion.Identity;
				cone.node.LocalScale = Vector3.One;
				//cone.node.SetEnabled(false);

				cone.SetMesh(coneHandle);
				cone.SetMaterial("Mtl_DefaultSurface");
			}

			MeshPrimitiveFactory.CreateIcosahedronMesh("Icosahedron", Engine, 0.75f, false, out _, out _, out ResourceHandle d20Handle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer d20))
			{
				d20.node.Name = "D20";
				d20.node.LocalPosition = new Vector3(0, 0, 2);
				d20.node.LocalRotation = Quaternion.Identity;
				d20.node.LocalScale = Vector3.One;
				//d20.node.SetEnabled(false);

				d20.SetMesh(d20Handle);
				d20.SetMaterial("Mtl_DefaultSurface");
			}

			MeshPrimitiveFactory.CreatePlaneMesh("Plane", Engine, new Vector2(5, 5), 6, false, out _, out _, out ResourceHandle planeHandle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer plane))
			{
				plane.node.Name = "Ground";
				plane.node.LocalPosition = new Vector3(0, -1.5f, 2);
				plane.node.LocalRotation = Quaternion.Identity;
				plane.node.LocalScale = Vector3.One;
				//plane.node.SetEnabled(false);

				plane.SetMesh(planeHandle);
				plane.SetMaterial("Mtl_DefaultSurface");
			}
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out plane))
			{
				plane.node.Name = "Wall";
				plane.node.LocalPosition = new Vector3(0, 1, 4.5f);
				plane.node.SetRotationFromAxisAngle(Vector3.UnitX, -90, false, true);
				plane.node.LocalScale = Vector3.One;
				//plane.node.SetEnabled(false);

				plane.SetMesh(planeHandle);
				plane.SetMaterial("Mtl_DefaultSurface");
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
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer quad))
			{
				quad.node.Name = "Quad";
				quad.node.LocalTransformation = Pose.Identity;
				quad.node.LocalPosition = new(0, 0, 5);
				quad.node.LocalScale = Vector3.One * 5;
				//quad.node.SetEnabled(false);

				quad.SetMesh(quadHandle);
				quad.SetMaterial("Mtl_DefaultSurface");
				quad.SetMaterial("Mtl_TestMaterial");
			}

			return true;
		}

		public override bool UpdateRunningState()
		{
			if (Engine.InputManager.GetKeyUp(Key.Escape) ||
				Engine.InputManager.GetKeyUp(Key.Enter))
			{
				Engine.Exit();
			}

			Scene scene = Engine.SceneManager.MainScene!;

			float deltaTime = (float)Engine.TimeManager.DeltaTime.TotalSeconds;

			if (scene.FindNode("Rabbit", out SceneNode? rabbitNode) && rabbitNode != null)
			{
				float radPerSec = 2 * MathF.PI / 10;
			
				Pose localPose = rabbitNode.LocalTransformation;
				localPose.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, radPerSec * deltaTime));
				rabbitNode.LocalTransformation = localPose;
			}

			if (scene.FindNode("D20", out SceneNode? cubeNode) && cubeNode != null)
			{
				float rotSpeed = deltaTime * 5;
				Pose localPose = cubeNode.LocalTransformation;
				Vector3 inputWASD = Engine.InputManager.GetKeyAxesSmoothed(InputAxis.ArrowKeys);
				Vector3 inputIJKL = Engine.InputManager.GetKeyAxesSmoothed(InputAxis.IJKL);
				localPose.position += new Vector3(inputIJKL.X, inputIJKL.Z, inputIJKL.Y) * deltaTime;
				localPose.Rotate(Quaternion.CreateFromYawPitchRoll(inputWASD.X * rotSpeed, inputWASD.Y * rotSpeed, inputWASD.Z * rotSpeed));
				cubeNode.LocalTransformation = localPose;
			}

			// Camera controls:
			if (Camera.MainCamera != null)
			{
				Pose p = Camera.MainCamera.node.LocalTransformation;
				Vector3 inputWASD = Engine.InputManager.GetKeyAxesSmoothed(InputAxis.WASD);
				Vector3 localMovement = new Vector3(inputWASD.X, inputWASD.Z, inputWASD.Y) * deltaTime;
				if (Engine.InputManager.GetKey(Key.ShiftLeft))
				{
					localMovement *= 3;
				}
				Vector3 cameraMovement = p.TransformDirection(localMovement);
				p.Translate(cameraMovement);

				if (Engine.InputManager.GetMouseButton(MouseButton.Right))
				{
					const float DEG2RAD = MathF.PI / 180.0f;
					const float mouseDegreesPerPixel = 0.1f;
					Vector2 mouseMovement = Engine.InputManager.MouseMovement * mouseDegreesPerPixel;
					cameraYaw += mouseMovement.X;
					cameraPitch = Math.Clamp(cameraPitch + mouseMovement.Y, -89, 89);
					p.rotation = Quaternion.CreateFromYawPitchRoll(cameraYaw * DEG2RAD, cameraPitch * DEG2RAD, 0);
				}

				Camera.MainCamera.node.LocalTransformation = p;
			}

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

				Engine.Logger.LogMessage("Saved main scene to file.");
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
