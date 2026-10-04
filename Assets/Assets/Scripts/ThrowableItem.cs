using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableItem : MonoBehaviour
{
    private enum State { Ready, Dragging, Thrown }
    private enum ItemType { Pokeball, Potion }

    [Header("Tipo de objeto")]
    [SerializeField] private ItemType itemType = ItemType.Pokeball;
    [SerializeField] private int potionHealAmount = 20;

    [Header("Agarre")]
    [SerializeField, Range(0.05f, 0.4f)] private float grabRadius = 0.15f;

    [Header("Gesto")]
    [SerializeField] private ThrowGestureTracker gesture = new ThrowGestureTracker();

    [Header("Lanzamiento")]
    [SerializeField] private ThrowBallistics ballistics = new ThrowBallistics();

    [Header("Visual en mano")]
    [SerializeField] private HoldVisuals visuals = new HoldVisuals();

    [Header("Vida")]
    [SerializeField] private float lifetimeAfterThrow = 6f;
    [SerializeField] private float lifetimeAfterHit = 2f;
    [Tooltip("Tiempo antes de destruir el objeto si ha tocado un Pokémon (poción).")]
    [SerializeField] private float lifetimeAfterPokemonHit = 0.15f;
    [Tooltip("Seguridad: si la captura no termina de caer al suelo en este tiempo, se destruye.")]
    [SerializeField] private float captureTimeout = 5f;

    [Header("Fin de la captura")]
    [SerializeField] private float catchAnimationDuration = 4.25f;
    [SerializeField] private bool destroyAfterCatch = true;

    public UnityEvent<Collision> onHit;
    public UnityEvent<float> onCurveThrow;
    public UnityEvent<Pokemon> onPokemonHit;
    public event Action<ThrowableItem> Thrown;

    public event Action<ThrowableItem> Resolved;

    private Rigidbody rb;
    private PokeballCapture capture;
    private Camera cam;
    private Vector2 viewport;
    private float distance;
    private Vector2 fingerPos;
    private State state = State.Ready;
    private bool hit;
    private bool resolved;
    private Vector3 curveAccel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        visuals.Setup(transform);

        if (itemType == ItemType.Pokeball)
        {
            capture = GetComponent<PokeballCapture>();
            if (capture == null) capture = gameObject.AddComponent<PokeballCapture>();
            capture.Landed += OnCaptureLanded;
            capture.CatchStarted += OnCatchStarted;
        }
    }

    void OnDestroy()
    {
        if (capture != null)
        {
            capture.Landed -= OnCaptureLanded;
            capture.CatchStarted -= OnCatchStarted;
        }
        RaiseResolved(); 
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

        var pointer = Pointer.current;
        if (pointer != null) HandleInput(pointer);

        if (state == State.Thrown) return;

        bool dragging = state == State.Dragging;
        float curve = dragging ? gesture.ComputeCurve() : 0f;
        visuals.Tick(transform, cam, viewport, distance, dragging, fingerPos, curve);
    }

    void FixedUpdate()
    {
        if (state == State.Thrown && !hit && curveAccel != Vector3.zero)
            rb.AddForce(curveAccel, ForceMode.Acceleration);
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
                gesture.Clear();
            }
        }

        if (state != State.Dragging) return;

        fingerPos = pos;
        gesture.Add(pos);

        if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            Release();
    }

    private void Release()
    {
        if (!gesture.TryGetFlick(out Vector2 v) || v.y < ballistics.MinFlick)
        {
            state = State.Ready;
            return;
        }

        Throw(v, gesture.ComputeCurve());
    }

    private void Throw(Vector2 v, float curve)
    {
        ThrowBallistics.Result r = ballistics.Compute(v, curve, cam.transform.forward);
        curveAccel = r.curveAccel;

        if (r.curveAmount > 0f) onCurveThrow?.Invoke(r.curveAmount);

        state = State.Thrown;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = 50f; 
        rb.AddForce(r.velocity, ForceMode.VelocityChange);
        rb.AddTorque(r.spinTorque, ForceMode.VelocityChange);

        float spinVel = visuals.SpinVelocity;
        if (Mathf.Abs(spinVel) > 1f)
        {
            Vector3 spinAxis = cam.transform.forward;
            rb.AddTorque(spinAxis * (spinVel * Mathf.Deg2Rad * visuals.ThrowSpinCarry), ForceMode.VelocityChange);
        }

        Thrown?.Invoke(this);
        ScheduleDestroy(lifetimeAfterThrow);
    }

    void OnCollisionEnter(Collision c)
    {
        if (state != State.Thrown) return;

        if (capture != null && capture.HandleCollision(c)) return;

        if (hit) return;
        hit = true;
        curveAccel = Vector3.zero;

        Pokemon pokemon = c.collider.GetComponentInParent<Pokemon>();
        if (pokemon != null)
        {
            if (itemType == ItemType.Pokeball)
            {
                if (capture != null && capture.TryStart(pokemon, cam, visuals.FaceAxis))
                {
                    ScheduleDestroy(captureTimeout);
                    onPokemonHit?.Invoke(pokemon);
                    onHit?.Invoke(c);
                    return;
                }
            }
            else
            {
                pokemon.Heal(potionHealAmount);
            }

            onPokemonHit?.Invoke(pokemon);
            onHit?.Invoke(c);
            ScheduleDestroy(lifetimeAfterPokemonHit);
            RaiseResolved();
            return;
        }

        onHit?.Invoke(c);
        ScheduleDestroy(lifetimeAfterHit);
        RaiseResolved();
    }

    private void OnCaptureLanded()
    {
        CancelInvoke(nameof(DestroySelf)); 
    }

    private void OnCatchStarted()
    {
        Invoke(nameof(RaiseResolved), catchAnimationDuration);
        if (destroyAfterCatch)
            ScheduleDestroy(catchAnimationDuration);
    }

    private void RaiseResolved()
    {
        if (resolved) return;
        resolved = true;
        Resolved?.Invoke(this);
    }

    private void ScheduleDestroy(float delay)
    {
        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), delay);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}