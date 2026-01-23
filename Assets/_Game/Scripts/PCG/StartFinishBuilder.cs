using UnityEngine;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class StartFinishBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TrackWaypointBuilder waypointBuilder;
        [SerializeField] private Transform parent;

        [Header("Visual")]
        [SerializeField] private Material startFinishMaterial;
        [SerializeField, Min(0.1f)] private float lineLength = 3.0f;
        [SerializeField, Min(0.01f)] private float lineThickness = 0.02f;
        [SerializeField] private float yOffset = 0.05f;

        [Header("Sizing")]
        [SerializeField] private float roadWidth = 8f;
        [SerializeField] private float extraWidth = 0.2f;

        [Header("Trigger")]
        [SerializeField] private bool createTrigger = true;
        [SerializeField] private string triggerObjectName = "LapTrigger";
        [SerializeField] private Vector3 triggerSize = new Vector3(9f, 3f, 2.5f);
        [SerializeField] private Vector3 triggerLocalOffset = new Vector3(0f, 1.0f, 0f);

        [Header("Build")]
        [SerializeField] private bool rebuild;

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

            var wps = waypointBuilder.Waypoints;
            if (wps == null || wps.Count < 2 || wps[0] == null || wps[1] == null) return;

            if (!parent) parent = transform;

            EnsureObjects();

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

            float width = roadWidth + extraWidth;
            _lineGO.transform.localScale = new Vector3(width, lineThickness, lineLength);

            var mr = _lineGO.GetComponent<MeshRenderer>();
            if (startFinishMaterial) mr.sharedMaterial = startFinishMaterial;

            if (createTrigger)
            {
                _triggerGO.SetActive(true);
                _triggerGO.transform.SetParent(_lineGO.transform, false);
                _triggerGO.transform.localPosition = triggerLocalOffset;

                var bc = _triggerGO.GetComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = triggerSize;
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
                _lineGO = new GameObject("StartFinishLine_AUTOGEN");
                _lineGO.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _lineGO.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                _lineGO.AddComponent<MeshRenderer>();
            }

            if (_triggerGO == null)
            {
                _triggerGO = new GameObject(triggerObjectName + "_AUTOGEN");
                _triggerGO.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _triggerGO.AddComponent<BoxCollider>();
                _triggerGO.SetActive(false);
            }
        }
    }
}
