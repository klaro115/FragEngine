﻿using FragEngine3.EngineCore;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Utility;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;

namespace TestApp.Application;

public sealed class TestEmptyAppLogic : ApplicationLogic
{
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
		Scene scene = new(Engine, "Empty")
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
		Scene scene = Engine.SceneManager.MainScene!;

		// Set ambient lighting:
		scene.settings.AmbientLightIntensityLow = new(0.18f, 0.16f, 0.12f, 0);
		scene.settings.AmbientLightIntensityMid = new(0.15f, 0.15f, 0.15f, 0);
		scene.settings.AmbientLightIntensityHigh = new(0.17f, 0.17f, 0.25f, 0);

		// Create a camera:
		if (SceneSpawner.CreateCamera(scene, true, out CameraComponent camera))
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
				ClearColorValue = RgbaFloat.Black,
				ClearDepthValue = 1.0f,
			};
			camera.IsMainCamera = true;
			camera.MarkDirty();
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

		MeshPrimitiveFactory.CreateCubeMesh("Cube", Engine, new(2, 2, 2), false, out _, out _, out ResourceHandle cubeHandle);
		if (SceneSpawner.CreateStaticMeshRenderer(scene, out StaticMeshRendererComponent cube))
		{
			cube.node.Name = "Cube";
			cube.node.LocalPosition = new Vector3(0, -0.5f, 2);
			cube.node.SetRotationFromYawPitchRoll(45, 45, 0, true, true);
			cube.node.LocalScale = Vector3.One;

			cube.SetMesh(cubeHandle);
			cube.SetMaterial("Mtl_DefaultSurface");
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
}
