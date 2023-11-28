
using FragEngine3.EngineCore;

namespace FragEngine3.Scenes.EventSystem
{
	/// <summary>
	/// Various events sent by the different parts that make up a scene.<para/>
	/// NOTE: None of the pre-defined events should be sent by user-side code, unless you know exactly what you're doing and what
	/// side effects might occur. Custom events are supported and possible, and valid IDs must be assembled through the helper method
	/// '<see cref="SceneEventTypeExt.CreateCustomEventType(int, out SceneEventType)"/>'. Numerically, all custom events will have a
    /// numerical value starting from '<see cref="CUSTOM_EVENT"/>' (i.e. value >= 1000); to check if an event type is custom, the
    /// extensonsion method '<see cref="SceneEventTypeExt.IsCustomEventType(SceneEventType)"/>' may be used. Note that no procedural
    /// events will ever be defined by the engine's systems that would overlap with the reserved user-side value range.
	/// </summary>
	public enum SceneEventType : int
    {
		None                = 0,

		// SCENE EVENTS:

		OnSceneAdded,       // New scene was added to the manager. data = new scene (Scene)
        OnSceneRemoved,     // Scene was removed (and previously unloaded) from the manager. data = removed scene (Scene)
        OnSceneSaved,       // Scene has finished saving to file and is ready for use again. data = saved scene (Scene)
		OnSceneLoaded,      // Scene has finished loading and is ready for use. data = loaded scene (Scene)
		OnSceneUnloaded,    // Scene has been fully unloaded and may now be removed. data = unloaded scene (Scene)

		// NODE EVENTS:

		OnNodeDestroyed,    // This node was destroyed. data = null
        OnSetNodeEnabled,   // This node was enabled. data = isEnabled (bool)
        OnParentChanged,    // This node's parent has changed. data = new parent (SceneNode)

        // COMPONENT EVENTS:

        OnCreateComponent,  // New component was created or added to this node. data = new component (Component)
        OnDestroyComponent, // Component was removed from this node. data = disposed component (Component)

        //...

        CUSTOM_EVENT        = 1000,
    }

    /// <summary>
    /// Helper class with extension and utility methods for dealing with the '<see cref="SceneElementType"/>' enum.
    /// </summary>
    public static class SceneEventTypeExt
    {
		#region Methods

        /// <summary>
        /// Check whether the event type is a custom event type, i.e. one that was defined through use-side code.<para/>
        /// NOTE: Custom event types defined by user code will always have a numerical value starting at 1000 (aka '<see cref="SceneEventType.CUSTOM_EVENT"/>').
        /// Additional custom events may be defined through '<see cref="CreateCustomEventType(int, out SceneEventType)"/>'.
        /// </summary>
        /// <param name="_type">The event type we wish to inspect.</param>
        /// <returns>True if the event type is a custom type defined by user-side code, false otherwise.</returns>
        public static bool IsCustomEventType(this SceneEventType _type)
        {
            return _type > SceneEventType.CUSTOM_EVENT;
        }

        /// <summary>
        /// Create a custom event type value. It is recommended that user-defined custom events never overlap or collide; they should be
        /// strictly unique for each use-case.<para/>
        /// WARNING: This will simply check the validity of the ID number and add 1000 to the value, before casting to <see cref="SceneElementType"/>.
        /// No additional checks for uniqueness or registration of the event type value will be performed; this is left at the user code's discretion.
        /// </summary>
        /// <param name="_idNumber">A numeric identifier for the event type that you're creating. This number should ideally be derived
        /// from a hardcoded constant and be unique to a specific use-case. Must be non-negative and greater than zero.</param>
        /// <param name="_outEventType">Outputs an enum value that for the given ID number that will not collide with engine-defined eventy type values.</param>
        /// <returns>True if the ID number was valid, false otherwise.</returns>
        public static bool CreateCustomEventType(int _idNumber, out SceneEventType _outEventType, bool _verbose = true)
        {
            if (_idNumber < 0)
            {
                if (_verbose) Logger.Instance?.LogError("ID number for custom eventy may not be negative! Possible collision with internal event types!");
                _outEventType = SceneEventType.None;
                return false;
            }
            if (_idNumber == 0)
            {
				if (_verbose) Logger.Instance?.LogError("ID number for custom eventy may not be zero! Possible ambiguity with custom event type flag!");
				_outEventType = SceneEventType.None;
				return false;
			}

            _outEventType = SceneEventType.CUSTOM_EVENT + _idNumber;
            return true;
        }

		#endregion
	}
}
