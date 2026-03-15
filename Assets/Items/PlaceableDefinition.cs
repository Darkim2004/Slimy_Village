using UnityEngine;

/// <summary>
/// Definisce le proprietà di un oggetto piazzabile nel mondo.
/// Ogni ItemDefinition di tipo Building può puntare a uno di questi asset.
/// </summary>
[CreateAssetMenu(menuName = "Game/Building/Placeable Definition")]
public class PlaceableDefinition : ScriptableObject
{
    [Header("Interaction")]
    [Tooltip("Se vero, il giocatore può interagire con questo oggetto (E).")]
    public bool canInteract = false;
    
    [Tooltip("Testo mostrato nel popup di interazione (es. 'Cassa', 'Letto').")]
    public string interactionText = "";

    [Space(10)]
    [Header("Grid Size")]
    [Tooltip("Dimensioni in celle della griglia (es. 1x1, 2x1, 2x2).")]
    public Vector2Int size = Vector2Int.one;

    [Tooltip("Offset del pivot rispetto all'angolo in basso a sinistra dell'area occupata.")]
    public Vector2Int pivotOffset = Vector2Int.zero;

    [Header("Prefab")]
    [Tooltip("Prefab istanziato quando l'oggetto viene piazzato nel mondo.")]
    public GameObject placedPrefab;

    [Header("Ghost Preview")]
    [Tooltip("Sprite per la preview fantasma. Se null, usa la sprite dal prefab.")]
    public Sprite ghostSprite;

    [Header("Spawn")]
    [Tooltip("Offset di spawn rispetto al centro della cella pivot.")]
    public Vector3 spawnOffset = Vector3.zero;

    /// <summary>
    /// Restituisce la sprite da usare per il ghost:
    /// ghostSprite se assegnata, altrimenti la sprite dal prefab.
    /// </summary>
    public Sprite GetGhostSprite()
    {
        if (ghostSprite != null) return ghostSprite;

        if (placedPrefab != null)
        {
            var sr = placedPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) return sr.sprite;
        }

        return null;
    }
}
