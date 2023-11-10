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
				out int totalComponentCount))
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

			_outProgress.Update(null, 0, _outData.GetTotalSceneElementCount());

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
			out int _outTotalComponentCount)
		{
			_progress.Update("Generating scene ID map", 0, 3);

			_outSceneBehaviours = new(_scene.SceneBehaviourCount);
			_outAllNodes = new(1024);
			_outIdMap = new Dictionary<ISceneElement, int>(1024);
			_outMaxComponentCount = 0;
			_outTotalComponentCount = 0;

			int idCounter = 0;

			for (int i = 0; i < _scene.SceneBehaviourCount; i++)
			{
				if (_scene.GetSceneBehaviour(i, out SceneBehaviour? component) && component != null)
				{
					_outIdMap.Add(component, idCounter++);
					_outSceneBehaviours.Add(component);
				}
			}
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
			_progress.Increment();

			foreach (SceneNode node in _outAllNodes)
			{
				for (int i = 0; i < node.ComponentCount; i++)
				{
					int liveComponentCount = 0;
					if (node.GetComponent(i, out Component? component) && component != null)
					{
						_outIdMap.Add(component, idCounter++);
						liveComponentCount++;
					}
					_outMaxComponentCount = Math.Max(_outMaxComponentCount, liveComponentCount);
					_outTotalComponentCount += liveComponentCount;
				}
			}
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
			for (int i = index; index < _sceneData.Behaviours.BehavioursData!.Length; ++i)
			{
				_sceneData.Behaviours.BehavioursData![i] = new();
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

		public static bool LoadSceneFromData(Engine _engine, SceneData _data, out Scene? _outScene, out Progress _outProgress)
		{
			_outProgress = new("Preparing to load scene", 1);

			if (_engine == null || _engine.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot load scene for null or disposed engine!");
				_outScene = null;
				return false;
			}
			if (_data == null)
			{
				Logger.Instance?.LogError("Cannot load scene from null scene data!");
				_outScene = null;
				return false;
			}

			_outScene = new Scene(_engine, _data.Name)
			{
				UpdatedInEngineStates = _data.UpdatedInEngineStates,
			};


			// First, try to recreate the ID mapping of all elements in the scene:
			if (!ReconstructSceneIdMap(in _data, _outProgress, out Dictionary<int, ISceneElementData> idMap, out int totalComponentCount))
			{
				Logger.Instance?.LogError("Failed to reconstruct ID map from scene data!");
				goto abort;
			}

			_outProgress.Update(null, 0, _data.GetTotalSceneElementCount());

			// Recreate and reattach scene-wide behaviours to the scene:
			if (!LoadSceneBehaviours(in _data, in idMap, _outScene, _outProgress))
			{
				Logger.Instance?.LogError("Failed to load and recreate scene-wide behaviours!");
				goto abort;
			}

			// Create all nodes:
			SceneBranchData branchData = new()
			{
				PrefabName = _data.Name,
				ID = -1,
				Hierarchy = _data.Hierarchy,
			};
			if (!SceneBranchSerializer.LoadBranchNodes(in branchData, in idMap, _outScene.rootNode, _outProgress, out Dictionary<int, SceneNode> nodeIdMap))
			{
				Logger.Instance?.LogError("Failed to load and recreate scene hierarchy!");
				goto abort;
			}

			// Create and all components and reattach them to nodes:
			if (!SceneBranchSerializer.LoadComponents(in branchData, in idMap, in nodeIdMap, _outProgress, totalComponentCount))
			{
				Logger.Instance?.LogError("Failed to load and recreate components!");
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

		private static bool ReconstructSceneIdMap(in SceneData _data, Progress _progress, out Dictionary<int, ISceneElementData> _outIdMap, out int _outTotalComponentCount)
		{
			int expectedElementCount = _data.GetTotalSceneElementCount();

			_progress.Update("Reconstructing scene ID map", 0, expectedElementCount);

			_outIdMap = new Dictionary<int, ISceneElementData>(expectedElementCount);
			_outTotalComponentCount = 0;

			if (_data.Behaviours?.BehavioursData != null)
			{
				int behaviourCount = Math.Min(_data.Behaviours.BehavioursData.Length, _data.Behaviours.BehaviourCount);

				for (int i = 0; i < behaviourCount; i++)
				{
					SceneBehaviourData bData = _data.Behaviours.BehavioursData[i];
					if (bData != null && bData.ID >= 0 && !string.IsNullOrEmpty(bData.Type))
					{
						_outIdMap.Add(bData.ID, bData);

						_progress.Increment();
					}
				}
			}

			if (_data.Hierarchy?.NodeData != null)
			{
				int nodeCount = Math.Min(_data.Hierarchy.NodeData.Length, _data.Hierarchy.TotalNodeCount);

				for (int i = 0; i < nodeCount; ++i)
				{
					SceneNodeData nData = _data.Hierarchy.NodeData[i];
					if (nData != null && nData.ID >= 0)
					{
						_outIdMap.Add(nData.ID, nData);

						int componentCount = nData.ComponentData != null ? nData.ComponentData.Length : 0;
						componentCount = Math.Min(componentCount, nData.ComponentCount);

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
			}

			_progress.CompleteAllTasks();
			return true;
		}

		private static bool LoadSceneBehaviours(in SceneData _data, in Dictionary<int, ISceneElementData> _idMap, Scene _scene, Progress _progress)
		{
			_progress.UpdateTitle("Loading scene behaviours");

			if (_data.Behaviours?.BehavioursData == null) return true;

			int behaviourCount = Math.Min(_data.Behaviours.BehavioursData.Length, _data.Behaviours.BehaviourCount);

			for (int i = 0; i < behaviourCount; i++)
			{
				SceneBehaviourData data = _data.Behaviours.BehavioursData[i];
				if (data != null && data.ID >= 0 && !string.IsNullOrEmpty(data.Type))
				{
					if (!SceneBehaviour.CreateBehaviour(_scene, data.Type, out SceneBehaviour? behaviour) || behaviour == null)
					{
						Logger.Instance?.LogError($"Failed to create scene-wide behaviour of type '{data.Type}' for scene '{_scene.Name}'!");
						return false;
					}
					_scene.AddSceneBehaviour(behaviour);

					_progress.Increment();
				}
			}

			return true;
		}

		#endregion
	}
}
