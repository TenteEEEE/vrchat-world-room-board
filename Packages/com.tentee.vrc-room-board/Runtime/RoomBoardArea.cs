using UdonSharp;
using UnityEngine;

namespace RoomBoard
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RoomBoardArea : UdonSharpBehaviour
    {
        public string areaName = "Room";
        public Color areaColor;
        public BoxCollider[] volumes;
        // Rebuild geometry every query for areas attached to moving platforms.
        public bool movesAtRuntime = false;

        private Matrix4x4[] worldToLocal;
        private Vector3[] centers;
        private Vector3[] halves;
        private Vector3[] inverseScales;
        private bool geometryBuilt;
        private bool fallbackResolved;

        public void RefreshGeometry()
        {
            if ((volumes == null || volumes.Length == 0) && !fallbackResolved)
            {
                fallbackResolved = true;
                BoxCollider ownCollider = GetComponent<BoxCollider>();
                if (ownCollider == null)
                {
                    volumes = new BoxCollider[0];
                }
                else
                {
                    volumes = new BoxCollider[1];
                    volumes[0] = ownCollider;
                }
            }

            int count = volumes.Length;
            if (worldToLocal == null || worldToLocal.Length != count)
            {
                worldToLocal = new Matrix4x4[count];
                centers = new Vector3[count];
                halves = new Vector3[count];
                inverseScales = new Vector3[count];
            }

            for (int i = 0; i < count; i++)
            {
                BoxCollider box = volumes[i];
                if (box == null) continue;

                Transform boxTransform = box.transform;
                worldToLocal[i] = boxTransform.worldToLocalMatrix;
                centers[i] = box.center;
                halves[i] = box.size * 0.5f;
                Vector3 scale = boxTransform.lossyScale;
                scale.x = Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
                scale.y = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
                scale.z = Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
                inverseScales[i] = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            }

            geometryBuilt = true;
        }

        public Color GetAreaColor()
        {
            if (areaColor.a == 0f) return new Color(0x2E / 255f, 0xD3 / 255f, 0xC6 / 255f);
            return areaColor;
        }

        public bool ContainsPoint(Vector3 worldPoint, float marginMeters)
        {
            if (!geometryBuilt || movesAtRuntime) RefreshGeometry();
            if (volumes == null) return false;

            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] == null) continue;
                Vector3 local = worldToLocal[i].MultiplyPoint3x4(worldPoint) - centers[i];
                Vector3 half = halves[i];
                Vector3 inverseScale = inverseScales[i];
                if (Mathf.Abs(local.x) > half.x + marginMeters * inverseScale.x) continue;
                if (Mathf.Abs(local.y) > half.y + marginMeters * inverseScale.y) continue;
                if (Mathf.Abs(local.z) > half.z + marginMeters * inverseScale.z) continue;
                return true;
            }

            return false;
        }
    }
}
