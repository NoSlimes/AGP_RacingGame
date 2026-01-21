using UnityEngine;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class StartFinishBuilder : MonoBehaviour
    {
        [Header("References")] public TrackWaypointBuilder waypointBuilder;
        public Transform parent;

        [Header("Visual")] public Material startFinishMaterial;
        [Min(0.1f)] public float lineLength = 3.0f;
        [Min(0.01f)] public float lineThickness = 0.02f;
        public float yOffset = 0.05f;

        [Header("Sizing")] public float roadWidth = 8f;
        public float extraWidth = 0.2f;

        [Header("Trigger")] public bool createTrigger = true;
        public string triggerObjectName = "LapTrigger";
        public Vector3 triggerSize = new Vector3(9f, 3f, 2.5f);
        public Vector3 triggerLocalOffset = new Vector3(0f, 1.0f, 0f);

        [Header("Build")] public bool rebuild;

        private GameObject _lineGO;
        private GameObject _triggerGO;

        void OnEnable()
        {
            if (!parent) parent = transform;
            TryBuild();
        }

        void Update()
        {
            if (rebuild)
            {
                rebuild = false;
                TryBuild();
            }
        }

        public void TryBuild()
        {
            if (!waypointBuilder) return;

            // 2 waypoints to get forward direction
            var wps = waypointBuilder.Waypoints;
            if (wps == null || wps.Count < 2 || wps[0] == null || wps[1] == null)
                return;

            if (!parent) parent = transform;

            EnsureObjects();

            // Position at WP_000
            Vector3 p0 = wps[0].position;
            Vector3 p1 = wps[1].position;

            Vector3 forward = (p1 - p0);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

            _lineGO.transform.SetParent(parent, true);
            _lineGO.transform.position = p0 + Vector3.up * yOffset;
            _lineGO.transform.rotation = rot;

            // Size the line
            float width = roadWidth + extraWidth;
            _lineGO.transform.localScale = new Vector3(width, lineThickness, lineLength);

            // Material
            var mr = _lineGO.GetComponent<MeshRenderer>();
            if (startFinishMaterial) mr.sharedMaterial = startFinishMaterial;

            // Trigger
            if (createTrigger)
            {
                _triggerGO.SetActive(true);
                _triggerGO.transform.SetParent(_lineGO.transform, false);
                _triggerGO.transform.localPosition = triggerLocalOffset;

                // Make trigger match road width
                var bc = _triggerGO.GetComponent<BoxCollider>();
                bc.isTrigger = true;

                bc.size = new Vector3(triggerSize.x, triggerSize.y, triggerSize.z);
                bc.center = Vector3.zero;
            }
            else
            {
                if (_triggerGO) _triggerGO.SetActive(false);
            }
        }

        void EnsureObjects()
        {
            if (_lineGO == null)
            {
                _lineGO = GameObject.Find("StartFinishLine_AUTOGEN");
                if (_lineGO == null)
                {
                    _lineGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _lineGO.name = "StartFinishLine_AUTOGEN";
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(_lineGO.GetComponent<Collider>());
                    else
#endif
                        Destroy(_lineGO.GetComponent<Collider>());
                }
            }

            if (_triggerGO == null)
            {
                _triggerGO = GameObject.Find(triggerObjectName + "_AUTOGEN");
                if (_triggerGO == null)
                {
                    _triggerGO = new GameObject(triggerObjectName + "_AUTOGEN");
                    _triggerGO.AddComponent<BoxCollider>();
                }
            }
        }
    }
}