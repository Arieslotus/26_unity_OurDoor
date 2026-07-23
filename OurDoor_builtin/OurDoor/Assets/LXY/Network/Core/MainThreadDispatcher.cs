using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace OurDoor.LXY.Networking.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Actions = new ConcurrentQueue<Action>();
        private static MainThreadDispatcher _instance;

        public static bool IsReady => _instance != null;

        public static void RequireInstance()
        {
            if (_instance == null)
            {
                throw new InvalidOperationException(
                    "[网络主线程队列] 场景中缺少 MainThreadDispatcher 组件。" +
                    "请在常驻网络对象上手动添加该组件。");
            }
        }

        public static void Post(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            RequireInstance();
            Actions.Enqueue(action);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                throw new InvalidOperationException(
                    $"[网络主线程队列] 检测到重复组件，对象：{gameObject.name}。");
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            while (Actions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
