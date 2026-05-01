using UnityEngine;

[System.Serializable]
public class SpawnRules
{
    [Header("Window (cells)")]
    public int windowSize = 32;
    public int SpawnRadiusCells => Mathf.Max(1, windowSize / 2);

    [Header("Min distance from player (cells)")]
    public int minDistanceFromPlayerCells = 16;

    [Header("Off-screen (Cinemachine ok)")]
    public bool requireOffscreen = true;
    public float offscreenMargin = 0.08f;

    [Header("Physics avoid")]
    public float avoidRadius = 0.35f;
    public LayerMask avoidMask;

    public bool IsOffscreen(Vector3 worldPos, Camera cam)
    {
        if (!requireOffscreen) return true;
        if (cam == null) return true;

        Vector3 v = cam.WorldToViewportPoint(worldPos);
        if (v.z < 0f) return true;

        float m = Mathf.Max(0f, offscreenMargin);
        bool onScreen =
            v.x >= -m && v.x <= 1f + m &&
            v.y >= -m && v.y <= 1f + m;

        return !onScreen;
    }
}
