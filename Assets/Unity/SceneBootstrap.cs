using UnityEngine;

namespace FlagFootball.Unity
{
    public sealed class SceneBootstrap : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            IsInitialized = true;
            Debug.Log("BASELINE_SCENE_READY");
        }
    }
}
