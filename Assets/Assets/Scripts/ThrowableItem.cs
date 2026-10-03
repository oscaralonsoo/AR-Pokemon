using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : MonoBehaviour
{
    private enum State { Ready, Dragging, Thrown }

    [Header("Flotar delante de la cámara")]
    [SerializeField] private float followSmooth = 12f;
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

    [Header("Agarre")]
    [SerializeField, Range(0.05f, 0.4f)] private float grabRadius = 0.15f;
    [SerializeField] private float dragSmooth = 25f;

    [Header("Lanzamiento")]
    [SerializeField] private float minFlick = 1.0f;
    [SerializeField] private float maxFlick = 6f;
    [SerializeField] private float minRange = 0.8f;
    [SerializeField] private float maxRange = 3.5f;
    [SerializeField] private float powerCurve = 2f;
    [SerializeField, Range(10f, 70f)] private float loftAngle = 25f;
    [SerializeField] private float maxYaw = 35f;
    [SerializeField] private float spin = 6f;
    [SerializeField] private float flickWindow = 0.12f;

    [Header("Tiro curvo")]
    [SerializeField] private float curveWindow = 0.8f;
    [SerializeField] private float curveMinTurns = 0.5f;
    [SerializeField] private float curveFullTurns = 2f;
    [SerializeField] private float curveAcceleration = 2.5f;
    [SerializeField, Range(0f, 1f)] private float aimCompensation = 1f;
    [SerializeField] private bool invertCurve = false;

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

    [Header("Vida")]
    [SerializeField] private float lifetimeAfterThrow = 6f;
    [SerializeField] private float lifetimeAfterHit = 2f;

    public UnityEvent<Collision> onHit;
    public UnityEvent<float> onCurveThrow;
    public event Action<ThrowableItem> Thrown;

    private struct Sample { public Vector2 pos; public float t; }

    private readonly List<Sample> samples = new List<Sample>();
    private Rigidbody rb;
    private Camera cam;
    private Vector2 viewport;
    private float distance;
    private Vector3 baseScale;
    private float popT;
    private Vector2 fingerPos;
    private State state = State.Ready;
    private bool hit;
    private Vector3 curveAccel;

    private float spinAngle;
    private float spinVelocity;
    private Quaternion smoothCamRot;
    private bool rotationInitialized;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    public void Init(Camera camera, Vector2 viewportPos, float dist)
    {
        cam = camera;
        viewport = viewportPos;
        distance = dist;
    }

    void Update()
    {
        if (state == State.Thrown || cam == null) return;

        PopAnimation();

        var pointer = Pointer.current;
        if (pointer != null) HandleInput(pointer);

        Vector3 target;
        float smooth;

        if (state == State.Dragging)
        {
            target = cam.ScreenToWorldPoint(new Vector3(fingerPos.x, fingerPos.y, distance));
            smooth = dragSmooth;

            float c = ComputeCurve();
            float targetVel = 0f;
            if (Mathf.Abs(c) > 0f)
            {
                float dir = invertSpinVisual ? -1f : 1f;
                float intensity = Mathf.InverseLerp(0.25f, 1f, Mathf.Abs(c));
                targetVel = dir * Mathf.Sign(c) * Mathf.Lerp(spinMinSpeed, spinMaxSpeed, intensity);
            }

            // Acelera con el gesto, frena con inercia al dejar de girar
            float rate = Mathf.Abs(targetVel) > 0f ? spinAcceleration : spinDamping;
            spinVelocity = Mathf.Lerp(spinVelocity, targetVel, 1f - Mathf.Exp(-rate * Time.deltaTime));
        }
        else
        {
            target = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distance))
                     + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);
            smooth = followSmooth;

            // Al soltar sin lanzar sigue girando y frena poco a poco
            spinVelocity = Mathf.Lerp(spinVelocity, 0f, 1f - Mathf.Exp(-spinDamping * Time.deltaTime));

            if (Mathf.Abs(spinVelocity) < 20f)
            {
                spinVelocity = 0f;
                spinAngle = Mathf.Repeat(spinAngle + 180f, 360f) - 180f;
                spinAngle = Mathf.MoveTowards(spinAngle, 0f, spinReturnSpeed * Time.deltaTime);
            }
        }

        spinAngle += spinVelocity * Time.deltaTime;

        UpdateRotation();
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    private void UpdateRotation()
    {
        Vector3 axis = faceAxis.sqrMagnitude > 0.001f ? faceAxis.normalized : Vector3.up;

        // faceAxis (local) -> apunta hacia la cámara (-Z en espacio de cámara)
        Quaternion faceCam = Quaternion.FromToRotation(axis, Vector3.back);
        Quaternion roll = Quaternion.AngleAxis(rollOffset, Vector3.forward);
        Quaternion spinQ = Quaternion.AngleAxis(spinAngle, Vector3.forward);

        // Solo se suaviza el seguimiento de la cámara; el giro se aplica directo
        // para que sea rápido y sin retraso ni saltos.
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

        transform.rotation = smoothCamRot * spinQ * roll * faceCam;
    }

    void FixedUpdate()
    {
        if (state == State.Thrown && !hit && curveAccel != Vector3.zero)
            rb.AddForce(curveAccel, ForceMode.Acceleration);
    }

    private void PopAnimation()
    {
        if (popT >= 1f) return;
        popT = Mathf.Min(1f, popT + Time.deltaTime / popDuration);

        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = popT - 1f;
        float e = 1f + c3 * x * x * x + c1 * x * x;
        transform.localScale = baseScale * e;
    }

    private void HandleInput(Pointer pointer)
    {
        Vector2 pos = pointer.position.ReadValue();

        if (state == State.Ready && pointer.press.wasPressedThisFrame)
        {
            Vector2 ballScreen = cam.WorldToScreenPoint(transform.position);
            if (Vector2.Distance(pos, ballScreen) <= grabRadius * Screen.height)
            {
                state = State.Dragging;
                samples.Clear();
            }
        }

        if (state != State.Dragging) return;

        fingerPos = pos;
        samples.Add(new Sample { pos = pos, t = Time.unscaledTime });
        while (samples.Count > 0 && Time.unscaledTime - samples[0].t > curveWindow)
            samples.RemoveAt(0);

        if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            Release();
    }

    private float ComputeCurve()
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

    private void Release()
    {
        if (samples.Count < 2) { state = State.Ready; return; }

        Sample b = samples[samples.Count - 1];
        int startIndex = samples.Count - 1;
        while (startIndex > 0 && b.t - samples[startIndex - 1].t <= flickWindow) startIndex--;
        Sample a = samples[startIndex];

        float dt = Mathf.Max(b.t - a.t, 0.016f);
        Vector2 v = (b.pos - a.pos) / dt / Screen.height;

        if (v.y < minFlick) { state = State.Ready; return; }

        Throw(v, ComputeCurve());
    }

    private float SpeedForRange(float range)
    {
        float g = Physics.gravity.magnitude;
        float s = Mathf.Sin(2f * loftAngle * Mathf.Deg2Rad);
        return Mathf.Sqrt(range * g / Mathf.Max(s, 0.1f));
    }

    private void Throw(Vector2 v, float curve)
    {
        float power = Mathf.InverseLerp(minFlick, maxFlick, v.y);
        power = Mathf.Pow(power, powerCurve);

        float range = Mathf.Lerp(minRange, maxRange, power);
        float speed = SpeedForRange(range);
        float yaw = Mathf.Clamp(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg, -maxYaw, maxYaw);

        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * cam.transform.forward;
        dir.y = 0f;
        dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        dir = Quaternion.AngleAxis(-loftAngle, right) * dir;

        Vector3 velocity = dir * speed;

        float sideSign = invertCurve ? 1f : -1f;
        float lateral = sideSign * curve * curveAcceleration;
        curveAccel = right * lateral;

        if (Mathf.Abs(curve) > 0f)
        {
            float g = Physics.gravity.magnitude;
            float flightTime = 2f * speed * Mathf.Sin(loftAngle * Mathf.Deg2Rad) / g;
            velocity += right * (-0.5f * lateral * flightTime * aimCompensation);
            onCurveThrow?.Invoke(Mathf.Abs(curve));
        }

        state = State.Thrown;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = 50f; // por defecto Unity limita a 7 rad/s
        rb.AddForce(velocity, ForceMode.VelocityChange);
        rb.AddTorque(right * -spin, ForceMode.VelocityChange);

        // Conserva parte del giro visual en el aire
        if (Mathf.Abs(spinVelocity) > 1f)
        {
            Vector3 spinAxis = cam.transform.forward;
            rb.AddTorque(spinAxis * (spinVelocity * Mathf.Deg2Rad * throwSpinCarry), ForceMode.VelocityChange);
        }

        Thrown?.Invoke(this);
        Destroy(gameObject, lifetimeAfterThrow);
    }

    void OnCollisionEnter(Collision c)
    {
        if (state != State.Thrown || hit) return;
        hit = true;
        curveAccel = Vector3.zero;
        onHit?.Invoke(c);
        Destroy(gameObject, lifetimeAfterHit);
    }
}