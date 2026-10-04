using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CaptureBeam : MonoBehaviour
{
    private const int Points = 12;

    private LineRenderer lr;

    public void Play(Transform origin, Pokemon target, float duration,
                     Color color, float width, Material material)
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = Points;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (material == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) material = new Material(sh);
        }
        if (material != null) lr.material = material;

        StartCoroutine(Run(origin, target, Mathf.Max(0.1f, duration), color, width));
    }

    private IEnumerator Run(Transform origin, Pokemon target, float duration, Color color, float width)
    {
        float t = 0f;
        float jitter = width * 1.5f;

        while (t < duration && origin != null && target != null)
        {
            t += Time.deltaTime;

            // Entra rápido y se apaga al final
            float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((duration - t) / 0.2f);

            lr.widthMultiplier = width * env;
            Color c = color;
            c.a = color.a * env;
            lr.startColor = c;
            lr.endColor = c;

            Vector3 a = origin.position;
            Vector3 b = target.CenterWorld;

            for (int i = 0; i < Points; i++)
            {
                float f = i / (float)(Points - 1);
                Vector3 p = Vector3.Lerp(a, b, f);
                // Chisporroteo: nulo en los extremos, máximo en el centro
                if (i > 0 && i < Points - 1)
                    p += Random.insideUnitSphere * (jitter * Mathf.Sin(f * Mathf.PI));
                lr.SetPosition(i, p);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}