using UnityEngine;
using UnityEngine.Rendering;

public class YSort : MonoBehaviour
{
    [Tooltip("Transform che rappresenta il 'piede' dell'oggetto. Se nullo usa transform.")]
    public Transform feet;

    [Tooltip("Più alto = più precisione nel sortingOrder.")]
    public int precision = 100;

    [Tooltip("Offset per forzare leggermente avanti/dietro.")]
    public int orderOffset = 0;

    private SortingGroup _sortingGroup;
    private Renderer[] _renderers;

    private void Awake()
    {
        _sortingGroup = GetComponent<SortingGroup>();
        _renderers = GetComponentsInChildren<Renderer>();
        if (feet == null) feet = transform;
    }

    private void LateUpdate()
    {
        // y più basso => order più alto => davanti
        int order = -(int)(feet.position.y * precision) + orderOffset;

        if (_sortingGroup != null)
        {
            _sortingGroup.sortingOrder = order;
        }
        else
        {
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].sortingOrder = order;
        }
    }
}