using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Scenes.Utility
{
	/// <summary>
	/// Utility class for saving and loading entire scenes.
	/// </summary>
	public static class SceneSerializer
	{
		#region Methods Saving

		public static bool SaveSceneToFile(
			in Scene _scene,
			string _filePath,
			out Progress _outProgress,
			bool _refreshBeforeSaving = true)
		{
			if (!SaveSceneToData(_scene, out SceneData data, out _outProgress, _refreshBeforeSaving)) return false;

			return Serializer.SerializeJsonToFile(data, _filePath);
		}

		public static bool SaveSceneToData(
			in Scene _scene,
			out SceneData _outData,
			out Progress  _outProgress,
			bool _refreshBeforeSaving = true)
		{
			_outProgress = new("Preparing to save scene", 1);

			if (_scene == null || _scene.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot serialize null or disposed scene!");
				goto abort;
			}

			// If requested, refresh and clean up scene graph:
			if (_refreshBeforeSaving)
			{
				_scene.Refresh(false);
				_outProgress.Increment();
			}

			// Map out all of the scene's elements, and assign an ID to each one:
			if (!GenerateSceneIdMap(
				in _scene,
				_outProgress,
				out List<SceneBehaviour> sceneBehaviours,
				out List<SceneNode> allNodes,
				out Dictionary<ISceneElement, int> idMap,
				out int maxComponentCount,
				out int totalComponentCount,
				out int totalProgressTaskCount))
			{
				Logger.Instance?.LogError("Failed to generate ID mapping for scene!");
				goto abort;
			}

			// Assemble preliminary scene data and allocate buffers:
			_outData = new()
			{
				Name = _scene.Name,
				UpdatedInEngineStates = _scene.UpdatedInEngineStates,
				Behaviours = new()
				{
					BehaviourCount = sceneBehaviours.Count,
					BehavioursData = sceneBehaviours.Count != 0 ? new SceneBehaviourData[sceneBehaviours.Count] : null,
				},
				Hierarchy = new()
				{
					TotalNodeCount = allNodes.Count,
					HierarchyDepth = 0,		//TODO
					TotalComponentCount = totalComponentCount,
					MaxComponentCount = maxComponentCount,
					NodeData = allNodes.Count != 0 ? new SceneNodeData[allNodes.Count] : null,
				},
			};
			// Set all array elements to null, they'll be written to later:
			if (_outData.Behaviours.BehavioursData != null)
			{
				Array.Fill(_outData.Behaviours.BehavioursData, null);
			}
			if (_outData.Hierarchy.NodeData != null)
			{
				Array.Fill(_outData.Hierarchy.NodeData, null);
			}

			_outProgress.Update(null, 0, totalProgressTaskCount);

			// Save all scene-wide behaviours data first:
			if (!SaveSceneBehaviours(in _scene, in idMap, _outData, _outProgress))
			{
				Logger.Instance?.LogError("Failed to save scene behaviours for scene!");
				goto abort;
			}

			// Save all node data:
			SceneBranchData branchData = new()
			{
				PrefabName = string.Empty,
				ID = -1,
				Hierarchy = _outData.Hierarchy,
			};
			if (!SceneBranchSerializer.SaveSceneNodes(in allNodes, in idMap, branchData, _outProgress))
			{
				Logger.Instance?.LogError("Failed to save node hierarchy for scene!");
				goto abort;
			}

			// Broadcast an event across the scene instance that it has just finished loading:
			_outProgress.UpdateTitle("Initializing states");
			_scene.BroadcastEvent(SceneEventType.OnSceneSaved, _scene, false);

			_outProgress.CompleteAllTasks();
			_outProgress.Finish();
			return true;

		abort:
			{
				_outProgress.errorCount++;
				_outProgress.Update("Saving process failed", 0, _outProgress.taskCount);
				_outProgress.Finish();
			}
			_outData = new();
			return false;
		}

		private static bool GenerateSceneIdMap(
			in Scene _scene,
			Progress _progress,
			out List<SceneBehaviour> _outSceneBehaviours,
			out List<SceneNode> _outAllNodes,
			out Dictionary<ISceneElement, int> _outIdMap,
			out int _outMaxComponentCount,
			out int _outTotalComponentCount,
			out int _outTotalProgressTaskCount)
		{
			_progress.Update("Generating scene ID map", 0, 3);

			_outSceneBehaviours = new(_scene.SceneBehaviourCount);
			_outAllNodes = new(1024);
			_outIdMap = new Dictionary<ISceneElement, int>(1024);
			_outMaxComponentCount = 0;
			_outTotalComponentCount = 0;
			_outTotalProgressTaskCount = 0;

			int idCounter = 0;

			for (int i = 0; i < _scene.SceneBehaviourCount; i++)
			{
				if (_scene.GetSceneBehaviour(i, out SceneBehaviour? component) && component != null)
				{
					_outIdMap.Add(component, idCounter++);
					_outSceneBehaviours.Add(component);
				}
			}
			_outTotalProgressTaskCount += _outSceneBehaviours.Count * 2;
			_progress.Increment();

			Queue<SceneNode> nodeQueue = new(1024);
			nodeQueue.Enqueue(_scene.rootNode);

			while (nodeQueue.TryDequeue(out SceneNode? node))
			{
				_outIdMap.Add(node, idCounter++);
				_outAllNodes.Add(node);
				for (int i = 0; i < node.ChildCount; i++)
				{
					if (node.GetChild(i, out SceneNode? child) && child != null)
					{
						nodeQueue.Enqueue(child);
					}
				}
			}
			_outTotalProgressTaskCount += _outAllNodes.Count;
			_progress.Increment();

			foreach (SceneNode node in _outAllNodes)
			{
				int liveComponentCount = 0;
				for (int i = 0; i < node.ComponentCount; i++)
				{
					if (node.GetComponent(i, out Component? component) && component != null)
					{
						_outIdMap.Add(component, idCounter++);
						liveComponentCount++;
					}
				}
				_outMaxComponentCount = Math.Max(_outMaxComponentCount, liveComponentCount);
				_outTotalComponentCount += liveComponentCount;
			}
			_outTotalProgressTaskCount += _outTotalComponentCount * 2;
			_progress.CompleteAllTasks();

			return true;
		}

		private static bool SaveSceneBehaviours(
			in Scene _scene,
			in Dictionary<ISceneElement, int> _idMap,
			SceneData _sceneData,
			Progress _progress)
		{
			_progress.UpdateTitle("Writing scene behaviours");

			int index = 0;

			for (int i = 0; i < _scene.SceneBehaviourCount; i++)
			{
				if (_scene.GetSceneBehaviour(i, out SceneBehaviour? behaviour) &&
					behaviour != null &&
					_idMap.TryGetValue(behaviour, out int id))
				{
					if (!behaviour.SaveToData(out SceneBehaviourData data))
					{
						Logger.Instance?.LogError($"Failed to save scene component '{behaviour}' to data!");
						return false;
					}

					data.Type = behaviour.GetType().ToString();
					data.ID = id;

					_sceneData.Behaviours.BehavioursData![index++] = data;

					_progress.Increment();
				}
			}
			if (_sceneData.Behaviours.BehavioursData != null)
			{
				for (int i = index; index < _sceneData.Behaviours.BehavioursData!.Length; ++i)
				{
					_sceneData.Behaviours.BehavioursData![i] = new();
				}
			}

			return true;
		}

		#endregion
		#region Methods Loading

		public static bool LoadSceneFromFile(Engine _engine, string _filePath, out Scene? _outScene, out Progress _outProgress)
		{
			if (!Serializer.DeserializeJsonFromFile(_filePath, out SceneData? data) || data == null)
			{
				_outScene = null;
				_outProgress = new("Deserialization failed", 0);
				_outProgress.Finish();
				return false;
			}

			return LoadSceneFromData(_engine, data, out _outScene, out _outProgress);
		}

		public static bool LoadSceneFromData(Engine _engine, SceneData _sceneData, out Scene? _outScene, out Progress _outProgress)
		{
			_outProgress = new("Preparing to load scene", 1);

			if (_engine == null || _engine.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot load scene for null or disposed engine!");
				_outScene = null;
				return false;
			}
			if (_sceneData == null)
			{
				Logger.Instance?.LogError("Cannot load scene from null scene data!");
				_outScene = null;
				return false;
			}

			_outScene = new Scene(_engine, _sceneData.Name)
			{
				UpdatedInEngineStates = _sceneData.UpdatedInEngineStates,
			};


			// First, try to recreate the ID mapping of all elements in the scene:
			if (!ReconstructSceneIdMap(
				in _sceneData,
				_outProgress,
				out _,
				out int totalComponentCount,
				out int totalProgressTaskCount))
			{
				Logger.Instance?.LogError("Failed to reconstruct ID map from scene data!");
				goto abort;
			}

			_outProgress.Update(null, 0, totalProgressTaskCount);

			// RECREATE:

			Dictionary<int, ISceneElement> idElementMap = [];

			// Recreate and reattach scene-wide behaviours to the scene: (loading happens after all elements have been spawned.)
			if (!CreateSceneBehaviours(in _sceneData, idElementMap, _outScene, _outProgress))
			{
				Logger.Instance?.LogError("Failed to recreate scene-wide behaviours!");
				goto abort;
			}

			// Create all nodes: (those can be create and loaded in one go)
			SceneBranchData branchData = new()
			{
				PrefabName = _sceneData.Name,
				ID = -1,
				Hierarchy = _sceneData.Hierarchy,
			};
			if (!SceneBranchSerializer.CreateBranchNodes(
				in branchData,
				_outScene.rootNode,
				idElementMap,
				out Dictionary<int, SceneNode> nodeIdMap,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to recreate and load scene hierarchy!");
				goto abort;
			}

			// RELOAD:

			// Create all components and reattach them to nodes:
			if (!SceneBranchSerializer.CreateComponents(
				in branchData,
				in nodeIdMap,
				idElementMap,
				totalComponentCount,
				out List<Component> allComponents,
				out List<ComponentData> allComponentData, _outProgress))
			{
				Logger.Instance?.LogError("Failed to recreate components!");
				goto abort;
			}

			// Load all scene-wide behaviour states:
			if (!LoadSceneBehaviours(
				in _sceneData,
				in idElementMap,
				in _outScene,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to load scene-wide behaviours!");
				goto abort;
			}

			// Load all component states:
			if (!SceneBranchSerializer.LoadComponents(
				in allComponents,
				in allComponentData,
				in idElementMap,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to load component states!");
				goto abort;
			}

			// Broadcast an event across the scene instance that it has just finished loading:
			_outScene.BroadcastEvent(SceneEventType.OnSceneLoaded, _outScene, false);
			_outScene.rootNode.SendEvent(SceneEventType.OnSetNodeEnabled, _outScene.rootNode.IsEnabled);

			_outProgress.CompleteAllTasks();
			_outProgress.Finish();
			return true;

		abort:
			{
				_outProgress.errorCount++;
				_outProgress.Update("Loading process failed", 0, _outProgress.taskCount);
				_outProgress.Finish();
			}
			_outScene?.Dispose();
			_outScene = null;
			return false;
		}

		private static bool ReconstructSceneIdMap(
			in SceneData _data,
			Progress _progress,
			out Dictionary<int, ISceneElementData> _outIdMap,
			out int _outTotalComponentCount,
			out int _outTotalProgressTaskCount)
		{
			int expectedElementCount = _data.GetTotalSceneElementCount();

			_progress.Update("Reconstructing scene ID map", 0, expectedElementCount);

			_outIdMap = new Dictionary<int, ISceneElementData>(expectedElementCount);
			_outTotalComponentCount = 0;
			_outTotalProgressTaskCount = 0;

			if (_data.Behaviours?.BehavioursData != null)
			{
				int behaviourCount = Math.Min(_data.Behaviours.BehavioursData.Length, _data.Behaviours.BehaviourCount);
				int actualBehaviourCount = 0;

				for (int i = 0; i < behaviourCount; i++)
				{
					SceneBehaviourData bData = _data.Behaviours.BehavioursData[i];
					if (bData != null && bData.ID >= 0 && !string.IsNullOrEmpty(bData.Type))
					{
						_outIdMap.Add(bData.ID, bData);

						actualBehaviourCount++;
						_progress.Increment();
					}
				}
				_outTotalProgressTaskCount += actualBehaviourCount * 2;
			}

			if (_data.Hierarchy?.NodeData != null)
			{
				int nodeCount = Math.Min(_data.Hierarchy.NodeData.Length, _data.Hierarchy.TotalNodeCount);
				int actualNodeCount = 0;

				for (int i = 0; i < nodeCount; ++i)
				{
					SceneNodeData nData = _data.Hierarchy.NodeData[i];
					if (nData != null && nData.ID >= 0)
					{
						_outIdMap.Add(nData.ID, nData);

						int componentCount = nData.ComponentData != null ? nData.ComponentData.Length : 0;
						componentCount = Math.Min(componentCount, nData.ComponentCount);

						actualNodeCount++;
						_progress.Increment();

						for (int j = 0; j < componentCount;  ++j)
						{
							ComponentData cData = nData.ComponentData![j];
							if (cData != null && cData.ID >= 0 && !string.IsNullOrEmpty(cData.Type))
							{
								_outIdMap.Add(cData.ID, cData);
								_outTotalComponentCount++;

								_progress.Increment();
							}
						}
					}
				}
				_outTotalProgressTaskCount += actualNodeCount;
				_outTotalProgressTaskCount += _outTotalComponentCount * 2;
			}

			_progress.CompleteAllTasks();
			return true;
		}

		private static bool CreateSceneBehaviours(in SceneData _data, Dictionary<int, ISceneElement> _idMap, Scene _scene, Progress _progress)
		{
			if (_data.Behaviours?.BehavioursData == null) return true;

			_progress.UpdateTitle("Creating scene behaviours");

			int behaviourCount = Math.Min(_data.Behaviours.BehavioursData.Length, _data.Behaviours.BehaviourCount);

			for (int i = 0; i < behaviourCount; i++)
			{
				SceneBehaviourData data = _data.Behaviours.BehavioursData[i];
				if (data != null && data.ID >= 0 && !string.IsNullOrEmpty(data.Type))
				{
					if (!SceneBehaviourFactory.CreateBehaviour(_scene, data.Type, out SceneBehaviour? behaviour) || behaviour == null)
					{
						Logger.Instance?.LogError($"Failed to create scene-wide behaviour of type '{data.Type}' for scene '{_scene.Name}'!");
						behaviour?.Dispose();
						return false;
					}
					_scene.AddSceneBehaviour(behaviour);
					_idMap.Add(data.ID, behaviour);

					_progress.Increment();
				}
			}

			return true;
		}

		private static bool LoadSceneBehaviours(in SceneData _data, in Dictionary<int, ISceneElement> _idMap, in Scene _scene, Progress _progress)
		{
			if (_data.Behaviours?.BehavioursData == null) return true;

			_progress.UpdateTitle("Loading scene behaviours");

			int behaviourCount = Math.Min(_data.Behaviours.BehavioursData.Length, _data.Behaviours.BehaviourCount);

			for (int i = 0; i < behaviourCount; i++)
			{
				SceneBehaviourData data = _data.Behaviours.BehavioursData[i];
				if (data != null && data.ID >= 0 && _idMap.TryGetValue(data.ID, out ISceneElement? behaviourElement) && behaviourElement is SceneBehaviour behaviour)
				{
					if (!behaviour.LoadFromData(in data))
					{
						Logger.Instance?.LogError($"Failed to load scene-wide behaviour of type '{data.Type}' for scene '{_scene.Name}'!");
						return false;
					}

					_progress.Increment();
				}
			}

			//...
			return true;
		}

		#endregion
	}
}
