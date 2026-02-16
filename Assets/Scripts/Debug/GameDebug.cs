using UnityEngine;

public static class GameDebug
{
    private const string ResourcesPath = "GameDebugSettings";

    private static bool triedLoad;
    private static GameDebugSettings settings;

    public static void ReloadSettings()
    {
        triedLoad = false;
        settings = null;
        EnsureSettingsLoaded();
    }

    public static void Log(GameDebugCategory category, object message, Object context = null)
    {
        if (!ShouldLog(category)) return;

        if (context != null) Debug.Log(message, context);
        else Debug.Log(message);
    }

    public static void Warning(GameDebugCategory category, object message, Object context = null)
    {
        if (!ShouldLog(category)) return;

        if (context != null) Debug.LogWarning(message, context);
        else Debug.LogWarning(message);
    }

    public static void Error(GameDebugCategory category, object message, Object context = null)
    {
        if (!ShouldLog(category)) return;

        if (context != null) Debug.LogError(message, context);
        else Debug.LogError(message);
    }

    public static bool IsEnabled(GameDebugCategory category)
    {
        return ShouldLog(category);
    }

    private static bool ShouldLog(GameDebugCategory category)
    {
        EnsureSettingsLoaded();

        // Nessun asset trovato → tutto spento (per evitare spam)
        if (settings == null)
            return false;

        if (!settings.LoggingEnabled)
            return false;

        return settings.IsCategoryEnabled(category);
    }

    private static void EnsureSettingsLoaded()
    {
        if (triedLoad) return;
        triedLoad = true;

        settings = Resources.Load<GameDebugSettings>(ResourcesPath);

        if (settings == null)
            Debug.LogWarning(
                "[GameDebug] Asset 'GameDebugSettings' non trovato in Resources/. " +
                "Crea: Create → Game → Debug → Debug Settings e salvalo in Assets/Resources/GameDebugSettings.asset. " +
                "Fino ad allora tutti i log di gioco sono DISATTIVATI.");
    }
}
