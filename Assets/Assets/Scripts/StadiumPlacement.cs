using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
public class StadiumPlacement : MonoBehaviour
{
    [Header("Sockets del estadio")]
    [Tooltip("Socket del primer Pokémon escaneado (el del jugador)")]
    [SerializeField] private Transform playerSocket;
    [Tooltip("Socket del segundo Pokémon escaneado (el rival)")]
    [SerializeField] private Transform enemySocket;

    [Header("Toque")]
    [Tooltip("Cámara AR. Si se deja vacío usa Camera.main")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private LayerMask tapMask = ~0;
    [SerializeField] private float maxTapDistance = 20f;

    [Header("Efecto (segundos)")]
    [Tooltip("Duración de rojo + encogerse en la carta")]
    [SerializeField] private float disappearDuration = 0.8f;
    [Tooltip("Duración de crecer + volver al color en el socket")]
    [SerializeField] private float appearDuration = 0.8f;

    private readonly Dictionary<string, GameObject> _cards = new Dictionary<string, GameObject>();

    private readonly Dictionary<string, int> _socketIndex = new Dictionary<string, int>();

    private readonly HashSet<string> _movingCards = new HashSet<string>();
    private readonly HashSet<string> _placedCards = new HashSet<string>();


    public void RegisterCard(string cardName, GameObject cardObject)
    {
        if (string.IsNullOrEmpty(cardName) || cardObject == null)
            return;

        _cards[cardName] = cardObject;

        if (!_socketIndex.ContainsKey(cardName) && _socketIndex.Count < 2)
            _socketIndex[cardName] = _socketIndex.Count;
    }

    public bool IsLocked(string cardName)
    {
        return _movingCards.Contains(cardName) || _placedCards.Contains(cardName);
    }

    public void ResetStadium()
    {
        StopAllCoroutines();

        _cards.Clear();
        _socketIndex.Clear();
        _movingCards.Clear();
        _placedCards.Clear();
    }

    private void Update()
    {
        if (_cards.Count == 0)
            return;

        if (!TryGetTap(out Vector2 screenPos))
            return;

        if (IsPointerOverUI())
            return;

        HandleTap(screenPos);
    }

    private void HandleTap(Vector2 screenPos)
    {
        Camera cam = arCamera != null ? arCamera : Camera.main;

        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxTapDistance, tapMask, QueryTriggerInteraction.Collide))
            return;

        foreach (var pair in _cards)
        {
            if (pair.Value == null)
                continue;

            if (hit.transform.IsChildOf(pair.Value.transform))
            {
                SendToSocket(pair.Key, pair.Value);
                return;
            }
        }
    }

    private void SendToSocket(string cardName, GameObject cardObject)
    {
        if (IsLocked(cardName))
            return;

        if (!_socketIndex.TryGetValue(cardName, out int index))
        {
            Debug.LogWarning($"'{cardName}' no tiene socket asignado (solo hay 2).");
            return;
        }

        Transform socket = index == 0 ? playerSocket : enemySocket;

        if (socket == null)
        {
            Debug.LogWarning($"Falta asignar el socket {(index == 0 ? "del jugador" : "del rival")}.");
            return;
        }

        StartCoroutine(MoveToSocketRoutine(cardName, cardObject, socket));
    }

    private IEnumerator MoveToSocketRoutine(string cardName, GameObject cardObject, Transform socket)
    {
        _movingCards.Add(cardName);

        Transform root = cardObject.transform;
        PokemonCapture capture = cardObject.GetComponentInChildren<PokemonCapture>(true);

        Vector3 rootScale = root.localScale;
        Vector3 captureScale = capture != null ? capture.transform.localScale : rootScale;

        if (capture != null)
        {
            capture.PlayDisappear(disappearDuration);

            yield return new WaitUntil(() => capture == null || !capture.IsPlayingEffect);
        }

        if (cardObject == null || socket == null)
        {
            _movingCards.Remove(cardName);
            yield break;
        }

        root.SetParent(socket, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        Vector3 socketScale = socket.lossyScale;
        Vector3 compensatedRootScale = new Vector3(
            rootScale.x / SafeScale(socketScale.x),
            rootScale.y / SafeScale(socketScale.y),
            rootScale.z / SafeScale(socketScale.z)
        );

        if (capture != null)
        {
            Vector3 targetScale;

            if (capture.transform == root)
            {
                targetScale = compensatedRootScale;
            }
            else
            {
                root.localScale = compensatedRootScale;
                targetScale = captureScale;
            }

            capture.PlayAppear(appearDuration, targetScale);

            yield return new WaitUntil(() => capture == null || !capture.IsPlayingEffect);
        }
        else
        {
            root.localScale = compensatedRootScale;
        }

        _movingCards.Remove(cardName);
        _placedCards.Add(cardName);
    }

    private float SafeScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : value;
    }


    private bool TryGetTap(out Vector2 screenPos)
    {
        screenPos = default;

#if ENABLE_INPUT_SYSTEM
        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;

        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                screenPos = touch.position;
                return true;
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif

        return EventSystem.current.IsPointerOverGameObject();
    }
}