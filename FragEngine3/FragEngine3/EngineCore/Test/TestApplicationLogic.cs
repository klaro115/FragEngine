
using FragEngine3.Graphics.Stack;
using FragEngine3.Resources;
using FragEngine3.Scenes;

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
			GraphicsStack stack = new(Engine.GraphicsSystem.graphicsCore)
			{
				OutputToMainSwapchain = true,
			};

			//TODO: Add layers to graphics stack.

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
			if (Engine.ResourceManager.GetResource("Rabbit.obj", out ResourceHandle handle))
			{
				handle.Load(true);
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
