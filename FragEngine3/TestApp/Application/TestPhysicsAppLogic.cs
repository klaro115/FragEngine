using BulletSharp;
using FragBulletPhysics;
using FragBulletPhysics.Extensions;
using FragBulletPhysics.ShapeComponents;
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Input;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Resources.Data;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Utility;
using System.Numerics;
using TestApp.Camera;
using Veldrid;
using Veldrid.Sdl2;

namespace TestApp.Application;

internal sealed class TestPhysicsAppLogic : ApplicationLogic
{
	#region Fields

	private readonly List<ResourceHandle> colorTextures = [];
	private readonly List<ResourceHandle> colorMaterials = [];

	#endregion
	#region Methods

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

		GraphicsSystem graphics = Engine.GraphicsSystem;
		GraphicsCore core = graphics.graphicsCore;
		ResourceManager resourceManager = Engine.ResourceManager;

		// Prepare several single-color texture resources:
		ResourceHandle[] colorTextureHandles =
		[
			graphics.TexPlaceholderWhite,
			graphics.TexPlaceholderGray,
			graphics.TexPlaceholderBlack,
			graphics.TexPlaceholderMagenta,
		];
		colorTextures.AddRange(colorTextureHandles);

		Tuple<string, RgbaByte>[] colorTextureData =
		[
			new("TexRed", RgbaByte.Red),
			new("TexGreen", RgbaByte.Green),
			new("TexBlue", RgbaByte.Blue),
			new("TexYellow", RgbaByte.Yellow),
			new("TexCyan", RgbaByte.Cyan),
		];
		foreach (var data in colorTextureData)
		{
			ResourceHandle texHandle = CreateColorTexture(data.Item1, data.Item2);
			if (texHandle.IsValid && texHandle.IsLoaded)
			{
				colorTextures.Add(texHandle);
			}
		}

		// Create flat-shaded materials from single-color texture:
		foreach (ResourceHandle texHandle in colorTextures)
		{
			string mtlResourceKey = texHandle.resourceKey.Replace("Tex", "Mtl");
			ResourceHandleData mtlHandleData = new()
			{
				ResourceKey = mtlResourceKey,
				ResourceType = ResourceType.Material,
			};
			ResourceHandle mtlHandle = new(resourceManager, mtlHandleData, mtlResourceKey);
			MaterialData mtlData = new()
			{
				Key = mtlResourceKey,
				Shaders = new()
				{
					Vertex = "DefaultSurface_VS",
					Pixel = "Basic_PS",
				},
				Replacements = new()
				{
					ShadowMap = "Mtl_DefaultShadow",
				},
				Resources =
				[
					new()
					{
						ResourceKey = texHandle.resourceKey,
						SlotName = "TexMain",
						SlotIndex = 4,
						ResourceKind = ResourceKind.TextureReadOnly,
						ShaderStageFlags = ShaderStages.Fragment,
					},
				],
			};
			if (Material.CreateMaterial(mtlHandle, mtlData, core, out Material? material))
			{
				mtlHandle = new(material!);
				if (resourceManager.AddResource(mtlHandle))
				{
					colorMaterials.Add(mtlHandle);
				}
				else
				{
					material!.Dispose();
				}
			}
		}

		return true;


