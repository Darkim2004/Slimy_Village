using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Griglia di occupazione per il sistema di piazzamento.
/// Mantiene un HashSet di celle occupate e valida i piazzamenti.
/// Si inizializza leggendo la treeCollisionTilemap del WorldGen
/// per pre-popolare le celle bloccate (alberi, rocce).
/// </summary>
public class PlacementGrid : MonoBehaviour
{
    [Header("References")]
    [Tooltip("WorldGenTilemap per leggere le celle bloccate e il bioma.")]
    [SerializeField] private WorldGenTilemap worldGen;

    [Header("Collision Check")]
    [Tooltip("Layer da controllare per entità/collider nell'area di piazzamento.")]
    [SerializeField] private LayerMask entityBlockingLayers;

    [Tooltip("Margine di shrink per l'OverlapBox (evita falsi positivi ai bordi).")]
    [SerializeField] private float overlapShrink = 0.1f;

    // Celle occupate (da blocchi piazzati + alberi/rocce del WorldGen)
    private readonly HashSet<Vector2Int> _occupiedCells = new();

    private bool _initialized;

    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// Inizializza la griglia leggendo le celle bloccate dal WorldGen.
    /// Può essere chiamato anche manualmente se il WorldGen genera dopo.
    /// </summary>
    public void Initialize()
    {
        if (worldGen == null)
            worldGen = FindFirstObjectByType<WorldGenTilemap>();

        if (worldGen == null || !worldGen.HasGenerated)
        {
            Debug.LogWarning("[PlacementGrid] WorldGen non trovato o non generato. Riproverò più tardi.");
            return;
        }

        _occupiedCells.Clear();

        // Leggi tutte le celle bloccate dal WorldGen (alberi, rocce)
        for (int y = 0; y < worldGen.Height; y++)
        {
            for (int x = 0; x < worldGen.Width; x++)
            {
                if (worldGen.IsBlockedCell(x, y))
                    _occupiedCells.Add(new Vector2Int(x, y));
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// Controlla se un'area di celle è libera per il piazzamento.
    /// Verifica: celle non occupate, su terra (non oceano), nessuna entità sovrapposta.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        if (!_initialized)
            Initialize();

        if (worldGen == null) return false;

        for (int dy = 0; dy < size.y; dy++)
        {
            for (int dx = 0; dx < size.x; dx++)
            {
                Vector2Int cell = origin + new Vector2Int(dx, dy);

                // Fuori dalla mappa
                if (!worldGen.IsInside(cell.x, cell.y))
                    return false;

                // Oceano
                if (!worldGen.IsLandCell(cell.x, cell.y))
                    return false;

                // Già occupata (alberi, rocce, altri blocchi piazzati)
                if (_occupiedCells.Contains(cell))
                    return false;
            }
        }

        // Physics check: controlla che non ci siano entità nell'area
        if (entityBlockingLayers != 0)
        {
            Vector3 worldCenter = GetAreaWorldCenter(origin, size);
            Vector2 boxSize = new Vector2(size.x - overlapShrink, size.y - overlapShrink);

            Collider2D hit = Physics2D.OverlapBox(worldCenter, boxSize, 0f, entityBlockingLayers);
            if (hit != null)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Segna un'area di celle come occupata.
    /// </summary>
    public void OccupyCells(Vector2Int origin, Vector2Int size)
    {
        for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
                _occupiedCells.Add(origin + new Vector2Int(dx, dy));
    }

    /// <summary>
    /// Libera un'area di celle (quando un oggetto piazzato viene distrutto).
    /// </summary>
    public void FreeCells(Vector2Int origin, Vector2Int size)
    {
        for (int dy = 0; dy < size.y; dy++)
            for (int dx = 0; dx < size.x; dx++)
                _occupiedCells.Remove(origin + new Vector2Int(dx, dy));
    }

    /// <summary>
    /// Controlla se una singola cella è occupata.
    /// </summary>
    public bool IsCellOccupied(Vector2Int cell) => _occupiedCells.Contains(cell);

    /// <summary>
    /// Converte una posizione nel mondo in coordinate cella della griglia.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        if (worldGen == null || worldGen.GroundTilemap == null)
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

        Vector3Int cell3 = worldGen.GroundTilemap.WorldToCell(worldPos);
        return new Vector2Int(cell3.x, cell3.y);
    }

    /// <summary>
    /// Restituisce il centro nel mondo di una cella della griglia.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        if (worldGen == null)
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        return worldGen.CellCenterWorld(cell.x, cell.y);
    }

    /// <summary>
    /// Restituisce il centro nel mondo di un'area multi-cella.
    /// </summary>
    public Vector3 GetAreaWorldCenter(Vector2Int origin, Vector2Int size)
    {
        // Centro dell'area = media dei centri delle celle agli angoli
        Vector3 bottomLeft = CellToWorld(origin);
        Vector3 topRight = CellToWorld(origin + size - Vector2Int.one);
        return (bottomLeft + topRight) * 0.5f;
    }
}
