using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : MonoBehaviour
{
    public static Transform AimTarget;

    private enum State { Ready, Dragging, Thrown }

    [Header("Flotar delante de la cámara")]
    [SerializeField] private float followSmooth = 12f;
    [SerializeField] private float bobAmount = 0.01f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float popDuration = 0.25f;

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

    [Header("Ayuda de puntería")]
    [SerializeField, Range(0f, 1f)] private float assistStrength = 0.4f;
    [SerializeField] private float assistAngle = 25f;
    [SerializeField] private float maxSpeedMargin = 1.2f; 

    [Header("Vida")]
    [SerializeField] private float lifetimeAfterThrow = 6f;
    [SerializeField] private float lifetimeAfterHit = 2f;

    public UnityEvent<Collision> onHit;
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
        }
        else
        {
            target = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, distance))
                     + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);
            smooth = followSmooth;
        }

        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
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
        while (samples.Count > 0 && Time.unscaledTime - samples[0].t > 0.12f)
            samples.RemoveAt(0);

        if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            Release();
    }

    private void Release()
    {
        if (samples.Count < 2) { state = State.Ready; return; }

        Sample a = samples[0];
        Sample b = samples[samples.Count - 1];
        float dt = Mathf.Max(b.t - a.t, 0.016f);

        Vector2 v = (b.pos - a.pos) / dt / Screen.height;

        if (v.y < minFlick) { state = State.Ready; return; } 

        Throw(v);
    }

    private float SpeedForRange(float range)
    {
        float g = Physics.gravity.magnitude;
        float s = Mathf.Sin(2f * loftAngle * Mathf.Deg2Rad);
        return Mathf.Sqrt(range * g / Mathf.Max(s, 0.1f));
    }

    private void Throw(Vector2 v)
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
        if (AimTarget != null && assistStrength > 0f)
        {
            Vector3 to = AimTarget.position - transform.position;
            if (Vector3.Angle(dir, to) < assistAngle)
            {
                float t = Mathf.Clamp(to.magnitude / Mathf.Max(speed, 0.1f), 0.3f, 2f);
                Vector3 assist = to / t - 0.5f * Physics.gravity * t;
                velocity = Vector3.Lerp(velocity, assist, assistStrength);

                float cap = SpeedForRange(maxRange) * maxSpeedMargin;
                velocity = Vector3.ClampMagnitude(velocity, cap);
            }
        }

        state = State.Thrown;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce(velocity, ForceMode.VelocityChange);
        rb.AddTorque(right * -spin, ForceMode.VelocityChange); 

        Thrown?.Invoke(this);
        Destroy(gameObject, lifetimeAfterThrow);
    }

    void OnCollisionEnter(Collision c)
    {
        if (state != State.Thrown || hit) return;
        hit = true;
        onHit?.Invoke(c);
        Destroy(gameObject, lifetimeAfterHit);
    }
}