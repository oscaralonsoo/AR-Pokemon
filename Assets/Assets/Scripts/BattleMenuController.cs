using UnityEngine;

public class BattleMenuController : MonoBehaviour
{
    public enum Menu { BattlePanel, FightMenu, BagPanel, PokemonMenu }

    [System.Serializable]
    public class MenuGroups
    {
        public Menu menu;
        public GroupAnimator[] groups;
    }

    [SerializeField] private MenuGroups[] menus;
    [SerializeField] private Menu current = Menu.BattlePanel;
    [SerializeField] private bool despawnItemsWhenLeavingBag = true;

    public void OpenMenu(int menuIndex) => Open((Menu)menuIndex);

    public void Open(Menu target)
    {
        if (target == current) return;

        if (despawnItemsWhenLeavingBag && current == Menu.BagPanel)
            ItemSpawner.DespawnAll();

        SetMenu(current, false);
        SetMenu(target, true);
        current = target;
    }

    public void Back()
    {
        ItemSpawner.DespawnAll();
        Open(Menu.BattlePanel);
    }

    private void SetMenu(Menu menu, bool show)
    {
        foreach (var m in menus)
        {
            if (m.menu != menu) continue;
            foreach (var g in m.groups)
            {
                if (show) g.Show();
                else g.Hide();
            }
        }
    }
}