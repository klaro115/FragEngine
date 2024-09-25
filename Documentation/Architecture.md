<h1>Architecture Guide</h1>

The Fragment engine is designed as a universal engine with very lax requirements and rules that developers need to follow when creating their own apps. The software ecosystem is straight-forward, with .NET 8.0 as a universal basis, support for several asset file formats using custom-written importers, and a degree of thread-safety in its core systems.

The following is an overview of how the engine works and where developers may start implementing their own content and code logic.
<br>

- [Engine Lifecycle](#engine-lifecycle)
    - [Loading](#loading)
    - [Running](#running)
    - [Unloading](#unloading)
- [Writing Application Logic](#writing-application-logic)
  - [Node Components](#node-components)
  - [Scene-wide Behaviours](#scene-wide-behaviours)
  - [Application-wide Logic](#application-wide-logic)
- [Main Loop](#main-loop)

<br>


## Engine Lifecycle
The engine has several lifecycle states that it goes through from launch to exit. These states may be accessed via `Engine.State` as an enum of type `FragEngine3.EngineCore.EngineState` (cf. [source](../FragEngine3/EngineCore/EngineEnums.cs)). Possible values are:

| Engine States: | Implementation:        | Main Loop: | Description:                                                                 |
| -------------- | ---------------------- | ---------- | ---------------------------------------------------------------------------- |
| Startup        | `EngineStartupState`   | no         | Engine is starting up, core systems are initialized, resources are gathered. |
| Loading        | `EngineLoadingState`   | yes        | Resources are loaded and catalogued, app shows loading and splash screens.   |
| Running        | `EngineRunningState`   | yes        | Main loop is running, scene logic is executed, menus and gameplay.           |
| Unloading      | `EngineUnloadingState` | yes        | Resources are unloaded, app shows loading and exit screens.                  |
| Shutdown       | `EngineShutdownState`  | no         | Engine is shutting down, core systems are shut down.                         |
<br>

During _Loading_, _Running_, and _Unloading_ states, the engine's main loop is running and scenes can be created and rendered. During the loading and unloading states, the only rendering and logic that should occur should be splash screens and loading screens. Only the running state should feature any interactive content.

#### Loading
Use the _Loading_ state to display splash screens and a loading screen while the user waits for the main menu to be ready.
When entering this state, create a single scene containing only your loading screen UI and splash screens with your company logos. Call `ResourceHandle.Load(true, true)` on all textures and assets you need for this, to ensure the app doesn't start on a blank screen.
You should queue up any resources required be the main menu for asynchronous loading in the background while we wait. See [Resource Guide](./Resources/Resource%20Guide.md) for more detail on how to handle resource loading.
<br>

#### Running
This is where all of your app's interative logic should happen. When entering this state, the user should be faced with a main menu or a start page.
When entering this state, create a single scene containing your main menu or your landing page. Alternatively, launch a tutorial or onboarding process if the app was launched for the first time.

Do not load all game assets immediately; instead, only load essential assets right away, that are needed at every stage of your app or game. When loading a scene or starting a new game, do so asynchronously in a background thread, whilst the users stares at a progress bar. Forcibly loading many assets on the main thread can cause major lag spikes, negatively impacting run-time preformance or causing freezes.
<br>

#### Unloading
In this state, the engine will first dispose of any remaining scenes, and then unload any and all resources held by the app. The only thing on the screen at this stage should be an exit screen. When entering this state, create a single scene containing this exit card so the user knows that the app is exiting safely. Do not try to load any resources or perform meaningful user-facing in this state.

<br>


## Writing Application Logic
The engine offers 3 main interface points where developers may control and interact with the engine's systems. These are:

| Interface Points:      | Implementated via:        | Description:                                                     |
| ---------------------- | ------------------------- | ---------------------------------------------------------------- |
| Node Components        | `Component` + `SceneNode` | Behaviour component attached to nodes within the scene.          |
| Scene-wide Behaviours  | `SceneBehaviour`          | Behaviour component attached to a scene, scene-level logic.      |
| Application-wide Logic | `ApplicationLogic`        | Overarching application-level logic, unique per engine instance. |
<br>

**Note:** In general, a combination of all three types is recommended, as all three logic systems have their strengths and weaknesses. There is nothing stopping you from mixing up control patterns to suit your application's needs.
Use `ApplicationLogic` to drive your app's lifecycle events to persist states across scenes; use `SceneBehaviour` to drive any behaviours within a scene that are not isolated to a single node; use `Component` to drive localized logic that is tied to a single node and its lifespan within the scene.
<br>


### Node Components
_Source code:_ [Component](../FragEngine3/Scenes/Component.cs), [SceneNode](../FragEngine3/Scenes/SceneNode.cs)

The engine can have multiple active scenes, where each scene has its own node hierarchy. Usage of this node system is no requirement when using the engine, though it may provide a familiar entry point for _Unity_ developers. The nodes are implemented via the `SceneNode` class. Each node is roughly equivalent to _Unity_'s `GameObject` type, and can have multiple components attached to them. Each component is responsible for a behaviour or an isolated logic pertaining to its host node.

To create a custom component, simply create a new class that inherits from `FragEngine3.Scenes.Component`.
An instance of the component can be attached to a node by calling `SceneNode.CreateComponent<T>(out T, params object[])`. The method's optional paramaters list allow you to pass any arguments to the new instance's constructor. Alternatively, The static class `ComponentFactory` offers more in-depth control over a component's creation.
Note that all components implement the `IDisposable` interface, which is called when the host node expires, or when the component is removed from its node.
Component instances cannot be transferred to a different node after creation, though you may duplicate an existing component to a new node via `ComponentFactory.DuplicateComponent()` (cf. [source](../FragEngine3/Scenes/ComponentFactory.cs)).

Components and their current state can be saved to file and loaded using the `LoadFromData()` and `SaveToData()` methods. Both methods receive a map of IDs for mapping dependencies within the scene in serialized data. The default save/load logic will save scene elements to file as JSON.

In order to respond to events within the scene, or originating from its hostg node, a component may implement one or more event interfaces (see [source](../FragEngine3/Scenes/EventSystem/SceneEventInterfaces.cs)). These will be automatically registered and unregistered with the scene over the course of the component's lifecycle.

**Pros:**
- Easy to use, clear pre-defined development paradigm.
- Great for simple, localized logic.

**Cons:**
- Not great for multithreaded processes.
- Impractical for scene-spanning or application-wide logic.
- Code execution order is not always obvious
<br>


### Scene-wide Behaviours
_Source code:_ [SceneBehaviour](../FragEngine3/Scenes/SceneBehaviour.cs), [Scene](../FragEngine3/Scenes/Scene.cs)

Since most game or application logic tends to be isolated to one active scene, it makes sense to expose a programming entry point that operates on a scene-wide level. This is where scene-wide behaviours come in; they inherit from the `SceneBehaviour` class and are attached directly to a scene.

To create a custom scene behaviour, simply create a new class that inherits from `FragEngine3.Scenes.SceneBehaviour`.
An instance of the behaviour can be attached to a scene by calling `Scene.CreateSceneBehaviour<T>(out T, params object[])`. The method's optional paramaters list allow you to pass aditional arguments to the new instance's constructor.
Note that all scene behaviours implement the `IDisposable` interface, which is called when the host scene expires, or when the behaviour is removed from its scene.

**Pros:**
- Highly customizable, minimal overhead.
- Great for scene-wide logic:
  - can replace component system entirely
  - allows for heavily multithreaded logic

**Cons:**
- Impractical for application-wide logic.
<br>


### Application-wide Logic
_Source code:_ [ApplicationLogic](../FragEngine3/EngineCore/ApplicationLogic.cs)

Most applications require some overarching code logic that spans across all aspects of the app. In these cases, passing data across scenes and from one component to another is convoluted and error-prone. the same problem applies to a sightly lesser degree to scene-wide behaviour.
The overarching, app-wide `ApplicationLogic` class exists to address these exact use-cases. A singleton of this type is attached directly to the engine at launch-time. This instance then serves as the main hub for the application's run-time state, providing listener methods for the main engine events, such as startup, exit, and the main loop stages.

Only one instance inheriting from `ApplicationLogic` can exist at any moment at run-time. This type represents your application's central control hub, that stears engine states, loads new scenes, and spawns any app-specific modules.
Note that the `Engine.applicationLogic` field is private! This object should not be visible to any other code, instead, it should operate in a strictly top-down manner. The appliction logic instance receives event calls directly from the engine's state machine, which are always executed before any update call are issued to scenes, behaviours, or components.

**Pros:**
- Great for application-wide stateful logic.
- Entry point for spawning and coordinating global systems.

**Cons:**
- Detached/distant from contents of a scene.
<br>


## Main Loop
[WIP]
