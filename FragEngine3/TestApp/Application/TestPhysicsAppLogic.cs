using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Scenes.Utility;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid.Sdl2;
using Veldrid;
using FragEngine3.Graphics;
using FragBulletPhysics;
using TestApp.Camera;
using FragEngine3.EngineCore.Input;
using BulletSharp;
using FragBulletPhysics.ShapeComponents;

namespace TestApp.Application;

internal sealed class TestPhysicsAppLogic : ApplicationLogic
{
	private CollisionConfiguration? physCollisionConfig;
	private Dispatcher? physDispatcher;
	private BroadphaseInterface? physBroadphase;
	private DiscreteDynamicsWorld? physWorld;

	private BoxShape? physGroundShape;
	private RigidBody? physGroundRigidbody;

	private SphereShape? physBallShape;
	private RigidBody? physBallRigidbody;

	// STARTUP:

	protected override bool RunStartupLogic()
	{
		Engine.TimeManager.TargetFrameRate = 60;

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
		Scene scene = new(Engine, "PhysicsTest")
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
		if (Engine.SceneManager.MainScene is not null)
		{
			ForwardPlusLightsStack stack = new(Engine.GraphicsSystem.graphicsCore);

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
		//TEST TEST TEST TEST
		physCollisionConfig = new DefaultCollisionConfiguration();
		physDispatcher = new CollisionDispatcher(physCollisionConfig);
		physBroadphase = new DbvtBroadphase();
		physWorld = new(physDispatcher, physBroadphase, null, physCollisionConfig);

		physGroundShape = new(20, 1, 20);
		using RigidBodyConstructionInfo physGroundRigidbodyInfo = new(0, new DefaultMotionState(), physGroundShape);
		physGroundRigidbody = new(physGroundRigidbodyInfo);
		physWorld.AddCollisionObject(physGroundRigidbody);
		//TEST TEST TEST TEST



		Scene scene = Engine.SceneManager.MainScene!;

		// Prepare physics scene:
		if (scene.rootNode.CreateComponent(out PhysicsWorldComponent? physicsWorld) && physicsWorld is not null)
		{
			physicsWorld.Gravity = Vector3.UnitY * -9.81f;
		}

		// Set ambient lighting:
		scene.settings.AmbientLightIntensityLow = new(0.18f, 0.16f, 0.12f, 0);
		scene.settings.AmbientLightIntensityMid = new(0.15f, 0.15f, 0.15f, 0);
		scene.settings.AmbientLightIntensityHigh = new(0.17f, 0.17f, 0.25f, 0);

		// Create a camera:
		if (SceneSpawner.CreateCamera(scene, true, out CameraComponent camera))
		{
			camera.node.LocalPosition = new Vector3(0, 2, -4);
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

			camera.node.CreateComponent<CameraFlightComponent>(out _);
		}

		// Create a directional light:
		if (SceneSpawner.CreateLight(scene, LightType.Directional, out LightComponent light))
		{
			light.node.Name = "Sun";
			light.node.WorldPosition = new Vector3(0, 5, 0);
			light.node.SetRotationFromYawPitchRoll(-22.5f, 45, 0, true, true);

			light.LightIntensity = 0.5f;
			light.CastShadows = false;
			light.ShadowCascades = 0;
			light.ShadowDepthBias = 0.01f;
		}

		MeshPrimitiveFactory.CreateCubeMesh("Cube", Engine, new(0.5f, 0.5f, 0.5f), false, out _, out _, out ResourceHandle cubeHandle);
		if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRendererComponent cube))
		{
			cube.node.Name = "Cube";
			cube.node.LocalPosition = new Vector3(0, 2, 0);
			cube.node.SetRotationFromYawPitchRoll(45, 45, 0, true, true);
			cube.node.LocalScale = Vector3.One;
			//cube.node.SetEnabled(false);

			cube.SetMesh(cubeHandle);
			cube.SetMaterial("Mtl_DefaultSurface");
		}

		MeshPrimitiveFactory.CreateCubeMesh("Ground", Engine, new(20, 1, 20), true, out _, out _, out ResourceHandle groundHandle);
		if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRendererComponent ground))
		{
			SceneNode node = ground.node;
			node.Name = "Ground";
			node.LocalPosition = new(0, -0.5f, 0);
			node.LocalRotation = Quaternion.Identity;
			node.LocalScale = Vector3.One;

			ground.SetMesh(groundHandle);
			ground.SetMaterial("Mtl_DefaultSurface");

			if (node.CreateComponent(out BoxPhysicsComponent? body, physicsWorld!) && body is not null)
			{
				body.Size = new(20, 1, 20);
				body.IsStatic = true;
			}
		}

		MeshPrimitiveFactory.CreateIcosahedronMesh("Sphere", Engine, 0.5f, true, out _, out _, out ResourceHandle sphereHandle);
		if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRendererComponent sphere))
		{
			SceneNode node = sphere.node;
			node.Name = "Sphere";
			node.LocalPosition = new(0, 8, 0);
			node.LocalRotation = Quaternion.Identity;
			node.LocalScale = Vector3.One;

			sphere.SetMesh(sphereHandle);
			sphere.SetMaterial("Mtl_DefaultSurface");

			if (node.CreateComponent(out SpherePhysicsComponent? body, physicsWorld!) && body is not null)
			{
				body.Radius = 0.5f;
				body.IsStatic = false;
				body.Mass = 1.0f;
			}
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

		//float deltaTime = (float)Engine.TimeManager.DeltaTime.TotalSeconds;

		if (scene.FindNode("Sphere", out SceneNode? node) && node is not null)
		{
			Vector3 input = Engine.InputManager.GetKeyAxes(InputAxis.IJKL);

			PhysicsBodyComponent body = node.GetComponent<PhysicsBodyComponent>()!;
			body.Rigidbody.ApplyCentralForce(input * 5);
		}

		//TEST TEST TEST TEST
		physWorld!.StepSimulation(0.0167f);
		
		if (physBallShape is null)
		{
			physBallShape = new(1);
			using RigidBodyConstructionInfo physBallRigidbodyInfo = new(1, new DefaultMotionState(), physGroundShape);
			physBallRigidbody = new(physBallRigidbodyInfo);
			physBallRigidbody.Translate(Vector3.UnitY * 100);
			physWorld.AddCollisionObject(physBallRigidbody);
		}
		else
		{
			Vector3 ballPos = physBallRigidbody!.WorldTransform.Translation;
			//Console.WriteLine($"Sphere: Mass={1}, Position={ballPos}, Velocity={physBallRigidbody.LinearVelocity}, Gravity={physBallRigidbody.Gravity}");
		}
		//TEST TEST TEST TEST

		return true;
	}

	public override bool DrawRunningState()
	{
		return true;
	}

	protected override bool EndRunningState()
	{
		//TEST TEST TEST TEST
		physBallRigidbody?.Dispose();
		physBallShape?.Dispose();

		physGroundRigidbody?.Dispose();
		physGroundShape?.Dispose();

		physWorld?.Dispose();
		physBroadphase?.Dispose();
		physDispatcher?.Dispose();
		physCollisionConfig?.Dispose();
		//TEST TEST TEST TEST

		return true;
	}
}
