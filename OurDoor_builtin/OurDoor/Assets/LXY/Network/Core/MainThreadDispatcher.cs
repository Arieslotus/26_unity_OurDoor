using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace OurDoor.LXY.Networking.Core
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Actions = new ConcurrentQueue<Action>();
        private static MainThreadDispatcher _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            var gameObject = new GameObject("[LXY] MainThreadDispatcher");
            _instance = gameObject.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(gameObject);
        }

        public static void Post(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            Actions.Enqueue(action);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
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
