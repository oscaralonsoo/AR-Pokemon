using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class PokeballCapture : MonoBehaviour
{
    [Header("Rebote")]
    [Tooltip("Altura (m) que sube la bola tras tocar al pokémon.")]
    [SerializeField] private float bounceHeight = 0.5f;
    [Tooltip("Tiempo (s) que dura el rebote (subida + bajada). Es también la duración del rayo y del encogimiento del pokémon.")]
    [SerializeField] private float bounceAirTime = 1.5f;
    [Tooltip("Velocidad con la que la bola se gira hacia el jugador mientras sube y cae (mayor = más rápido). 0 = gira libre por la física.")]
    [SerializeField] private float turnToPlayerSpeed = 10f;

    [Header("Rayo de captura")]
    [SerializeField] private Color beamColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Tooltip("Grosor del rayo en metros.")]
    [SerializeField] private float beamWidth = 0.04f;
    [Tooltip("OPCIONAL: material del rayo (p. ej. Unlit/aditivo rojo). Si no, usa Sprites/Default.")]
    [SerializeField] private Material beamMaterial;

    [Header("Caer de pie + animación")]
    [Tooltip("Eje LOCAL del modelo que debe apuntar hacia ARRIBA cuando la bola está de pie (el eje de la bisagra). Prueba (0,0,1), (0,1,0) o (1,0,0).")]
    [SerializeField] private Vector3 uprightAxis = Vector3.forward;
    [Tooltip("Duración (s) del giro suave hasta quedar de pie.")]
    [SerializeField] private float uprightDuration = 0.25f;
    [Tooltip("Pausa (s) entre quedar de pie y empezar la animación Catching.")]
    [SerializeField] private float pauseBeforeAnimation = 0.4f;
    [Tooltip("Nombre del trigger del Animator que lanza la animación de captura.")]
    [SerializeField] private string catchTriggerName = "Catching";

    [Header("Orientación de la animación")]
    [Tooltip("El eje +Z del clip apunta al jugador. Si tu clip mira hacia el lado o hacia atrás, corrige aquí: prueba 90, -90 o 180.")]
    [SerializeField] private float animationYawOffset = 0f;
    [Tooltip("Activado: el ancla usa la pose de pie completa en vez de mirar al jugador. Normalmente déjalo desactivado.")]
    [SerializeField] private bool anchorUsesUprightRotation = false;

    [Header("Partículas de atrapado")]
    [Tooltip("OPCIONAL: prefab con tu ParticleSystem. Si está vacío, se crea una ráfaga de chispas por defecto.")]
    [SerializeField] private GameObject captureParticlesPrefab;
    [Tooltip("Segundos desde que arranca la animación hasta que salen las partículas. Unity muestra '3:50' como segundo 3 + frame 50: a 60 fps son 3.83 s, a 30 fps serían 4.67 s.")]
    [SerializeField] private float particlesTime = 3.83f;
    [Tooltip("Activado: salen por tiempo. Desactívalo si prefieres un Animation Event que llame a PlayCaptureParticles().")]
    [SerializeField] private bool playParticlesByTime = true;
    [Tooltip("Segundos que viven las partículas antes de destruirse.")]
    [SerializeField] private float particlesLifetime = 3f;

    public UnityEvent onLanded;
    public event Action Landed;     
    public event Action CatchStarted; 

    public bool IsCapturing => capturing;

    private Rigidbody rb;
    private Animator animator;
    private Camera cam;
    private Transform anchor;
    private Vector3 faceAxis = Vector3.up;
    private bool capturing;
    private bool landed;
    private bool particlesPlayed;
    private float startTime;
    private float captureGravity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
    }

    void FixedUpdate()
    {
        if (!capturing || landed || rb.isKinematic) return;

        rb.AddForce(Vector3.down * captureGravity, ForceMode.Acceleration);

        if (turnToPlayerSpeed > 0f && cam != null)
        {
            rb.angularVelocity = Vector3.zero;
            float k = 1f - Mathf.Exp(-turnToPlayerSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, ComputeUprightRotation(), k));
        }
    }

    void OnDestroy()
    {
        if (anchor != null) Destroy(anchor.gameObject);
    }
    public bool TryStart(PokemonCapture pokemon, Camera camera, Vector3 ballFaceAxis)
    {
        if (capturing) return false;

        float airTime = Mathf.Max(0.2f, bounceAirTime);
        float height = Mathf.Max(0.05f, bounceHeight);

        captureGravity = 8f * height / (airTime * airTime);
        float upSpeed = 4f * height / airTime;

        if (!pokemon.Capture(airTime, transform)) return false;

        cam = camera;
        faceAxis = ballFaceAxis;
        capturing = true;
        startTime = Time.time;

        rb.useGravity = false;
        SetVelocity(Vector3.up * upSpeed);
        SpawnBeam(pokemon, airTime);
        return true;
    }

    private void SpawnBeam(PokemonCapture pokemon, float duration)
    {
        var go = new GameObject("CaptureBeam");
        go.transform.SetParent(transform, false);
        go.AddComponent<CaptureBeam>().Play(transform, pokemon, duration, beamColor, beamWidth, beamMaterial);
    }

    public bool HandleCollision(Collision c)
    {
        if (!capturing) return false;
        if (landed) return true;
        if (Time.time - startTime < 0.1f) return true; 
        if (c.collider.GetComponentInParent<PokemonCapture>() != null) return true;

        landed = true;
        Landed?.Invoke();
        onLanded?.Invoke();
        StartCoroutine(LandAndCatchRoutine());
        return true;
    }

    private IEnumerator LandAndCatchRoutine()
    {
        SetVelocity(Vector3.zero);
        rb.interpolation = RigidbodyInterpolation.None;
        rb.useGravity = false;
        rb.isKinematic = true;

        Quaternion from = transform.rotation;
        Quaternion to = ComputeUprightRotation();
        float duration = Mathf.Max(0.01f, uprightDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        transform.rotation = to;

        if (pauseBeforeAnimation > 0f)
            yield return new WaitForSeconds(pauseBeforeAnimation);

        anchor = new GameObject("CatchAnchor").transform;
        anchor.SetPositionAndRotation(transform.position, ComputeAnchorRotation(to));
        transform.SetParent(anchor, true); 

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = true;
            yield return null; 
            animator.SetTrigger(Animator.StringToHash(catchTriggerName));
        }

        CatchStarted?.Invoke();

        if (playParticlesByTime)
        {
            yield return new WaitForSeconds(particlesTime);
            PlayCaptureParticles();
        }
    }

    private Quaternion ComputeAnchorRotation(Quaternion uprightRot)
    {
        if (anchorUsesUprightRotation) return uprightRot;

        Vector3 toPlayer = Vector3.zero;
        if (cam != null)
        {
            toPlayer = Vector3.ProjectOnPlane(cam.transform.position - transform.position, Vector3.up);
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = Vector3.ProjectOnPlane(-cam.transform.forward, Vector3.up);
        }
        if (toPlayer.sqrMagnitude < 0.0001f) return Quaternion.identity;

        return Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
               * Quaternion.Euler(0f, animationYawOffset, 0f);
    }

    public void PlayCaptureParticles()
    {
        if (particlesPlayed) return;
        particlesPlayed = true;

        Vector3 pos = transform.position;
        GameObject go;

        if (captureParticlesPrefab != null)
        {
            go = Instantiate(captureParticlesPrefab, pos, Quaternion.identity);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        }
        else
        {
            go = CreateDefaultBurst(pos);
        }

        Destroy(go, particlesLifetime);
    }

    private GameObject CreateDefaultBurst(Vector3 pos)
    {
        var go = new GameObject("CaptureParticles");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.3f), Color.white);
        main.gravityModifier = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) rend.material = new Material(sh);

        ps.Play();
        return go;
    }

    private Quaternion ComputeUprightRotation()
    {
        Vector3 up = uprightAxis.sqrMagnitude > 0.001f ? uprightAxis.normalized : Vector3.forward;
        Quaternion stand = Quaternion.FromToRotation(up, Vector3.up);

        if (cam == null) return stand;

        Vector3 face = faceAxis.sqrMagnitude > 0.001f ? faceAxis.normalized : Vector3.up;
        Vector3 buttonDir = Vector3.ProjectOnPlane(stand * face, Vector3.up);
        Vector3 toCam = Vector3.ProjectOnPlane(-cam.transform.forward, Vector3.up);

        if (buttonDir.sqrMagnitude < 0.0001f || toCam.sqrMagnitude < 0.0001f) return stand;

        float yaw = Vector3.SignedAngle(buttonDir, toCam, Vector3.up);
        return Quaternion.AngleAxis(yaw, Vector3.up) * stand;
    }

    private void SetVelocity(Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }
}