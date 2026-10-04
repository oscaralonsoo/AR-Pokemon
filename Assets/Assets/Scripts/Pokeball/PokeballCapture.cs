using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class PokeballCapture : MonoBehaviour
{
    [Header("Rebote")]
    [Tooltip("Velocidad vertical (m/s) del rebote al tocar al pokémon. Más alto = más tiempo de efecto (tiempo en el aire = 2·v/g).")]
    [SerializeField] private float bounceSpeed = 2.5f;

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
    [Tooltip("Nombre del trigger del Animator que lanza la animación de captura.")]
    [SerializeField] private string catchTriggerName = "Catching";
    [Tooltip("Desactivado: el ancla solo conserva el giro horizontal (el clip se mueve en ejes del mundo). Actívalo solo si tu clip está hecho respecto a la bola de pie.")]
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
    public event Action Landed;       // tocó el suelo
    public event Action CatchStarted; // ya de pie y con la animación lanzada

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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // El Animator se mantiene apagado hasta la captura para que no pise la rotación
        animator = GetComponentInChildren<Animator>(true);
        if (animator != null) animator.enabled = false;
    }

    void OnDestroy()
    {
        if (anchor != null) Destroy(anchor.gameObject);
    }

    /// <summary>Inicia la captura: rebota, lanza el rayo rojo y el pokémon se pone rojo y es absorbido.</summary>
    public bool TryStart(Pokemon pokemon, Camera camera, Vector3 ballFaceAxis)
    {
        if (capturing) return false;

        // Tiempo aproximado que la bola pasa en el aire (subida + bajada)
        float g = Physics.gravity.magnitude;
        float airTime = 2f * bounceSpeed / Mathf.Max(g, 0.01f);

        if (!pokemon.Capture(airTime, transform)) return false;

        cam = camera;
        faceAxis = ballFaceAxis;
        capturing = true;
        startTime = Time.time;

        SetVelocity(Vector3.up * bounceSpeed);
        SpawnBeam(pokemon, airTime);
        return true;
    }

    private void SpawnBeam(Pokemon pokemon, float duration)
    {
        var go = new GameObject("CaptureBeam");
        go.transform.SetParent(transform, false);
        go.AddComponent<CaptureBeam>().Play(transform, pokemon, duration, beamColor, beamWidth, beamMaterial);
    }

    /// <summary>Devuelve true si la colisión ha sido consumida por la captura.</summary>
    public bool HandleCollision(Collision c)
    {
        if (!capturing) return false;
        if (landed) return true;
        if (Time.time - startTime < 0.1f) return true; // ignora el contacto inicial
        if (c.collider.GetComponentInParent<Pokemon>() != null) return true;

        landed = true;
        Landed?.Invoke();
        onLanded?.Invoke();
        StartCoroutine(LandAndCatchRoutine());
        return true;
    }

    // Ya en el suelo: congela la física, se endereza y lanza la animación
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

        // Ancla en el punto de aterrizaje: las claves de posición del clip pasan a ser
        // relativas a este punto. Solo conserva el giro horizontal para que "arriba"
        // en el clip siga siendo arriba en el mundo.
        Quaternion anchorRot = anchorUsesUprightRotation
            ? to
            : Quaternion.Euler(0f, to.eulerAngles.y, 0f);

        anchor = new GameObject("CatchAnchor").transform;
        anchor.SetPositionAndRotation(transform.position, anchorRot);
        transform.SetParent(anchor, true); // mantiene la pose de pie de la bola

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.enabled = true;
            yield return null; // deja que el Animator se inicialice antes del trigger
            animator.SetTrigger(Animator.StringToHash(catchTriggerName));
        }

        CatchStarted?.Invoke();

        if (playParticlesByTime)
        {
            yield return new WaitForSeconds(particlesTime);
            PlayCaptureParticles();
        }
    }

    /// <summary>
    /// Lanza las partículas de atrapado. Se llama sola por tiempo, o puedes llamarla
    /// desde un Animation Event del clip (el script debe estar en el mismo objeto que el Animator).
    /// </summary>
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

    // Ráfaga de chispas por defecto (tamaños en metros: ajusta si tu escena AR es muy distinta)
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

    // De pie: uprightAxis (local) apunta al cielo y el botón (faceAxis) mira hacia la cámara en horizontal
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