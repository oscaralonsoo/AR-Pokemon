using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorldHealthBar : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image fill;
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text nameText;

    [Header("Ajustes")]
    [SerializeField] private float fullBarSeconds = 2f;
    [SerializeField] private bool yawOnly = false;

    private float targetFill = 1f;
    private Camera cam;

    public float CurrentFill => fill != null ? fill.fillAmount : targetFill;

    void Awake()
    {
        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        if (nameText == null) nameText = GetComponentInChildren<TMP_Text>(true);
        cam = Camera.main;
        if (canvas != null) canvas.worldCamera = cam;
    }

    public void SetName(string pokemonName)
    {
        if (nameText != null) nameText.text = pokemonName;
    }

    public void SetHealth(int current, int max)
    {
        targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }

    public void SetHealthInstant(int current, int max)
    {
        SetHealth(current, max);
        if (fill != null) fill.fillAmount = targetFill;
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null) canvas.enabled = visible;
    }

    void Update()
    {
        if (fill != null)
        {
            float speed = 1f / Mathf.Max(0.01f, fullBarSeconds);
            fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, targetFill, speed * Time.deltaTime);
        }
    }

    void LateUpdate()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        if (yawOnly)
        {
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            transform.rotation = cam.transform.rotation;
        }
    }
}