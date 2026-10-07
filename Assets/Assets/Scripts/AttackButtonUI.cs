using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class AttackButtonUI : MonoBehaviour
{
    [SerializeField] private TypeButtonLibrary library;
    [SerializeField] private Image background;      // el Image del botón (Target Graphic)
    [SerializeField] private TMP_Text nameText;     // opcional
    [SerializeField] private TMP_Text damageText;   // opcional

    private Button button;

    void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (button == null) button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();
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

        var entry = library != null ? library.Get(type) : null;
        if (entry == null || entry.normal == null || background == null) return;

        background.sprite = entry.normal;

        // Para que pressed/highlighted/disabled también cambien según el tipo
        button.targetGraphic = background;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = entry.highlighted != null ? entry.highlighted : entry.normal,
            pressedSprite = entry.pressed != null ? entry.pressed : entry.normal,
            selectedSprite = entry.normal,
            disabledSprite = entry.disabled != null ? entry.disabled : entry.normal
        };
    }
}