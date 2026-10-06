using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PokemonCapture : MonoBehaviour
{
    [SerializeField] private string pokemonName;
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP = 50;

    [Header("Barra de vida")]
    [SerializeField] private WorldHealthBar healthBar;

    [Header("Efecto de captura")]
    [SerializeField] private Color redColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField] private float redFadeDuration = 0.2f;
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private Material redMaterial;
    [SerializeField] private bool suckIntoBall = true;

    public UnityEvent<int> onHealed;
    public UnityEvent onCaptureStarted;
    public UnityEvent onCaptured;

    public bool IsCaptured { get; private set; }
    public bool IsBeingCaptured { get; private set; }
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public string PokemonName => string.IsNullOrWhiteSpace(pokemonName) ? name : pokemonName;

    public Vector3 CenterWorld
    {
        get
        {
            bool has = false;
            Bounds b = default;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return has ? b.center : transform.position;
        }
    }

    private struct TintMat
    {
        public Material mat;
        public int colorId;
        public Color baseColor;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] renderers;
    private Collider[] colliders;
    private readonly List<TintMat> tints = new List<TintMat>();
    private Material fallbackRed;

    void Awake()
    {
        RefreshChildren();
    }

    void Start()
    {
        if (healthBar != null)
        {
            healthBar.SetName(PokemonName);
            healthBar.SetHealthInstant(currentHP, maxHP);
        }
    }

    private void RefreshChildren()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void Heal(int amount)
    {
        if (IsCaptured || IsBeingCaptured) return;
        int before = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        Debug.Log($"{PokemonName} curado +{currentHP - before} PS ({currentHP}/{maxHP})");

        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);

        onHealed?.Invoke(currentHP - before);
    }

    public bool Capture(float duration, Transform sinkTarget = null)
    {
        if (IsCaptured || IsBeingCaptured) return false;
        IsBeingCaptured = true;

        RefreshChildren();

        foreach (var c in colliders) c.enabled = false;
        if (healthBar != null) healthBar.SetVisible(false);

        onCaptureStarted?.Invoke();
        StartCoroutine(CaptureRoutine(Mathf.Max(0.1f, duration), sinkTarget));
        return true;
    }

    private IEnumerator CaptureRoutine(float duration, Transform sinkTarget)
    {
        float redTime = Mathf.Max(0.01f, Mathf.Min(redFadeDuration, duration * 0.6f));
        float shrinkTime = Mathf.Max(0.05f, duration - redTime);

        PrepareRed();

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / redTime;
            ApplyRed(Mathf.Clamp01(t));
            yield return null;
        }
        ApplyRed(1f);

        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / shrinkTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, e);

            if (suckIntoBall && sinkTarget != null)
                transform.position = Vector3.Lerp(startPos, sinkTarget.position, e);

            yield return null;
        }

        transform.localScale = Vector3.zero;
        IsBeingCaptured = false;
        IsCaptured = true;
        HideVisuals();
        Debug.Log($"{PokemonName} capturado");
        onCaptured?.Invoke();
    }

    private Material GetFallbackRed()
    {
        if (fallbackRed != null) return fallbackRed;

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null)
        {
            return null;
        }

        fallbackRed = new Material(sh) { name = "RuntimeRedFallback" };
        if (fallbackRed.HasProperty(BaseColorId)) fallbackRed.SetColor(BaseColorId, redColor);
        if (fallbackRed.HasProperty(ColorId)) fallbackRed.SetColor(ColorId, redColor);
        return fallbackRed;
    }

    private void PrepareRed()
    {
        tints.Clear();

        if (redMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = redMaterial;
                r.sharedMaterials = arr;
            }
            return;
        }

        foreach (var r in renderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;

            Material[] mats = r.materials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                int id = m.HasProperty(BaseColorId) ? BaseColorId
                       : m.HasProperty(ColorId) ? ColorId : -1;

                if (id == -1)
                {
                    Material fb = GetFallbackRed();
                    if (fb != null) { mats[i] = fb; changed = true; }
                    continue;
                }

                if (m.HasProperty(EmissionId)) m.EnableKeyword("_EMISSION");

                tints.Add(new TintMat
                {
                    mat = m,
                    colorId = id,
                    baseColor = m.GetColor(id)
                });
            }

            if (changed) r.materials = mats;
        }
    }

    private void ApplyRed(float k)
    {
        foreach (var tm in tints)
        {
            if (tm.mat == null) continue;
            if (tm.colorId != -1)
                tm.mat.SetColor(tm.colorId, Color.Lerp(tm.baseColor, redColor, k));
            if (tm.mat.HasProperty(EmissionId))
                tm.mat.SetColor(EmissionId, redColor * (emissionIntensity * k));
        }
    }

    void LateUpdate()
    {
        if (IsCaptured) HideVisuals();
    }

    private void HideVisuals()
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = false;
    }
}