using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ThrowGestureTracker
{
    [Header("Flick")]
    [SerializeField] private float flickWindow = 0.12f;

    [Header("Tiro curvo")]
    [SerializeField] private float curveWindow = 0.8f;
    [SerializeField] private float curveMinTurns = 0.5f;
    [SerializeField] private float curveFullTurns = 2f;

    private struct Sample { public Vector2 pos; public float t; }

    [NonSerialized] private readonly List<Sample> samples = new List<Sample>();

    public void Clear() => samples.Clear();

    public void Add(Vector2 pos)
    {
        samples.Add(new Sample { pos = pos, t = Time.unscaledTime });
        while (samples.Count > 0 && Time.unscaledTime - samples[0].t > curveWindow)
            samples.RemoveAt(0);
    }

    public bool TryGetFlick(out Vector2 v)
    {
        v = Vector2.zero;
        if (samples.Count < 2) return false;

        Sample b = samples[samples.Count - 1];
        int startIndex = samples.Count - 1;
        while (startIndex > 0 && b.t - samples[startIndex - 1].t <= flickWindow) startIndex--;
        Sample a = samples[startIndex];

        float dt = Mathf.Max(b.t - a.t, 0.016f);
        v = (b.pos - a.pos) / dt / Screen.height;
        return true;
    }

    public float ComputeCurve()
    {
        if (samples.Count < 4) return 0f;

        Vector2 center = Vector2.zero;
        foreach (var s in samples) center += s.pos;
        center /= samples.Count;

        float minRadius = 0.02f * Screen.height;
        float total = 0f;

        for (int i = 1; i < samples.Count; i++)
        {
            Vector2 a = samples[i - 1].pos - center;
            Vector2 b = samples[i].pos - center;
            if (a.magnitude < minRadius || b.magnitude < minRadius) continue;
            total += Vector2.SignedAngle(a, b);
        }

        float turns = Mathf.Abs(total) / 360f;
        if (turns < curveMinTurns) return 0f;

        float t = Mathf.InverseLerp(curveMinTurns, curveFullTurns, turns);
        float amount = Mathf.Lerp(0.25f, 1f, t);
        return Mathf.Sign(total) * amount;
    }
}