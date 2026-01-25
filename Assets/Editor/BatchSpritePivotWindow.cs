using UnityEditor;
using UnityEngine;
using UnityEditor.U2D.Sprites; // ISpriteEditorDataProvider, SpriteRect, SpriteDataProviderFactories

public class BatchSpritePivotWindow : EditorWindow
{
    private enum PivotPreset
    {
        Center,
        TopLeft, Top, TopRight,
        Left, Right,
        BottomLeft, Bottom, BottomRight,
        Custom
    }

    private PivotPreset preset = PivotPreset.Center;
    private Vector2 customPivot = new Vector2(0.5f, 0.5f); // 0..1 nello spazio sprite
    private static SpriteDataProviderFactories _factories;

    [MenuItem("Tools/Batch Sprite Pivot (Data Provider)")]
    public static void Open() => GetWindow<BatchSpritePivotWindow>("Batch Sprite Pivot");

    private void OnEnable()
    {
        // Inizializza una volta le factories
        _factories ??= new SpriteDataProviderFactories();
        _factories.Init();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch pivot per slice (Sprite Mode: Multiple)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        preset = (PivotPreset)EditorGUILayout.EnumPopup("Pivot preset", preset);

        if (preset == PivotPreset.Custom)
        {
            customPivot = EditorGUILayout.Vector2Field("Custom pivot (0..1)", customPivot);
            customPivot.x = Mathf.Clamp01(customPivot.x);
            customPivot.y = Mathf.Clamp01(customPivot.y);
        }

        EditorGUILayout.HelpBox(
            "Seleziona una o più Texture2D nel Project (Sprite Mode = Multiple) e premi Apply.\n" +
            "Il tool imposta il pivot su tutte le slice e reimporta l'asset.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!HasValidSelection()))
        {
            if (GUILayout.Button("Apply pivot to selected textures", GUILayout.Height(32)))
                ApplyToSelection();
        }
    }

    private bool HasValidSelection()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0) return false;

        foreach (var o in objs)
            if (o is Texture2D) return true;

        return false;
    }

    private void ApplyToSelection()
    {
        Vector2 pivot = GetPivotFromPreset(preset, customPivot);

        int processed = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is not Texture2D tex) { skipped++; continue; }

                // Crea un data provider per questa texture
                var dataProvider = _factories.GetSpriteEditorDataProviderFromObject(tex);
                if (dataProvider == null) { skipped++; continue; }

                dataProvider.InitSpriteEditorDataProvider();

                // Lavoriamo solo su spritesheet (Multiple)
                if (dataProvider.spriteImportMode != SpriteImportMode.Multiple)
                {
                    skipped++;
                    continue;
                }

                var spriteRects = dataProvider.GetSpriteRects();
                if (spriteRects == null || spriteRects.Length == 0)
                {
                    skipped++;
                    continue;
                }

                // Modifica pivot su tutte le slice
                foreach (var rect in spriteRects)
                {
                    rect.pivot = pivot;
                    rect.alignment = SpriteAlignment.Custom;
                }

                dataProvider.SetSpriteRects(spriteRects);

                // Applica le modifiche e reimporta
                dataProvider.Apply();

                if (dataProvider.targetObject is AssetImporter importer)
                    importer.SaveAndReimport();

                processed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Batch Sprite Pivot: processed={processed}, skipped={skipped}, pivot={pivot}");
    }

    private static Vector2 GetPivotFromPreset(PivotPreset p, Vector2 custom)
    {
        return p switch
        {
            PivotPreset.Center => new Vector2(0.5f, 0.5f),

            PivotPreset.TopLeft => new Vector2(0f, 1f),
            PivotPreset.Top => new Vector2(0.5f, 1f),
            PivotPreset.TopRight => new Vector2(1f, 1f),

            PivotPreset.Left => new Vector2(0f, 0.5f),
            PivotPreset.Right => new Vector2(1f, 0.5f),

            PivotPreset.BottomLeft => new Vector2(0f, 0f),
            PivotPreset.Bottom => new Vector2(0.5f, 0f),
            PivotPreset.BottomRight => new Vector2(1f, 0f),

            PivotPreset.Custom => new Vector2(Mathf.Clamp01(custom.x), Mathf.Clamp01(custom.y)),
            _ => new Vector2(0.5f, 0.5f)
        };
    }
}