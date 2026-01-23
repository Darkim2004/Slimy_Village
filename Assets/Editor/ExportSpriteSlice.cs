using UnityEngine;
using UnityEditor;
using System.IO;

public class ExportSpriteSlice
{
    [MenuItem("Tools/Export Selected Sprite")]
    static void Export()
    {
        Sprite sprite = Selection.activeObject as Sprite;
        if (sprite == null)
        {
            Debug.LogError("Seleziona UNA sprite (slice), non la texture intera.");
            return;
        }

        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;

        Texture2D newTex = new Texture2D((int)r.width, (int)r.height);
        Color[] pixels = tex.GetPixels(
            (int)r.x,
            (int)r.y,
            (int)r.width,
            (int)r.height
        );

        newTex.SetPixels(pixels);
        newTex.Apply();

        byte[] png = newTex.EncodeToPNG();
        File.WriteAllBytes("Assets/Exported_" + sprite.name + ".png", png);

        AssetDatabase.Refresh();
    }
}

