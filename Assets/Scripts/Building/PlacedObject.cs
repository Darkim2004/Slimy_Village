using UnityEngine;

/// <summary>
/// Componente iniettato negli oggetti piazzati dal BuildingSystem.
/// Tiene traccia della definizione e delle celle occupate sulla griglia.
/// Quando l'oggetto viene distrutto, libera le celle occupate.
/// </summary>
public class PlacedObject : MonoBehaviour
{
    [Header("Placement Data (set at runtime)")]
    [Tooltip("Definizione del piazzabile da cui è stato creato.")]
    public PlaceableDefinition definition;

    [Tooltip("Cella di origine sulla griglia (angolo basso-sinistra dell'area occupata).")]
    public Vector2Int gridOrigin;

    [Tooltip("Dimensioni in celle occupate.")]
    public Vector2Int gridSize = Vector2Int.one;

    /// <summary>
    /// Inizializza i dati di piazzamento. Chiamato dal BuildingSystem subito dopo l'istanziazione.
    /// </summary>
    public void Initialize(PlaceableDefinition def, Vector2Int origin, Vector2Int size)
    {
        definition = def;
        gridOrigin = origin;
        gridSize = size;
    }

    private void OnDestroy()
    {
        // Libera le celle sulla griglia quando l'oggetto viene distrutto
        var grid = FindFirstObjectByType<PlacementGrid>();
        if (grid != null)
            grid.FreeCells(gridOrigin, gridSize);
    }
}
