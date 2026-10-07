using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonSpawnEffect : MonoBehaviour
{
    [Header("Partículas")]
    [SerializeField] private GameObject spawnParticle;
    [SerializeField] private float particleFadeDuration = 0.5f;

    [Header("Aparición (inverso de la captura, en blanco)")]
    [SerializeField] private float duration = 1f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Transform originPoint;

    [Header("Fundido blanco")]
    [SerializeField] private Color whiteColor = Color.white;
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private AnimationCurve whiteAmountCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private struct TintMat
    {
        public Material mat;
        public int colorId;
        public Color baseColor;
        public Color baseEmission;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Vector3 originalScale;
    private Vector3 endPosition;
    private Coroutine effectRoutine;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private readonly List<Material> instancedMaterials = new List<Material>();
    private readonly List<TintMat> tints = new List<TintMat>();

    private GameObject particleInstance;
    private ParticleSystem[] particleSystems;

    private void Awake()
    {
        originalScale = transform.localScale;

        renderers = GetComponentsInChildren<Renderer>(true);
        originalMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterials;
        }
    }

    private void OnEnable()
    {
        endPosition = transform.position;

        SpawnParticle();

        if (effectRoutine != null) StopCoroutine(effectRoutine);
        effectRoutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
        if (originPoint != null) transform.position = endPosition;
        RestoreMaterials();
        DestroyParticle();
        effectRoutine = null;
    }

    private IEnumerator SpawnRoutine()
    {
        float safeDuration = Mathf.Max(0.05f, duration);

        PrepareTint();
        ApplyTint(whiteAmountCurve.Evaluate(0f));

        Vector3 startPos = originPoint != null ? originPoint.position : endPosition;
        transform.localScale = Vector3.zero;
        transform.position = startPos;

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            transform.localScale = originalScale * scaleCurve.Evaluate(t);

            if (originPoint != null)
                transform.position = Vector3.Lerp(startPos, endPosition, Mathf.SmoothStep(0f, 1f, t));

            ApplyTint(Mathf.Clamp01(whiteAmountCurve.Evaluate(t)));
            SyncParticle();
            yield return null;
        }

        transform.localScale = originalScale;
        transform.position = endPosition;

        ApplyTint(0f);
        RestoreMaterials();

        yield return FadeOutParticle();

        effectRoutine = null;
    }

    private void PrepareTint()
    {
        RestoreMaterials();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer) continue;

            Material[] mats = r.materials;
            for (int j = 0; j < mats.Length; j++)
            {
                Material m = mats[j];
                if (m == null) continue;

                instancedMaterials.Add(m);

                int id = m.HasProperty(BaseColorId) ? BaseColorId
                       : m.HasProperty(ColorId) ? ColorId : -1;
                if (id == -1) continue;

                if (m.HasProperty(EmissionId)) m.EnableKeyword("_EMISSION");

                tints.Add(new TintMat
                {
                    mat = m,
                    colorId = id,
                    baseColor = m.GetColor(id),
                    baseEmission = m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black
                });
            }
        }
    }

    private void ApplyTint(float k)
    {
        foreach (var tm in tints)
        {
            if (tm.mat == null) continue;

            tm.mat.SetColor(tm.colorId, Color.Lerp(tm.baseColor, whiteColor, k));

            if (tm.mat.HasProperty(EmissionId))
                tm.mat.SetColor(EmissionId, Color.Lerp(tm.baseEmission, whiteColor * emissionIntensity, k));
        }
    }

    private void RestoreMaterials()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        foreach (var m in instancedMaterials)
        {
            if (m != null) Destroy(m);
        }

        instancedMaterials.Clear();
        tints.Clear();
    }

    private void SpawnParticle()
    {
        DestroyParticle();

        if (spawnParticle == null)
        {
            Debug.LogWarning("No hay un prefab asignado en Spawn Particle.", this);
            return;
        }

        particleInstance = Instantiate(spawnParticle, transform.position, transform.rotation);
        particleSystems = particleInstance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var ps in particleSystems)
        {
            ps.Play(false);
        }
    }

    private void SyncParticle()
    {
        if (particleInstance == null) return;
        particleInstance.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    private IEnumerator FadeOutParticle()
    {
        if (particleInstance == null) yield break;

        foreach (var ps in particleSystems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        float elapsed = 0f;
        float previousFactor = 1f;
        var buffer = new ParticleSystem.Particle[0];

        while (elapsed < particleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float factor = 1f - Mathf.Clamp01(elapsed / particleFadeDuration);
            float ratio = previousFactor > 0f ? factor / previousFactor : 0f;
            previousFactor = factor;

            SyncParticle();

            foreach (var ps in particleSystems)
            {
                int max = ps.main.maxParticles;
                if (buffer.Length < max) buffer = new ParticleSystem.Particle[max];

                int count = ps.GetParticles(buffer);
                for (int i = 0; i < count; i++)
                {
                    Color32 c = buffer[i].startColor;
                    c.a = (byte)(c.a * ratio);
                    buffer[i].startColor = c;
                }
                ps.SetParticles(buffer, count);
            }

            yield return null;
        }

        DestroyParticle();
    }

    private void DestroyParticle()
    {
        if (particleInstance != null)
        {
            Destroy(particleInstance);
        }

        particleInstance = null;
        particleSystems = null;
    }
}