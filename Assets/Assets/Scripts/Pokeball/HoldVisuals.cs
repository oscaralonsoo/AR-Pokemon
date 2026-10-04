using System;
using UnityEngine;

[Serializable]
public class HoldVisuals
{
    [Header("Flotar delante de la cámara")]
    [SerializeField] private float followSmooth = 12f;
    [SerializeField] private float dragSmooth = 25f;
    [SerializeField] private float bobAmount = 0.01f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float popDuration = 0.25f;

    [Header("Orientación hacia el usuario")]
    [Tooltip("Eje LOCAL del modelo que debe apuntar hacia la cámara (la cara del botón).")]
    [SerializeField] private Vector3 faceAxis = Vector3.up;
    [Tooltip("Giro extra (grados) alrededor del eje de visión para colocar la bola recta.")]
    [SerializeField] private float rollOffset = 0f;
    [SerializeField] private float rotationSmooth = 15f;
    [SerializeField] private float spinReturnSpeed = 360f;

    [Header("Giro visual (estilo Pokémon GO)")]
    [Tooltip("Velocidad de giro (°/s) con el círculo más suave.")]
    [SerializeField] private float spinMinSpeed = 540f;
    [Tooltip("Velocidad de giro (°/s) con el círculo más intenso.")]
    [SerializeField] private float spinMaxSpeed = 1440f;
    [Tooltip("Qué rápido acelera hacia la velocidad objetivo (mayor = más nervioso).")]
    [SerializeField] private float spinAcceleration = 10f;
    [Tooltip("Qué rápido frena al dejar de girar o soltar (menor = más inercia).")]
    [SerializeField] private float spinDamping = 2.5f;
    [Tooltip("Cuánto del giro se conserva en el aire al lanzar (0-1).")]
    [SerializeField, Range(0f, 1f)] private float throwSpinCarry = 0.5f;
    [SerializeField] private bool invertSpinVisual = false;

    public Vector3 FaceAxis => faceAxis;
    public float SpinVelocity => spinVelocity;
    public float ThrowSpinCarry => throwSpinCarry;

    private Vector3 baseScale;
    private float popT;
    private float spinAngle;
    private float spinVelocity;
    private Quaternion smoothCamRot;
    private bool rotationInitialized;

    public void Setup(Transform t)
    {
        baseScale = t.localScale;
        t.localScale = Vector3.zero;
        popT = 0f;
    }

    public void Tick(Transform t, Camera cam, Vector2 viewport, float distance,
                     bool dragging, Vector2 fingerPos, float curve)
    {
        Pop(t);

        Vector3 target;
        float smooth;

        if (dragging)
        {
            target = cam.ScreenToWorldPoint(new Vector3(fingerPos.x, fingerPos.y, distance));
            smooth = dragSmooth;

            float targetVel = 0f;
            if (Mathf.Abs(curve) > 0f)
            {
                float dir = invertSpinVisual ? -1f : 1f;
                float intensity = Mathf.InverseLerp(0.25f, 1f, Mathf.Abs(curve));
                targetVel = dir * Mathf.Sign(curve) * Mathf.Lerp(spinMinSpeed, spinMaxSpeed, intensity);
            }

            float rate = Mathf.Abs(targetVel) > 0f ? spinAcceleration : spinDamping;
            spinVelocity = Mathf.Lerp(spinVelocity, targetVel, 1f - Mathf.Exp(-rate * Time.deltaTime));
        }
        else
        {
            target = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distance))
                     + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);
            smooth = followSmooth;

            spinVelocity = Mathf.Lerp(spinVelocity, 0f, 1f - Mathf.Exp(-spinDamping * Time.deltaTime));

            if (Mathf.Abs(spinVelocity) < 20f)
            {
                spinVelocity = 0f;
                spinAngle = Mathf.Repeat(spinAngle + 180f, 360f) - 180f;
                spinAngle = Mathf.MoveTowards(spinAngle, 0f, spinReturnSpeed * Time.deltaTime);
            }
        }

        spinAngle += spinVelocity * Time.deltaTime;

        UpdateRotation(t, cam);
        t.position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    private void Pop(Transform t)
    {
        if (popT >= 1f) return;
        popT = Mathf.Min(1f, popT + Time.deltaTime / popDuration);

        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = popT - 1f;
        float e = 1f + c3 * x * x * x + c1 * x * x;
        t.localScale = baseScale * e;
    }

    private void UpdateRotation(Transform t, Camera cam)
    {
        Vector3 axis = faceAxis.sqrMagnitude > 0.001f ? faceAxis.normalized : Vector3.up;

        Quaternion faceCam = Quaternion.FromToRotation(axis, Vector3.back);
        Quaternion roll = Quaternion.AngleAxis(rollOffset, Vector3.forward);
        Quaternion spinQ = Quaternion.AngleAxis(spinAngle, Vector3.forward);

        if (!rotationInitialized)
        {
            smoothCamRot = cam.transform.rotation;
            rotationInitialized = true;
        }
        else
        {
            smoothCamRot = Quaternion.Slerp(
                smoothCamRot, cam.transform.rotation, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
        }

        t.rotation = smoothCamRot * spinQ * roll * faceCam;
    }
}