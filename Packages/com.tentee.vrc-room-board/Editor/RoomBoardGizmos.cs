#if UNITY_EDITOR
using RoomBoard;
using UnityEditor;
using UnityEngine;

namespace RoomBoardEditor
{
    public static class RoomBoardGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawArea(RoomBoardArea area, GizmoType gizmoType)
        {
            if (area == null) return;
            BoxCollider[] volumes = area.volumes;
            if (volumes == null || volumes.Length == 0)
            {
                BoxCollider ownBox = area.GetComponent<BoxCollider>();
                if (ownBox != null) volumes = new BoxCollider[] { ownBox };
            }
            if (volumes == null) return;

            bool selected = (gizmoType & GizmoType.Selected) != 0;
            Color areaColor = area.areaColor == Color.clear ? new Color32(0x2E, 0xD3, 0xC6, 0xFF) : area.areaColor;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            for (int i = 0; i < volumes.Length; i++)
            {
                BoxCollider box = volumes[i];
                if (box == null) continue;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                areaColor.a = selected ? 0.12f : 0.18f;
                Gizmos.color = areaColor;
                Gizmos.DrawCube(box.center, box.size);
                if (selected)
                {
                    areaColor.a = 1f;
                    Gizmos.color = areaColor;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
#endif
