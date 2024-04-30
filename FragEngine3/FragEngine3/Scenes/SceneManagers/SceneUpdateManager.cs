using FragEngine3.Scenes.EventSystem;

namespace FragEngine3.Scenes.SceneManagers;

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

    private readonly List<IOnEarlyUpdateListener> earlyUpdateList = [];
    private readonly List<IOnMainUpdateListener> mainUpdateList = [];
    private readonly List<IOnLateUpdateListener> lateUpdateList = [];
	private readonly List<IOnFixedUpdateListener> fixedUpdateList = [];

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
		bool success = true;

		switch (_updateStage)
        {
            // EARLY:
            case SceneUpdateStage.Early:
					foreach (var element in earlyUpdateList)
					{
						success &= element.OnEarlyUpdate();
					}
					break;

            // MAIN:
            case SceneUpdateStage.Main:
					foreach (var kvp in mainUpdateList)
					{
						success &= kvp.OnUpdate();
					}
					break;

            // LATE:
            case SceneUpdateStage.Late:
					foreach (var element in lateUpdateList)
					{
						success &= element.OnLateUpdate();
					}
					break;

            // FIXED:
            case SceneUpdateStage.Fixed:
					foreach (var element in fixedUpdateList)
					{
						success &= element.OnFixedUpdate();
					}
					break;

            default:
                return false;
        }

        return success;
    }

    public bool RegisterSceneElement(ISceneUpdateListener _newListener)
    {
        if (_newListener == null || _newListener.IsDisposed)
        {
            scene.Logger.LogError("Cannot register null or disposed scene event listener for update events!");
            return false;
        }

        bool wasRegistered = false;

        if (_newListener is IOnEarlyUpdateListener earlyUpdateListener)
        {
            SortedInsert(earlyUpdateList, earlyUpdateListener);
				wasRegistered = true;
			}
			if (_newListener is IOnMainUpdateListener mainUpdateListener)
			{
            SortedInsert(mainUpdateList, mainUpdateListener);
				wasRegistered = true;
			}
			if (_newListener is IOnLateUpdateListener lateUpdateListener)
			{
            SortedInsert(lateUpdateList, lateUpdateListener);
				wasRegistered = true;
			}
			if (_newListener is IOnFixedUpdateListener fixedUpdateListener)
			{
            SortedInsert(fixedUpdateList, fixedUpdateListener);
				wasRegistered = true;
			}

			return wasRegistered;
    }

    public bool UnregisterSceneElements(ISceneUpdateListener _oldListener)
    {
        if (_oldListener == null)
        {
            scene.Logger.LogError("Cannot unregister null scene event listener from update events!");
            return false;
        }

        bool wasRemoved = false;

			if (_oldListener is IOnEarlyUpdateListener earlyUpdateListener)
			{
				wasRemoved |= earlyUpdateList.Remove(earlyUpdateListener);
			}
			if (_oldListener is IOnMainUpdateListener mainUpdateListener)
			{
				wasRemoved |= mainUpdateList.Remove(mainUpdateListener);
			}
			if (_oldListener is IOnLateUpdateListener lateUpdateListener)
			{
				wasRemoved |= lateUpdateList.Remove(lateUpdateListener);
			}
			if (_oldListener is IOnFixedUpdateListener fixedUpdateListener)
			{
				wasRemoved |= fixedUpdateList.Remove(fixedUpdateListener);
			}

        return wasRemoved;
    }

    private static void SortedInsert<T>(List<T> _list, T _newListener) where T : class, ISceneUpdateListener
    {
        // If list is empty, add directly:
        if (_list.Count == 0)
        {
            _list.Add(_newListener);
            return;
        }

        // If lowest order, prepend to list:
        int order = _newListener.UpdateOrder;
        if (order < _list[0].UpdateOrder)
        {
				_list.Insert(0, _newListener);
            return;
			}

        // If highest order, append to list:
        int highIdx = _list.Count - 1;
        if (order >= _list[highIdx].UpdateOrder)
        {
            _list.Add(_newListener);
            return;
        }

        // Insert after searching for similarly weighted update order, using Newton pattern:
        int lowIdx = 0;
        int midIdx = _list.Count / 2;
        int mid = _list[midIdx].UpdateOrder;
        while (mid != order)
        {
            if (mid < order)
            {
                highIdx = midIdx;
            }
            else
            {
                lowIdx = midIdx;
            }
            midIdx = (highIdx + lowIdx) / 2;
            mid = _list[midIdx].UpdateOrder;
        }
        _list.Insert(midIdx, _newListener);
    }

    #endregion
}
