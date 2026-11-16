using UnityEngine;

namespace FlagFootball.Unity
{
    public sealed class SceneBootstrap : MonoBehaviour
    {
        [SerializeField, Min(80f)] private float joystickDiameter = 240f;
        [SerializeField, Min(0f)] private float joystickPadding = 64f;
        [SerializeField, Min(0.1f)] private float joystickSensitivity = 1f;

        private Renderer ground;
        private Material originalGroundMaterial, gridMaterial;
        private Texture2D gridTexture;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            IsInitialized = true;
            Debug.Log("BASELINE_SCENE_READY");
        }

        private void Start()
        {
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player) TouchJoystick.Create(player, joystickDiameter, joystickPadding, joystickSensitivity);
            AddFieldGrid();
        }

        private void AddFieldGrid()
        {
            var field = GameObject.Find("Ground");
            if (!field) return;
            ground = field.GetComponent<Renderer>();
            originalGroundMaterial = ground.sharedMaterial;
            gridMaterial = new Material(originalGroundMaterial);
            gridTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                { name = "PrototypeFieldGrid", wrapMode = TextureWrapMode.Repeat };
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
                gridTexture.SetPixel(x, y, x < 2 || y < 2
                    ? new Color(0.26f, 0.34f, 0.28f) : new Color(0.15f, 0.2f, 0.17f));
            gridTexture.Apply(false, true);
            gridMaterial.SetColor("_BaseColor", Color.white);
            gridMaterial.SetTexture("_BaseMap", gridTexture);
            gridMaterial.SetTextureScale("_BaseMap", Vector2.one * 10f);
            ground.sharedMaterial = gridMaterial;
        }

        private void OnDestroy()
        {
            if (ground) ground.sharedMaterial = originalGroundMaterial;
            if (gridMaterial) Destroy(gridMaterial);
            if (gridTexture) Destroy(gridTexture);
        }
    }
}
