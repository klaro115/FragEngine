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
	public static class SceneBranchSerializer
	{
		#region Methods Saving

		public static bool SaveBranchToFile(
			in SceneNode _node,
			string _filePath,
			out Progress _outProgress,
			bool _refreshBeforeSaving = true)
		{
			if (!SaveBranchToData(_node, out SceneBranchData data, out _outProgress, _refreshBeforeSaving)) return false;

			return Serializer.SerializeJsonToFile(data, _filePath);
		}

		/// <summary>
		/// Saves a branch of the hierarchy, starting from the branch's root node.
		/// </summary>
		/// <param name="_node">The node from where to start serializing and saving; this node and all its children will be saved. May not be null.</param>
		/// <param name="_outData">Outputs a branch data object that can then be serialized to file.</param>
		/// <param name="_refreshBeforeSaving">Whether to refresh the part of the scene hierarchy before saving. This will attempt to clean up the scene graph before expired data is saved.</param>
		/// <returns>True if save data could be written, false otherwise.</returns>
		public static bool SaveBranchToData(
			in SceneNode _node,
			out SceneBranchData _outData,
			out Progress _outProgress,
			bool _refreshBeforeSaving = true)
		{
			_outProgress = new("Preparing to save nodes", 1);

			if (_node == null || _node.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot serialize null or disposed scene!");
				goto abort;
			}

			// If requested, refresh and clean up scene graph:
			if (_refreshBeforeSaving)
			{
				IEnumerator<SceneNode> e = _node.IterateChildren(false);
				while (e.MoveNext())
				{
					if (!e.Current.IsDisposed) e.Current.Refresh();
				}
				_outProgress.Increment();
			}

			// Map out all of the branch's elements, and assign an ID to each one:
			if (!GenerateSceneIdMap(
				in _node,
				_outProgress,
				out List<SceneNode> allNodes,
				out Dictionary<ISceneElement, int> idMap,
				out int maxComponentCount,
				out int totalComponentCount))
			{
				Logger.Instance?.LogError("Failed to generate ID mapping for scene node branch!");
				goto abort;
			}

			_outData = new()
			{
				PrefabName = _node.Name ?? "scene_branch",
				ID = idMap.TryGetValue(_node, out int id) ? id : -1,
				Hierarchy = new()
				{
					TotalNodeCount = allNodes.Count,
					HierarchyDepth = 0,     //TODO
					TotalComponentCount = totalComponentCount,
					MaxComponentCount = maxComponentCount,
					NodeData = allNodes.Count != 0 ? new SceneNodeData[allNodes.Count] : null,
				}
			};
			// Set all array elements to null, they'll be written to later:
			if (_outData.Hierarchy.NodeData != null)
			{
				Array.Fill(_outData.Hierarchy.NodeData, null);
			}

			_outProgress.Update(null, 0, _outData.GetTotalSceneElementCount());

			// Save all node data:
			if (!SaveSceneNodes(in allNodes, in idMap, _outData, _outProgress))
			{
				Logger.Instance?.LogError("Failed to save node hierarchy for scene!");
				goto abort;
			}

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
			in SceneNode _node,
			Progress _progress,
			out List<SceneNode> _outAllNodes,
			out Dictionary<ISceneElement, int> _outIdMap,
			out int _outMaxComponentCount,
			out int _outTotalComponentCount)
		{
			_progress.Update("Generating scene ID map", 1, 3);

			_outAllNodes = new(1024);
			_outIdMap = new Dictionary<ISceneElement, int>(1024);
			_outMaxComponentCount = 0;
			_outTotalComponentCount = 0;

			int idCounter = 0;

			Queue<SceneNode> nodeQueue = new(1024);
			nodeQueue.Enqueue(_node);

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

		internal static bool SaveSceneNodes(
			in List<SceneNode> _allNodes,
			in Dictionary<ISceneElement, int> _idMap,
			SceneBranchData _sceneData,
			Progress _progress)
		{
			_progress?.UpdateTitle("Writing scene hierarchy");

			int index = 0;
			List<ComponentData> componentData = new(_sceneData.Hierarchy.MaxComponentCount);

			foreach (SceneNode node in _allNodes)
			{
				if (_idMap.TryGetValue(node, out int nodeId))
				{
					if (!node.SaveToData(out SceneNodeData nData))
					{
						Logger.Instance?.LogError($"Failed to save scene node '{node}' to data!");
						return false;
					}

					nData.ID = nodeId;
					if (node.Parent != null && _idMap.TryGetValue(node.Parent, out int parentId))
					{
						nData.ParentID = parentId;
					}

					componentData.Clear();
					for (int i = 0; i < node.ComponentCount; ++i)
					{
						if (node.GetComponent(i, out Component? component) &&
							component != null &&
							_idMap.TryGetValue(component, out int componentId))
						{
							if (!component.SaveToData(out ComponentData cData, _idMap))
							{
								Logger.Instance?.LogError($"Failed to save component '{component}' of scene node '{node}' to data!");
								return false;
							}

							cData.Type = component.GetType().ToString();
							cData.ID = componentId;
							cData.NodeID = nodeId;

							componentData.Add(cData);

							_progress?.Increment();
						}
					}
					nData.ComponentCount = componentData.Count;
					nData.ComponentData = componentData.Count != 0 ? componentData.ToArray() : null;

					_sceneData.Hierarchy.NodeData![index++] = nData;

					_progress?.Increment();
				}
			}
			for (int i = index; index < _sceneData.Hierarchy.NodeData!.Length; ++i)
			{
				_sceneData.Hierarchy.NodeData![i] = new();
			}
			return true;
		}

		#endregion
		#region Methods Loading

		public static bool LoadBranchFromFile(
			SceneNode _parentNode,
			string _filePath,
			out SceneNode? _outNode,
			out Progress _outProgress)
		{
			if (!Serializer.DeserializeJsonFromFile(_filePath, out SceneBranchData? data) || data == null)
			{
				_outNode = null;
				_outProgress = new("Deserialization failed", 0);
				_outProgress.Finish();
				return false;
			}

			return LoadBranchFromData(_parentNode, in data, out _outNode, out _outProgress);
		}

		/// <summary>
		/// Loads and spawns a branch of scene hierarchy into the scene, as a child to a given parent node.
		/// </summary>
		/// <param name="_parentNode">The parent node which we want to append the loaded branch to. Must be non-null.</param>
		/// <param name="_data">Save data from which to load scene nodes and components. The branch data is expected to be self-contained.
		/// Must be non-null and contain at least one node.</param>
		/// <param name="_outNode">Outputs the root node of the newly created hierarchy branch, or null, if loading failed.</param>
		/// <returns>True if the branch was successfully recreated from saved data, false otherwise.</returns>
		public static bool LoadBranchFromData(
			SceneNode _parentNode,
			in SceneBranchData _data,
			out SceneNode? _outNode,
			out Progress _outProgress)
		{
			_outProgress = new("Preparing to load nodes", 1);

			if (_parentNode == null || _parentNode.IsDisposed)
			{
				Logger.Instance?.LogError("Cannot load hierarchy branch under null or disposed parent node!");
				_outNode = null;
				goto abort;
			}
			if (_data == null)
			{
				Logger.Instance?.LogError("Cannot load hierarchy branch from null branch data!");
				_outNode = null;
				goto abort;
			}

			// First, try to recreate the ID mapping of all elements in the scene:
			if (!ReconstructBranchIdMap(in _data, out _, out int totalComponentCount))
			{
				Logger.Instance?.LogError("Failed to reconstruct ID map from scene data!");
				_outNode = null;
				goto abort;
			}

			int progressTaskCount = _data.GetTotalSceneElementCount();
			if (_data.Hierarchy != null) progressTaskCount += _data.Hierarchy.TotalComponentCount;
			_outProgress.Update(null, 0, progressTaskCount);

			_outNode = _parentNode.CreateChild(_data.PrefabName);

			// RECREATE:

			Dictionary<int, ISceneElement> idElementMap = [];

			// Create all nodes:
			if (!CreateBranchNodes(
				in _data,
				_outNode,
				idElementMap,
				out Dictionary<int, SceneNode> nodeIdMap,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to load and recreate scene hierarchy!");
				goto abort;
			}

			// Create all components and reattach them to nodes:
			if (!CreateComponents(
				in _data,
				in nodeIdMap,
				idElementMap,
				totalComponentCount,
				out List<Component> allComponents,
				out List<ComponentData> allComponentData,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to recreate components!");
				goto abort;
			}

			// RELOAD:

			// Load all components' states from data:
			if (!LoadComponents(
				in allComponents,
				in allComponentData,
				in idElementMap,
				_outProgress))
			{
				Logger.Instance?.LogError("Failed to load component states!");
				goto abort;
			}

			_outNode.SendEvent(SceneEventType.OnSetNodeEnabled, _outNode.IsEnabled);

			_outProgress.CompleteAllTasks();
			_outProgress.Finish();
			return true;

		abort:
			{
				_outProgress.errorCount++;
				_outProgress.Update("Loading process failed", 0, _outProgress.taskCount);
				_outProgress.Finish();
			}
			_outNode?.Dispose();
			_outNode = null;
			return false;
		}

		private static bool ReconstructBranchIdMap(in SceneBranchData _data, out Dictionary<int, ISceneElementData> _outIdMap, out int _outTotalComponentCount)
		{
			int expectedElementCount = 0;
			if (_data.Hierarchy?.NodeData != null)
			{
				expectedElementCount += _data.Hierarchy.NodeData.Length;
				expectedElementCount += _data.Hierarchy.TotalComponentCount;
			}

			_outIdMap = new Dictionary<int, ISceneElementData>(expectedElementCount);
			_outTotalComponentCount = 0;

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

						for (int j = 0; j < componentCount; ++j)
						{
							ComponentData cData = nData.ComponentData![j];
							if (cData != null && cData.ID >= 0 && !string.IsNullOrEmpty(cData.Type))
							{
								_outIdMap.Add(cData.ID, cData);
								_outTotalComponentCount++;
							}
						}
					}
				}
			}

			return true;
		}

		internal static bool CreateBranchNodes(
			in SceneBranchData _data,
			SceneNode _prefabNode,
			Dictionary<int, ISceneElement> _idElementMap,
			out Dictionary<int, SceneNode> _outNodeIdMap,
			Progress _progress)
		{
			_outNodeIdMap = [];

			_progress.UpdateTitle("Reading scene hierarchy");

			if (_data.Hierarchy?.NodeData == null) return true;

			int nodeCount = Math.Min(_data.Hierarchy.NodeData.Length, _data.Hierarchy.TotalNodeCount);
			if (nodeCount == 0)
			{
				Logger.Instance?.LogError("Hierarchy branch must contain at least a root node!");
				return false;
			}

			SceneNodeData rootNodeData = _data.Hierarchy.NodeData[0];
			if (!_prefabNode.LoadFromData(in rootNodeData))
			{
				Logger.Instance?.LogError("Failed to load prefabNode node from data!");
				return false;
			}
			_outNodeIdMap.Add(rootNodeData.ID, _prefabNode);

			for (int i = 1; i < nodeCount; i++)
			{
				SceneNodeData data = _data.Hierarchy.NodeData[i];
				if (data != null && data.ID >= 0 && _outNodeIdMap.TryGetValue(data.ParentID, out SceneNode? parentNode))
				{
					SceneNode node = parentNode.CreateChild(data.Name);
					if (!node.LoadFromData(in data))
					{
						Logger.Instance?.LogError($"Failed to load node '{data.Name ?? string.Empty}' from data!");
						return false;
					}
					_idElementMap.Add(data.ID, node);

					_progress.Increment();
				}
			}

			return true;
		}

		internal static bool CreateComponents(
			in SceneBranchData _data,
			in Dictionary<int, SceneNode> _nodeIdMap,
			Dictionary<int, ISceneElement> _idElementMap,
			int _totalComponentCount,
			out List<Component> _outAllComponents,
			out List<ComponentData> _outAllComponentData,
			Progress _progress)
		{
			_progress.UpdateTitle("Creating components");

			_outAllComponents = new(_totalComponentCount);
			_outAllComponentData = new(_totalComponentCount);
			
			if (_data.Hierarchy?.NodeData == null || _totalComponentCount == 0) return true;

			int nodeCount = Math.Min(_data.Hierarchy.NodeData.Length, _data.Hierarchy.TotalNodeCount);
			if (nodeCount == 0) return false;


			// Create all components on their nodes first, but don't initialize and interconnect them yet:
			for (int i = 0; i < nodeCount; ++i)
			{
				SceneNodeData nData = _data.Hierarchy.NodeData[i];
				if (nData.ID < 0 || !_nodeIdMap.TryGetValue(nData.ID, out SceneNode? node)) continue;

				int componentCount = nData.ComponentData != null ? nData.ComponentData.Length : 0;
				componentCount = Math.Min(componentCount, nData.ComponentCount);

				for (int j = 0; j < componentCount; j++)
				{
					ComponentData cData = nData.ComponentData![j];
					if (cData.ID < 0 || string.IsNullOrEmpty(cData.Type)) continue;

					if (!ComponentFactory.CreateComponent(node, cData.Type, out Component? component) || component == null)
					{
						Logger.Instance?.LogError($"Failed to create component of type '{cData.Type}' for node '{node}'!");
						return false;
					}
					node.AddComponent(component);

					_outAllComponents.Add(component);
					_outAllComponentData.Add(cData);
					_idElementMap.Add(cData.ID, component);

					_progress.Increment();
				}
			}
			return true;
		}

		internal static bool LoadComponents(
			in List<Component> _allComponents,
			in List<ComponentData> _allComponentData,
			in Dictionary<int, ISceneElement> _idMap,
			Progress _progress)
		{
			_progress.UpdateTitle("Initializing components");

			// Initialize components and load their states from data:
			for (int i = 0; i < _allComponents.Count; ++i)
			{
				Component component = _allComponents[i];
				ComponentData data = _allComponentData[i];

				if (!component.LoadFromData(in data, _idMap))
				{
					Logger.Instance?.LogError($"Failed to load and initialize component '{component}' on scene node '{component.node}'!");
					return false;
				}

				_progress.Increment();
			}

			return true;
		}

		#endregion
	}
}
