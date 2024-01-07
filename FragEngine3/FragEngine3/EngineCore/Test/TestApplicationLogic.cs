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
			//TEST TEST TEST
			Vector3 localPos = new(0, 0, 1);
			Matrix4x4 mtxRot = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);  // rotate by 90 deg along Y.
			Matrix4x4 mtxPos = Matrix4x4.CreateTranslation(0, 0, 1);                        // move by 1m along Z.
			Matrix4x4 mtxSca = Matrix4x4.CreateScale(2, 2, 2);                              // double scale.

			Matrix4x4 mtxSRT = mtxSca * mtxRot * mtxPos;
			Matrix4x4 mtxTRS = mtxPos * mtxRot * mtxSca;
			Vector3 worldPosSRT = Vector3.Transform(localPos, mtxSRT);
			Vector3 worldPosTRS = Vector3.Transform(localPos, mtxTRS);
			Vector3 worldPosRight = new(2, 0, 1);
			Vector3 worldPosWrong = new(4, 0, 0);
			//TEST TEST TEST


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
			if (SceneSpawner.CreateCamera(scene, true, out Camera camera))
			{
				camera.node.LocalPosition = new Vector3(0, 0, -5);
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
				light.node.SetRotationFromYawPitchRoll(45, 45, 0, true, true);
			}

			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer rabbit))
			{
				rabbit.node.Name = "Rabbit";
				rabbit.node.LocalPosition = new Vector3(0, -0.25f, 0);
				rabbit.node.LocalRotation = Quaternion.Identity;
				rabbit.node.LocalScale = Vector3.One * 5;
				rabbit.node.SetEnabled(false);

				rabbit.SetMesh("Rabbit.obj");
				rabbit.SetMaterial("Mtl_DefaultSurface");
				//rabbit.SetMaterial("Mtl_TestMaterial");
			}

			MeshPrimitiveFactory.CreateCubeMesh("Cube", Engine, new(1, 1, 1), false, out _, out _, out ResourceHandle cubeHandle);
			if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRenderer cube))
			{
				cube.node.Name = "Cube";
				cube.node.LocalPosition = new Vector3(0, 0, 2);
				cube.node.LocalRotation = Quaternion.Identity;
				//cube.node.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
				cube.node.LocalScale = Vector3.One;
				//cube.node.SetEnabled(false);

				cube.SetMesh(cubeHandle);
				cube.SetMaterial("Mtl_DefaultSurface");
				//cube.SetMaterial("Mtl_TestMaterial");
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

					0, 1, 2,
					2, 1, 3,
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
				//quad.SetMaterial("Mtl_DefaultSurface");
				quad.SetMaterial("Mtl_TestMaterial");
			}

			return true;
		}

		public override bool UpdateRunningState()
		{
			if (Engine.InputManager.GetMouseButtonUp(MouseButton.Right) ||
				Engine.InputManager.GetKeyUp(Key.Escape))
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

			if (scene.FindNode("Cube", out SceneNode? cubeNode) && cubeNode != null)
			{
				float dt = (float)Engine.TimeManager.DeltaTime.TotalSeconds * 5;
				Pose localPose = cubeNode.LocalTransformation;
				Vector3 input = Engine.InputManager.GetKeyAxesSmoothed(InputAxis.WASD);
				localPose.Rotate(Quaternion.CreateFromYawPitchRoll(input.X * dt, input.Y * dt, input.Z * dt));
				cubeNode.LocalTransformation = localPose;
			}

			//TEST
			Pose p = Camera.MainCamera!.node.LocalTransformation;
			//p.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f * (float)Engine.TimeManager.DeltaTime.TotalSeconds));
			//p.position = new(MathF.Sin(0.5f * (float)Engine.TimeManager.DeltaTime.TotalSeconds) * 2, 0, 0);
			Camera.MainCamera!.node.LocalTransformation = p;

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
