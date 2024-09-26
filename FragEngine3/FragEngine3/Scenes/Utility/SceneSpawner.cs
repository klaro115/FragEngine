using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Lighting;

namespace FragEngine3.Scenes.Utility;

/// <summary>
/// Helper class for creating nodes with common components more easily.
/// </summary>
public static class SceneSpawner
{
	#region Methods

	// CAMERAS:

	public static bool CreateCamera(in Scene _scene, bool _isMainCamera, out CameraComponent _outCamera)
	{
		if (_scene == null || _scene.IsDisposed)
		{
			_outCamera = null!;
			return false;
		}

		SceneNode node = _scene.rootNode.CreateChild("Camera");
		if (!node.CreateComponent(out _outCamera!))
		{
			_scene.rootNode.DestroyChild(node);
			return false;
		}

		if (_isMainCamera)
		{
			_outCamera.IsMainCamera = true;
		}
		return true;
	}

	// LIGHTS:

	public static bool CreateLight(in Scene _scene, LightType _type, out LightComponent _outLight)
	{
		if (_scene == null || _scene.IsDisposed)
		{
			_outLight = null!;
			return false;
		}

		SceneNode node = _scene.rootNode.CreateChild($"{_type} Light");
		if (!node.CreateComponent(out _outLight!))
		{
			_scene.rootNode.DestroyChild(node);
			return false;
		}

		_outLight.Type = _type;
		return true;
	}
	public static bool CreateLight(in SceneNode _parent, LightType _type, out LightComponent _outLight)
	{
		if (_parent == null || _parent.IsDisposed)
		{
			_outLight = null!;
			return false;
		}

		SceneNode node = _parent.CreateChild($"{_type} Light");
		if (!node.CreateComponent(out _outLight!))
		{
			_parent.DestroyChild(node);
			return false;
		}

		_outLight.Type = _type;
		return true;
	}

	// GEOMETRY:

	public static bool CreateStaticMeshRenderer(in Scene _scene, out StaticMeshRendererComponent _outRenderer)
	{
		if (_scene == null || _scene.IsDisposed)
		{
			_outRenderer = null!;
			return false;
		}

		SceneNode node = _scene.rootNode.CreateChild();
		if (!node.CreateComponent(out _outRenderer!))
		{
			_scene.rootNode.DestroyChild(node);
			return false;
		}
		return true;
	}
	public static bool CreateStaticMeshRenderer(in SceneNode _parent, out StaticMeshRendererComponent _outRenderer)
	{
		if (_parent == null || _parent.IsDisposed)
		{
			_outRenderer = null!;
			return false;
		}

		SceneNode node = _parent.CreateChild();
		if (!node.CreateComponent(out _outRenderer!))
		{
			_parent.DestroyChild(node);
			return false;
		}
		return true;
	}

	#endregion
}
