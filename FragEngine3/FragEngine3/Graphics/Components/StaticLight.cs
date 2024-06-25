using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Graphics.Components;

public sealed class StaticLight : Component, ILightSource, IOnComponentAddedListener, IOnComponentRemovedListener
{
	#region Constructors

	public StaticLight(SceneNode _node) : base(_node)
	{
		GetLightComponent();
	}

	#endregion
	#region Fields

	private Light? light = null;

	#endregion
	#region Properties

	public bool IsRedrawIssued { get; private set; } = true;

	/// <summary>
	/// Priority rating to indicate which light sources are more important. Higher priority lights will
	/// be drawn first, lower priority light may be ignored as their impact on a mesh may be negligable.
	/// </summary>
	public int LightPriority
	{
		get => LightComponent is not null ? light!.LightPriority : 0;
		set
		{
			if (LightComponent is not null) light!.LightPriority = value;
		}
	}

	/// <summary>
	/// Gets the currently assigned light component that is used to draw the initial/static shadow map.
	/// </summary>
	public Light? LightComponent => GetLightComponent();

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		light = null;
	}

	private Light? GetLightComponent()
	{
		if (light is null || light.IsDisposed)
		{
			light = null;

			// If no light component is set, find one on host node and assign it:
			Light? newLight = node.GetComponent<Light>();
			if (newLight is not null)
			{
				SetLightComponent(newLight);
			}
		}
		
		return light;
	}

	private void SetLightComponent(Light _newLight)
	{
		if (_newLight is null || _newLight.IsDisposed) return;

		// Unassign and re-register previous component:
		Light? prevLight = light;
		if (prevLight is not null && !prevLight.IsDisposed)
		{
			node.scene.drawManager.RegisterLight(prevLight);
		}

		// Assign and unregister new component, register ourselves in its place:
		light = _newLight;
		node.scene.drawManager.UnregisterLight(light);
		node.scene.drawManager.RegisterLight(this);

		// Raise flag to issue a redraw with our new light component:
		IssueRedraw();
	}

	public void OnComponentAdded(Component _newComponent)
	{
		if (_newComponent is Light newLight && (light is null || light.IsDisposed))
		{
			SetLightComponent(newLight);
		}
	}

	public void OnComponentRemoved(Component removedComponent)
	{
		if (removedComponent is Light removedLight && removedLight == light)
		{
			light = null;
		}
		else if (removedComponent == this)
		{
			// Unregister self, and re-register main light component:
			node.scene.drawManager.UnregisterLight(this);
			if (light is not null && !light.IsDisposed)
			{
				node.scene.drawManager.RegisterLight(light);
			}
		}
	}

	/// <summary>
	/// Triggers a redraw of the associated light component's shadow maps during the next frame.
	/// </summary>
	public void IssueRedraw()
	{
		IsRedrawIssued = true;
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		throw new NotImplementedException();
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		throw new NotImplementedException();
	}

	#endregion
}
