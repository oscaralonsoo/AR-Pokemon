using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class AttackButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private TypeButtonLibrary library;
    [SerializeField] private Image background; 
    [SerializeField] private TMP_Text nameText;   
    [SerializeField] private TMP_Text damageText; 

    [Header("Feedback al pulsar")]
    [SerializeField, Range(0.7f, 1f)] private float pressedScale = 0.92f;
    [SerializeField] private float scaleSpeed = 20f;
    [SerializeField, Range(0.3f, 1f)] private float pressedTint = 0.7f;
    [SerializeField, Range(0.2f, 1f)] private float disabledTint = 0.55f;

    private Button button;
    private Vector3 baseScale;
    private float targetScaleMul = 1f;

    private bool typeApplied;
    private PokemonType lastType;

    void Awake()
    {
        Init();
        baseScale = transform.localScale;
    }

    void OnDisable()
    {
        targetScaleMul = 1f;
        if (baseScale != Vector3.zero)
            transform.localScale = baseScale;
    }

    void Update()
    {
        Vector3 target = baseScale * targetScaleMul;
        if ((transform.localScale - target).sqrMagnitude > 0.00001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, target, scaleSpeed * Time.unscaledDeltaTime);
        }
    }

    private void Init()
    {
        if (button == null) button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && button.interactable)
            targetScaleMul = pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScaleMul = 1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScaleMul = 1f;
    }

    public void Setup(string attackName, int damage, string typeName, UnityAction onClick = null)
    {
        Init();

        if (nameText != null) nameText.text = attackName;
        if (damageText != null) damageText.text = damage.ToString();

        ApplyType(TypeButtonLibrary.Parse(typeName));

        if (onClick != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }
    }

    public void SetDamage(int damage)
    {
        if (damageText != null)
            damageText.text = damage.ToString();
    }

    public void ApplyType(PokemonType type)
    {
        Init();

        if (typeApplied && lastType == type) return;

        var entry = library != null ? library.Get(type) : null;
        if (entry == null || entry.normal == null || background == null) return;

        typeApplied = true;
        lastType = type;

        background.sprite = entry.normal;
        button.targetGraphic = background;

        bool hasSwapSprites =
            (entry.pressed != null && entry.pressed != entry.normal) ||
            (entry.highlighted != null && entry.highlighted != entry.normal);

        if (hasSwapSprites)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = entry.highlighted != null ? entry.highlighted : entry.normal,
                pressedSprite = entry.pressed != null ? entry.pressed : entry.normal,
                selectedSprite = entry.normal,
                disabledSprite = entry.disabled != null ? entry.disabled : entry.normal
            };
        }
        else
        {
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = Color.white,
                pressedColor = new Color(pressedTint, pressedTint, pressedTint, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(disabledTint, disabledTint, disabledTint, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }
    }
}