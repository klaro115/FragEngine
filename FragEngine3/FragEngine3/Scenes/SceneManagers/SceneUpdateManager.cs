namespace FragEngine3.Scenes.SceneManagers
{
    /// <summary>
    /// Module of a <see cref="Scene"/> that manages registration of updatable scene elements, though mainly components.
    /// The different update stages (Fixed, Early, Main, Late) are executed through this type's '<see cref="RunUpdateStage"/>' method.
    /// </summary>
    /// <param name="_scene">The scene this manager instance is attached to.</param>
    internal sealed class SceneUpdateManager(Scene _scene) : IDisposable
    {
        #region Constructors

        ~SceneUpdateManager()
        {
            Dispose(false);
        }

        #endregion
        #region Fields

        public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(_scene), "Scene may not be null!");

        private readonly List<IUpdatableSceneElement> earlyUpdateList = [];
        private readonly List<IUpdatableSceneElement> mainUpdateList = [];
        private readonly List<IUpdatableSceneElement> lateUpdateList = [];
        private readonly List<IUpdatableSceneElement> fixedUpdateList = [];

        #endregion
        #region Properties

        public int EarlyUpdateListenerCount => earlyUpdateList.Count;
        public int MainUpdateListenerCount => mainUpdateList.Count;
        public int LateUpdateListenerCount => lateUpdateList.Count;
        public int FixedUpdateListenerCount => fixedUpdateList.Count;

        #endregion
        #region Methods

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
        private void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                Clear();
            }
        }

        public void Clear()
        {
            fixedUpdateList.Clear();
            earlyUpdateList.Clear();
            mainUpdateList.Clear();
            lateUpdateList.Clear();
        }

        public bool RunUpdateStage(SceneUpdateStage _updateStage)
        {
            List<IUpdatableSceneElement>? updateList = _updateStage switch
            {
                SceneUpdateStage.Early => earlyUpdateList,
                SceneUpdateStage.Main => mainUpdateList,
                SceneUpdateStage.Late => lateUpdateList,
                SceneUpdateStage.Fixed => fixedUpdateList,
                _ => null,
            };

            if (updateList == null) return false;
            if (updateList.Count == 0) return true;

            bool success = true;

            foreach (IUpdatableSceneElement element in updateList)
            {
                success &= element.HandleUpdate(_updateStage);
            }

            return success;
        }

        public bool RegisterSceneElement(IUpdatableSceneElement _newElement)
        {
            if (_newElement == null || _newElement.IsDisposed)
            {
                scene.Logger.LogError("Cannot register null or disposed scene element for update events!");
                return false;
            }

            SceneUpdateStage updateStageFlags = _newElement.UpdateStageFlags;
            if (updateStageFlags == 0)
            {
                return true;
            }

            if (updateStageFlags.HasFlag(SceneUpdateStage.Early) && !earlyUpdateList.Contains(_newElement))
            {
                earlyUpdateList.Add(_newElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Main) && !mainUpdateList.Contains(_newElement))
            {
                mainUpdateList.Add(_newElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Late) && !lateUpdateList.Contains(_newElement))
            {
                lateUpdateList.Add(_newElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Fixed) && !fixedUpdateList.Contains(_newElement))
            {
                fixedUpdateList.Add(_newElement);
            }

            return true;
        }

        public bool UnregisterSceneElements(IUpdatableSceneElement _oldElement)
        {
            if (_oldElement == null)
            {
                scene.Logger.LogError("Cannot unregister null scene element from update events!");
                return false;
            }

            SceneUpdateStage updateStageFlags = _oldElement.UpdateStageFlags;
            if (updateStageFlags == 0)
            {
                return true;
            }

            if (updateStageFlags.HasFlag(SceneUpdateStage.Early))
            {
                earlyUpdateList.Remove(_oldElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Main))
            {
                mainUpdateList.Remove(_oldElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Late))
            {
                lateUpdateList.Remove(_oldElement);
            }
            if (updateStageFlags.HasFlag(SceneUpdateStage.Fixed))
            {
                fixedUpdateList.Remove(_oldElement);
            }

            return true;
        }

        #endregion
    }
}
