using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;                  // per l'eventuale orologio UI
using UnityEngine.Rendering;  // per il Volume (post processing)

public class DayNightScript : MonoBehaviour
{
    // 0 = pieno giorno, 1 = piena notte
    public static float NightFactor { get; private set; } = 0f;

    [Header("UI (opzionale)")]
    public TextMeshProUGUI timeDisplay; // Display Time (puoi anche lasciarlo null)
    public TextMeshProUGUI dayDisplay;  // Display Day  (puoi anche lasciarlo null)

    [Header("Post Processing")]
    public Volume ppv; // post processing volume (assegnalo da Inspector o con GetComponent)

    [Header("Tempo di gioco")]
    public float tick = 1f;   // 1 = 1 secondo di gioco al secondo reale (con FixedUpdate a 0.02)
    public bool useDebugSpeed = false;
    public float debugMultiplier = 300f;  // quanto più veloce in modalità debug

    public float seconds;
    public int mins;
    public int hours;
    public int days = 1;

    [Header("Ambiente")]
    public SpriteRenderer[] stars;  // sprites delle stelle

    // Luci "fisse" gestite on/off (lampioni principali, ecc.) – opzionale
    public GameObject[] staticNightLights;
    private bool staticLightsOn = false;

    void Start()
    {
        if (ppv == null)
        {
            ppv = GetComponent<Volume>(); // se il Volume è sullo stesso GO
        }
    }

    void FixedUpdate() // usiamo FixedUpdate per avere passo costante
    {
        CalcTime();
        ControlPPV();
        DisplayTime();
    }

    void CalcTime()
    {
        float currentTick = tick;

        if (useDebugSpeed)
        {
            currentTick *= debugMultiplier;
        }

        seconds += Time.fixedDeltaTime * currentTick;

        if (seconds >= 60f)
        {
            seconds = 0f;
            mins += 1;
        }

        if (mins >= 60)
        {
            mins = 0;
            hours += 1;
        }

        if (hours >= 24)
        {
            hours = 0;
            days += 1;
        }
    }

    void ControlPPV()
    {
        float tMinute = mins / 60f; // da 0 a 1 all'interno dell'ora corrente

        // DUSK: 21:00 → 22:00
        if (hours >= 21 && hours < 22)
        {
            float t = tMinute; // 0 -> 1

            // Volume: da 0 (giorno) a 1 (notte)
            if (ppv != null)
                ppv.weight = t;

            // Stelle: da invisibili a visibili
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    Color c = stars[i].color;
                    c.a = t;
                    stars[i].color = c;
                }
            }

            // Quanto è notte? (0 -> 1)
            NightFactor = t;
        }
        // DAWN: 6:00 → 7:00
        else if (hours >= 6 && hours < 7)
        {
            float t = tMinute; // 0 -> 1

            // Volume: da 1 (notte) a 0 (giorno)
            if (ppv != null)
                ppv.weight = 1f - t;

            // Stelle: da visibili a invisibili
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    Color c = stars[i].color;
                    c.a = 1f - t;
                    stars[i].color = c;
                }
            }

            // Quanto è notte? (1 -> 0)
            NightFactor = 1f - t;
        }
        // GIORNO PIENO: 7:00 → 21:00
        else if (hours >= 7 && hours < 21)
        {
            if (ppv != null)
                ppv.weight = 0f;

            // Stelle completamente invisibili
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    Color c = stars[i].color;
                    c.a = 0f;
                    stars[i].color = c;
                }
            }

            NightFactor = 0f;
        }
        // NOTTE PIENA: 22:00 → 6:00
        else
        {
            if (ppv != null)
                ppv.weight = 1f;

            // Stelle completamente visibili
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    Color c = stars[i].color;
                    c.a = 1f;
                    stars[i].color = c;
                }
            }

            NightFactor = 1f;
        }

        ControlStaticNightLights();
    }

    void ControlStaticNightLights()
    {
        // sistema semplice: se è "molto notte" accendo, se è "molto giorno" spengo
        if (!staticLightsOn && NightFactor > 0.8f)
        {
            for (int i = 0; i < staticNightLights.Length; i++)
            {
                if (staticNightLights[i] != null)
                    staticNightLights[i].SetActive(true);
            }
            staticLightsOn = true;
        }
        else if (staticLightsOn && NightFactor < 0.2f)
        {
            for (int i = 0; i < staticNightLights.Length; i++)
            {
                if (staticNightLights[i] != null)
                    staticNightLights[i].SetActive(false);
            }
            staticLightsOn = false;
        }
    }

    void DisplayTime()
    {
        if (timeDisplay != null)
        {
            timeDisplay.text = string.Format("{0:00}:{1:00}", hours, mins);
        }

        if (dayDisplay != null)
        {
            dayDisplay.text = "Day: " + days;
        }
    }
}