		// Local helper method for creating an 8x8 pixel single-color texture resource:
		ResourceHandle CreateColorTexture(string _resourceKey, RgbaByte _color)
		{
			core.CreateBlankTexture(_color, out Texture texture);
			_ = new TextureResource(_resourceKey, Engine, texture, out ResourceHandle handle);
			return handle;
		}
	}

	// UNLOADING:

	protected override bool BeginUnloadingState()
	{
		return true;
	}

	// RUNNING:

	protected override bool BeginRunningState()
	{
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

			if (camera.node.CreateComponent(out CameraFlightComponent? cameraFlight))
			{
				cameraFlight!.RotationSpeed = 0.2f;
			}
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

			node.CreatePhysicsBodyComponent(out BoxPhysicsComponent? _, new(20, 1, 20, 0), _isStatic: true);
		}

		MeshPrimitiveFactory.CreateCylinderMesh("Cylinder", Engine, 1, 10, 32, true, out _, out _, out ResourceHandle cylinderHandle);
		if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRendererComponent cylinder))
		{
			SceneNode node = cylinder.node;
			node.Name = "Cylinder";
			node.LocalPosition = new(5, 1, 5);
			node.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.5f);

			cylinder.SetMesh(cylinderHandle);
			cylinder.SetMaterial("Mtl_BrickWall");

			if (node.CreatePhysicsBodyComponent(out CylinderPhysicsComponent? body, new(1, 10, 0, 0), _isStatic: true))
			{
				body!.Rigidbody.CollisionFlags |= CollisionFlags.KinematicObject;
			}
		}

		MeshPrimitiveFactory.CreateIcosahedronMesh("Sphere", Engine, 0.5f, true, out _, out _, out ResourceHandle sphereMeshHandle);
		Engine.ResourceManager.GetResource("Mtl_DefaultSurface", out ResourceHandle sphereMaterialHandle);
		SpawnSphere(in scene.rootNode, "Sphere", new(0, 1.5f, 0), sphereMaterialHandle, sphereMeshHandle);
		scene.rootNode.CreateChild("SphereParent");

		return true;
	}

	private static void SpawnSphere(in SceneNode _parent, string _name, Vector3 _worldPosition, ResourceHandle _materialHandle, ResourceHandle _meshHandle)
	{
		if (SceneSpawner.CreateStaticMeshRenderer(in _parent, out StaticMeshRendererComponent sphere))
		{
			SceneNode node = sphere.node;
			node.Name = _name ?? "Sphere";
			node.LocalPosition = _worldPosition;

			sphere.SetMesh(_meshHandle);
			sphere.SetMaterial(_materialHandle);

			node.CreatePhysicsBodyComponent(out SpherePhysicsComponent? _, new(0.5f, 0, 0, 0), 1, false);
		}
	}

	public override bool UpdateRunningState()
	{
		InputManager input = Engine.InputManager;
		Scene scene = Engine.SceneManager.MainScene!;
		ResourceManager resourceManager = Engine.ResourceManager;

		if (input.GetKeyUp(Key.Escape) ||
			input.GetKeyUp(Key.Enter))
		{
			Engine.Exit();
		}

		float deltaTime = (float)Engine.TimeManager.DeltaTime.TotalSeconds;

		if (scene.FindNode("Cylinder", out SceneNode? cylinderNode) && cylinderNode!.GetComponent(out CylinderPhysicsComponent? cylinderBody))
		{
			Quaternion localRot = cylinderNode!.LocalRotation;
			localRot *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ, deltaTime * 0.7f);
			cylinderNode.LocalRotation = localRot;
			cylinderBody!.Rigidbody.WorldTransform = cylinderNode.WorldTransformation.ConvertHandedness();
		}

		if (scene.FindNode("Sphere", out SceneNode? node) && node is not null)
		{
			Vector3 inputAxes = input.GetKeyAxes(InputAxis.IJKL);

			PhysicsBodyComponent body = node.GetComponent<PhysicsBodyComponent>()!;
			body.Rigidbody.ApplyCentralForce(inputAxes * 50);
		}

		// Spawn a whole bunch of spheres when pressing 'Space':
		if (input.GetKeyUp(Key.Space))
		{
			scene.FindNode("SphereParent", out SceneNode? sphereParent);
			resourceManager.GetResource("Sphere", out ResourceHandle sphereMeshHandle);

			for (int x = 0; x < 5;  x++)
			{
				float posX = 0.3f * x - 0.75f;

				for (int z = 0; z < 5; z++)
				{
					float posZ = 0.3f * z - 0.75f;
					float posY = 4 + x + z;
					int sphereIdx = x * 5 + z;

					ResourceHandle sphereMaterialHandle = colorMaterials[sphereIdx % colorMaterials.Count];

					SpawnSphere(in sphereParent!, $"Sphere_{x}_{z}", new(posX, posY, posZ), sphereMaterialHandle, sphereMeshHandle);
				}
			}
		}

		return true;
	}

	public override bool DrawRunningState()
	{
		return true;
	}

	protected override bool EndRunningState()
	{
		return true;
	}

	#endregion
}
