using System.Collections;
using UnityEngine;

public class PokemonSpawnEffect : MonoBehaviour
{
    [SerializeField] private GameObject spawnParticle;
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float particleFadeDuration = 0.5f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 originalScale;
    private Coroutine effectRoutine;

    private Renderer[] renderers;
    private Material[][] originalMaterials;

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
        // Cada vez que se activa el GO se crea una partícula nueva
        SpawnParticle();

        if (effectRoutine != null) StopCoroutine(effectRoutine);
        effectRoutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        // Al desactivar el GO se limpia todo y se destruye la partícula
        transform.localScale = originalScale;
        RestoreMaterials();
        DestroyParticle();
        effectRoutine = null;
    }

    private IEnumerator SpawnRoutine()
    {
        // 1) Crece el GO con material blanco mientras la partícula está activa
        float elapsed = 0f;
        transform.localScale = Vector3.zero;
        SetWhiteMaterial();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = originalScale * scaleCurve.Evaluate(t);
            SyncParticle();
            yield return null;
        }

        transform.localScale = originalScale;
        RestoreMaterials();

        // 2) Desvanece y destruye la partícula
        yield return FadeOutParticle();

        effectRoutine = null;
    }

    private void SpawnParticle()
    {
        // Por si quedara una instancia anterior
        DestroyParticle();

        if (spawnParticle == null)
        {
            Debug.LogWarning("No hay un prefab asignado en Spawn Particle.", this);
            return;
        }

        // Sin padre, para que no le afecte la escala del GO
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

        // Deja de emitir partículas nuevas; las existentes siguen vivas mientras se desvanecen
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

    private void SetWhiteMaterial()
    {
        if (whiteMaterial == null)
        {
            Debug.LogWarning("No hay un material asignado en White Material.", this);
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] whites = new Material[originalMaterials[i].Length];
            for (int j = 0; j < whites.Length; j++)
            {
                whites[j] = whiteMaterial;
            }

            renderers[i].sharedMaterials = whites;
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
    }
}