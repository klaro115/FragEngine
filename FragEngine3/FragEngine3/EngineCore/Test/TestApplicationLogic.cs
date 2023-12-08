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
			if (Engine.ResourceManager.GetResource("DefaultSurface", out handle))
			{
				handle.Load(true);
			}

			Scene scene = Engine.SceneManager.MainScene!;

			// Create a camera:
			SceneNode cameraNode = scene.rootNode.CreateChild("Camera");
			cameraNode.LocalPosition = new(0, 0, -0.5f);
			cameraNode.LocalRotation = Quaternion.Identity;
			cameraNode.LocalScale = Vector3.One;
			if (cameraNode.CreateComponent(out Camera? camera) && camera != null)
			{
				Sdl2Window window = Engine.GraphicsSystem.graphicsCore.Window;
				camera.ResolutionX = (uint)window.Width;
				camera.ResolutionY = (uint)window.Height;
			
				camera.FieldOfViewDegrees = 60.0f;
				camera.NearClipPlane = 0.1f;
				camera.FarClipPlane = 1000.0f;
			
				camera.clearBackground = true;
				camera.clearColor = Color32.Cornflower;
				camera.clearDepth = 1.0f;
			
				camera.IsMainCamera = true;

				//TEST
				camera.UpdateProjection();
				Vector3 screenPos0 = camera.TransformWorldPointToPixelCoord(new Vector3(0, 1, 1));
				Vector3 screenPos1 = camera.TransformWorldPointToPixelCoord(new Vector3(1, 0, 1));
				Vector3 screenPos2 = camera.TransformWorldPointToPixelCoord(new Vector3(-0.1f, 0, 1));
				Vector3 screenPos3 = camera.TransformWorldPointToPixelCoord(new Vector3(0, 0, 0.01f));
				Vector3 screenPos4 = camera.TransformWorldPointToPixelCoord(new Vector3(0, 0, 10));
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
			rabbitNode.LocalPosition = Vector3.Zero;
			rabbitNode.LocalRotation = Quaternion.Identity;
			rabbitNode.LocalScale = Vector3.One;
			//rabbitNode.SetEnabled(false);
			if (rabbitNode.CreateComponent(out StaticMeshRenderer? rabbitRenderer) && rabbitRenderer != null)
			{
				rabbitRenderer.SetMesh("Rabbit.obj");
				//rabbitRenderer.SetMaterial("DefaultSurface");
				rabbitRenderer.SetMaterial("TestMaterial");
			}

			SceneNode cubeNode = scene.rootNode.CreateChild("Cube");
			cubeNode.LocalPosition = Vector3.Zero;
			cubeNode.LocalRotation = Quaternion.Identity;
			cubeNode.LocalScale = Vector3.One * 0.5f;
			//cubeNode.SetEnabled(false);
			if (cubeNode.CreateComponent(out StaticMeshRenderer? cubeRenderer) && cubeRenderer != null)
			{
				cubeRenderer.SetMesh("Cube.obj");
				//cubeRenderer.SetMaterial("DefaultSurface");
				cubeRenderer.SetMaterial("TestMaterial");
			}

			// Create fullscreen quad:
			MeshSurfaceData quadData = new()
			{
				verticesBasic =
				[
					new BasicVertex(new(-1, -1, 0), new(0, 0, -1), new(0, 0)),
					new BasicVertex(new(1, -1, 0.5f), new(0, 0, -1), new(1, 0)),
					new BasicVertex(new(-1, 1, 0.5f), new(0, 0, -1), new(0, 1)),
					new BasicVertex(new(1, 1, 1), new(0, 0, -1), new(1, 1)),
				],
				indices16 =
				[
					0, 2, 1,
					2, 3, 1,
				],
			};
			StaticMesh quadMesh = new("Quad", Engine.ResourceManager, Engine.GraphicsSystem.graphicsCore, false, out ResourceHandle quadHandle);
			quadMesh.SetGeometry(in quadData);
			SceneNode quadNode = scene.rootNode.CreateChild("Quad");
			quadNode.LocalTransformation = Pose.Identity;
			//quadNode.SetEnabled(false);
			if (quadNode.CreateComponent(out StaticMeshRenderer? quadRenderer) && quadRenderer != null)
			{
				quadRenderer.SetMesh(quadHandle);
				//quadRenderer.SetMaterial("DefaultSurface");
				quadRenderer.SetMaterial("TestMaterial");
			}

			/*
			// Create a static mesh renderer displaying a default-shaded cube:
			SceneNode cubeNode = scene.rootNode.CreateChild("Cube");
			cubeNode.LocalPosition = new(0.4f, -0.5f, 0.9f);
			cubeNode.LocalScale = Vector3.One;
			if (cubeNode.CreateComponent(out StaticMeshRenderer? cubeRenderer) && cubeRenderer != null)
			{
				cubeRenderer.SetMesh("Cube.obj");
				cubeRenderer.SetMaterial("DefaultSurface");
			}

			// Create a simple quad:
			MeshSurfaceData quadData = new()
			{
				verticesBasic =
				[
					new BasicVertex() { position = new(-1, -1, -1), normal = Vector3.UnitZ, uv = new(0, 0) },
					new BasicVertex() { position = new( 1, -1, -1), normal = Vector3.UnitZ, uv = new(1, 0) },
					new BasicVertex() { position = new(-1,  1,  1), normal = Vector3.UnitZ, uv = new(0, 1) },
					new BasicVertex() { position = new( 1,  1,  1), normal = Vector3.UnitZ, uv = new(1, 1) },

					new BasicVertex() { position = new( 1,  1, -1), normal = -Vector3.UnitZ, uv = new(0, 0) },
					new BasicVertex() { position = new(-1,  1, -1), normal = -Vector3.UnitZ, uv = new(1, 0) },
					new BasicVertex() { position = new( 1, -1,  1), normal = -Vector3.UnitZ, uv = new(0, 1) },
					new BasicVertex() { position = new(-1, -1,  1), normal = -Vector3.UnitZ, uv = new(1, 1) },
				],
				indices16 = [0, 1, 2, 1, 3, 2,		5, 4, 6, 5, 6, 7],
			};
			quadData.TransformVertices(new(new Vector3(0, 0, 0.5f), Quaternion.Identity, Vector3.One));
			Vector3 vert0 = Camera.MainCamera!.TransformWorldPointToPixelCoord(quadData.verticesBasic[0].position, false);
			Vector3 vert1 = Camera.MainCamera!.TransformWorldPointToPixelCoord(quadData.verticesBasic[1].position, false);
			Vector3 vert2 = Camera.MainCamera!.TransformWorldPointToPixelCoord(quadData.verticesBasic[2].position, false);
			Vector3 vert3 = Camera.MainCamera!.TransformWorldPointToPixelCoord(quadData.verticesBasic[3].position, false);

			StaticMesh quadMesh = new("Quad", Engine.ResourceManager, Engine.GraphicsSystem.graphicsCore, false, out ResourceHandle quadHandle);
			Engine.ResourceManager.AddResource(quadHandle);
			quadMesh.SetGeometry(quadData);
			SceneNode quadNode = scene.rootNode.CreateChild("Quad");
			quadNode.LocalPosition = Vector3.Zero;
			quadNode.LocalScale = Vector3.One;
			if (quadNode.CreateComponent(out StaticMeshRenderer? quadRenderer) && quadRenderer != null)
			{
				quadRenderer.SetMesh(quadMesh);
				quadRenderer.SetMaterial("DefaultSurface");
			}
			*/

			return true;
		}

		public override bool UpdateRunningState()
		{
			if (Engine.InputManager.GetMouseButtonUp(Veldrid.MouseButton.Right))
			{
				Engine.Exit();
			}

			Scene scene = Engine.SceneManager.MainScene!;
			if (scene.FindNode("Rabbit", out SceneNode? rabbitNode) && rabbitNode != null)
			{
				float radPerSec = 2 * MathF.PI / 10;
				float deltaTime = (float)Engine.TimeManager.DeltaTime.TotalSeconds;
				float time = (float)Engine.TimeManager.RunTime.TotalSeconds;

				Pose localPose = rabbitNode.LocalTransformation;
				localPose.position = new(0, 0, MathF.Sin(time));
				localPose.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, radPerSec * deltaTime));
				rabbitNode.LocalTransformation = localPose;
			}

			return true;
		}

		public override bool DrawRunningState()
		{
			return true;
		}

		protected override bool EndRunningState()
		{
			string saveDirPath = Path.Combine(Engine.ResourceManager.fileGatherer.applicationPath, "saves");
			if (!Directory.Exists(saveDirPath))
			{
				Directory.CreateDirectory(saveDirPath);
			}
			string saveFilePath = Path.Combine(saveDirPath, "test.json");
			SceneSerializer.SaveSceneToFile(Engine.SceneManager.MainScene!, saveFilePath, out _, false);

			return true;
		}

		#endregion
	}
}
