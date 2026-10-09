using UnityEngine;

namespace Hortensia.Runtime
{
    /// <summary>
    /// Supplies the native-display clear pass for the render-texture presentation.
    /// Gameplay cameras render only to the shared retro target, while the output canvas
    /// presents that target as an overlay. This camera keeps Display 1 alive in
    /// every scene without requiring each scene to carry a duplicate object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisplayOutputCamera : MonoBehaviour
    {
        private const string CameraObjectName = "Display Output Camera";
        private static DisplayOutputCamera instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateForDisplay()
        {
            if (instance != null)
                return;

            GameObject cameraObject = new(CameraObjectName);
            DontDestroyOnLoad(cameraObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -1000f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            instance = cameraObject.AddComponent<DisplayOutputCamera>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
