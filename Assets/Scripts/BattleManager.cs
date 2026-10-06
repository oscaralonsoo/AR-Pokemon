using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleManager : MonoBehaviour
{
    public enum BattleState
    {
        WaitingForPlayer,
        WaitingForEnemy,
        PlayerTurn,
        EnemyTurn,
        BattleOver,
        Resolving  
    }

    private const string TriggerAttack = "Attack";     
    private const string TriggerAttack2 = "Attack2";   
    private const string TriggerDamage = "Damage";
    private const string TriggerDeath = "Death";

    [Header("Battle State")]
    [SerializeField] private BattleState currentState;

    [Header("Datos")]
    [Tooltip("Ruta dentro de una carpeta Resources, sin extensión. " +
             "El archivo debe estar en Assets/Resources/pokemonsdb.json")]
    [SerializeField] private string databaseResourcePath = "pokemonsdb";

    [Header("UI")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text enemyNameText;
    [SerializeField] private Text playerHPText;
    [SerializeField] private Text enemyHPText;

    [Header("Botones de ataque")]
    [SerializeField] private Button attackButton1;
    [SerializeField] private Button attackButton2;

    [Header("Textos de los botones (TextMeshPro)")]
    [Tooltip("Si el texto es un hijo del botón puedes dejarlo vacío: se busca solo")]
    [SerializeField] private TMP_Text attackButton1TMP;
    [SerializeField] private TMP_Text attackButton2TMP;

    [Header("Reinicio (opcional)")]
    [SerializeField] private Button resetButton;
    [SerializeField] private MultipleImagesTrackingManager trackingManager;

    [Header("Tiempos (segundos)")]
    [Tooltip("Tiempo desde que empieza la animación de ataque hasta que el golpe llega")]
    [SerializeField] private float attackHitDelay = 0.6f;
    [Tooltip("Duración aproximada de la animación Damage")]
    [SerializeField] private float hitReactionDuration = 0.8f;
    [Tooltip("Duración aproximada de la animación Death")]
    [SerializeField] private float deathDuration = 2f;
    [Tooltip("Pausa antes de que la IA ataque")]
    [SerializeField] private float enemyThinkDelay = 0.8f;
    [Tooltip("Duración del efecto rojo + encogerse al morir (el mismo de la captura)")]
    [SerializeField] private float defeatEffectDuration = 1f;

    private PokemonDatabase database;

    private Pokemon playerPokemon;
    private Pokemon enemyPokemon;

    private int playerCurrentHP;
    private int enemyCurrentHP;

    private WorldHealthBar playerHealthBar;
    private WorldHealthBar enemyHealthBar;

    private Animator playerAnimator;
    private Animator enemyAnimator;

    private PokemonCapture playerCapture;
    private PokemonCapture enemyCapture;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        LoadDatabase();

        if (attackButton1 != null)
            attackButton1.onClick.AddListener(() => PlayerAttack(0));

        if (attackButton2 != null)
            attackButton2.onClick.AddListener(() => PlayerAttack(1));

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetBattle);
    }

    private void Start()
    {
        currentState = BattleState.WaitingForPlayer;
        UpdateBattleUI();
    }


    // =========================================================
    // LOAD DATABASE
    // =========================================================

    private void LoadDatabase()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(databaseResourcePath);

        if (jsonFile == null)
        {
            Debug.LogError(
                $"No se ha encontrado el JSON en Resources/{databaseResourcePath}. " +
                "Ponlo en Assets/Resources/pokemonsdb.json"
            );
            return;
        }

        database = JsonUtility.FromJson<PokemonDatabase>(jsonFile.text);

        if (database == null || database.pokemon == null)
        {
            Debug.LogError("No se ha podido cargar la base de datos de Pokémon.");
            return;
        }

        Debug.Log($"Base de datos cargada: {database.pokemon.Length} Pokémon.");
    }


    // =========================================================
    // REGISTER CARD
    // =========================================================

    public void RegisterCard(string pokemonName, GameObject cardObject = null)
    {
        if (string.IsNullOrEmpty(pokemonName))
            return;

        if (database == null)
        {
            Debug.LogError("La base de datos no está cargada.");
            return;
        }

        switch (currentState)
        {
            case BattleState.WaitingForPlayer:
                RegisterPlayerPokemon(pokemonName, cardObject);
                break;

            case BattleState.WaitingForEnemy:
                RegisterEnemyPokemon(pokemonName, cardObject);
                break;

            default:
                Debug.LogWarning(
                    $"No se puede registrar una carta durante el estado: {currentState}"
                );
                break;
        }
    }

    private void RegisterPlayerPokemon(string pokemonName, GameObject cardObject)
    {
        playerPokemon = FindPokemon(pokemonName);

        if (playerPokemon == null)
        {
            Debug.LogError($"No se ha encontrado '{pokemonName}' en el JSON.");
            return;
        }

        playerCurrentHP = playerPokemon.hp;

        playerHealthBar = FindHealthBar(cardObject);
        playerAnimator = FindAnimator(cardObject);
        playerCapture = FindCapture(cardObject);

        if (playerCapture != null)
            playerCapture.SetFullHealth();

        SetupHealthBar(playerHealthBar, playerPokemon, playerCurrentHP);

        Debug.Log($"Jugador: {playerPokemon.name} | HP: {playerCurrentHP} | Tipo: {playerPokemon.type}");

        currentState = BattleState.WaitingForEnemy;
        Debug.Log("Esperando a que se escanee la carta del rival...");

        UpdateBattleUI();
    }

    private void RegisterEnemyPokemon(string pokemonName, GameObject cardObject)
    {
        enemyPokemon = FindPokemon(pokemonName);

        if (enemyPokemon == null)
        {
            Debug.LogError($"No se ha encontrado '{pokemonName}' en el JSON.");
            return;
        }

        enemyCurrentHP = enemyPokemon.hp;

        enemyHealthBar = FindHealthBar(cardObject);
        enemyAnimator = FindAnimator(cardObject);
        enemyCapture = FindCapture(cardObject);

        if (enemyCapture != null)
            enemyCapture.SetFullHealth();

        SetupHealthBar(enemyHealthBar, enemyPokemon, enemyCurrentHP);

        Debug.Log($"Rival: {enemyPokemon.name} | HP: {enemyCurrentHP} | Tipo: {enemyPokemon.type}");

        StartBattle();
    }

    private Pokemon FindPokemon(string pokemonName)
    {
        foreach (Pokemon pokemon in database.pokemon)
        {
            if (string.Equals(pokemon.name, pokemonName, StringComparison.OrdinalIgnoreCase))
                return pokemon;
        }

        return null;
    }


    // =========================================================
    // HEALTH BARS / ANIMATORS
    // =========================================================

    private WorldHealthBar FindHealthBar(GameObject cardObject)
    {
        if (cardObject == null)
            return null;

        return cardObject.GetComponentInChildren<WorldHealthBar>(true);
    }

    private Animator FindAnimator(GameObject cardObject)
    {
        if (cardObject == null)
            return null;

        return cardObject.GetComponentInChildren<Animator>(true);
    }

    private PokemonCapture FindCapture(GameObject cardObject)
    {
        if (cardObject == null)
            return null;

        return cardObject.GetComponentInChildren<PokemonCapture>(true);
    }
    private void SetupHealthBar(WorldHealthBar bar, Pokemon pokemon, int currentHP)
    {
        if (bar == null)
            return;

        bar.SetName(pokemon.name);
        bar.SetHealthInstant(currentHP, pokemon.hp);
        bar.SetVisible(true);
    }

    private void UpdateHealthBars()
    {
        if (playerHealthBar != null && playerPokemon != null)
            playerHealthBar.SetHealth(playerCurrentHP, playerPokemon.hp);

        if (enemyHealthBar != null && enemyPokemon != null)
            enemyHealthBar.SetHealth(enemyCurrentHP, enemyPokemon.hp);
    }

    private void PlayTrigger(Animator animator, string trigger)
    {
        if (animator == null)
        {
            Debug.LogWarning($"No hay Animator para lanzar el trigger '{trigger}'.");
            return;
        }

        animator.SetTrigger(trigger);
    }

    private string GetAttackTrigger(int moveIndex)
    {
        return moveIndex == 0 ? TriggerAttack : TriggerAttack2;
    }

    private void ResetAnimator(Animator animator)
    {
        if (animator == null)
            return;

        animator.ResetTrigger(TriggerAttack);
        animator.ResetTrigger(TriggerAttack2);
        animator.ResetTrigger(TriggerDamage);
        animator.ResetTrigger(TriggerDeath);
        animator.Rebind();
    }


    // =========================================================
    // START BATTLE
    // =========================================================

    private void StartBattle()
    {
        Debug.Log("================================");
        Debug.Log("        ¡COMIENZA EL COMBATE!");
        Debug.Log("================================");

        Debug.Log(
            $"Jugador: {playerPokemon.name} | " +
            $"HP: {playerCurrentHP}/{playerPokemon.hp} | Tipo: {playerPokemon.type}"
        );

        Debug.Log(
            $"Rival: {enemyPokemon.name} | " +
            $"HP: {enemyCurrentHP}/{enemyPokemon.hp} | Tipo: {enemyPokemon.type}"
        );

        // El jugador empieza
        currentState = BattleState.PlayerTurn;

        Debug.Log($"Turno del jugador: {playerPokemon.name}");

        UpdateBattleUI();
    }


    // =========================================================
    // PLAYER ATTACK (llamado por los botones)
    // =========================================================

    public void PlayerAttack(int moveIndex)
    {
        if (currentState != BattleState.PlayerTurn)
        {
            Debug.LogWarning("No es el turno del jugador.");
            return;
        }

        if (playerPokemon == null || enemyPokemon == null)
        {
            Debug.LogWarning("Todavía no hay dos Pokémon en combate.");
            return;
        }

        if (playerPokemon.attacks == null || playerPokemon.attacks.Length == 0)
        {
            Debug.LogWarning("El Pokémon del jugador no tiene ataques.");
            return;
        }

        if (moveIndex < 0 || moveIndex >= playerPokemon.attacks.Length)
        {
            Debug.LogWarning("Índice de ataque inválido.");
            return;
        }

        // Bloquea los botones mientras dura la animación
        currentState = BattleState.Resolving;
        UpdateBattleUI();

        StartCoroutine(AttackRoutine(true, moveIndex));
    }


    // =========================================================
    // ATTACK ROUTINE (la usan jugador y enemigo)
    // =========================================================

    private IEnumerator AttackRoutine(bool playerIsAttacker, int moveIndex)
    {
        Pokemon attacker = playerIsAttacker ? playerPokemon : enemyPokemon;
        Pokemon defender = playerIsAttacker ? enemyPokemon : playerPokemon;

        Animator attackerAnimator = playerIsAttacker ? playerAnimator : enemyAnimator;
        Animator defenderAnimator = playerIsAttacker ? enemyAnimator : playerAnimator;

        Move move = attacker.attacks[moveIndex];

        Debug.Log($"{attacker.name} usa {move.name}!");

        PlayTrigger(attackerAnimator, GetAttackTrigger(moveIndex));

        yield return new WaitForSeconds(attackHitDelay);

        int damage = CalculateDamage(attacker, defender, move, true);

        if (playerIsAttacker)
            enemyCurrentHP = Mathf.Max(0, enemyCurrentHP - damage);
        else
            playerCurrentHP = Mathf.Max(0, playerCurrentHP - damage);

        int defenderHP = playerIsAttacker ? enemyCurrentHP : playerCurrentHP;

        Debug.Log($"{defender.name} recibe {damage} de daño.");
        Debug.Log($"HP de {defender.name}: {defenderHP}/{defender.hp}");

        UpdateHealthBars();
        UpdateBattleUI();

        if (defenderHP <= 0)
        {
            PlayTrigger(defenderAnimator, TriggerDeath);

            yield return new WaitForSeconds(deathDuration);

            PokemonCapture defenderCapture = playerIsAttacker ? enemyCapture : playerCapture;

            if (defenderCapture != null)
            {
                defenderCapture.Capture(defeatEffectDuration);

                yield return new WaitUntil(() =>
                    defenderCapture == null || defenderCapture.IsCaptured);
            }

            EndBattle(playerIsAttacker);
            yield break;
        }
        if (damage > 0)
            PlayTrigger(defenderAnimator, TriggerDamage);

        yield return new WaitForSeconds(hitReactionDuration);

        if (playerIsAttacker)
        {
            currentState = BattleState.EnemyTurn;
            UpdateBattleUI();

            yield return new WaitForSeconds(enemyThinkDelay);

            StartEnemyTurn();
        }
        else
        {
            currentState = BattleState.PlayerTurn;
            UpdateBattleUI();

            Debug.Log($"Turno del jugador: {playerPokemon.name}");
        }
    }


    // =========================================================
    // ENEMY TURN
    // =========================================================

    private void StartEnemyTurn()
    {
        if (currentState != BattleState.EnemyTurn)
            return;

        Debug.Log($"Turno del rival: {enemyPokemon.name}");

        if (enemyPokemon.attacks == null || enemyPokemon.attacks.Length == 0)
        {
            Debug.LogWarning("El rival no tiene ataques.");

            currentState = BattleState.PlayerTurn;
            UpdateBattleUI();
            return;
        }

        int selectedMoveIndex = ChooseBestEnemyMove();

        Debug.Log($"La IA ha elegido: {enemyPokemon.attacks[selectedMoveIndex].name}");

        currentState = BattleState.Resolving;
        UpdateBattleUI();

        StartCoroutine(AttackRoutine(false, selectedMoveIndex));
    }

    private int ChooseBestEnemyMove()
    {
        int bestMoveIndex = 0;
        int bestDamage = -1;

        for (int i = 0; i < enemyPokemon.attacks.Length; i++)
        {
            Move move = enemyPokemon.attacks[i];

            // Sin logs: es solo una estimación
            int estimatedDamage = CalculateDamage(enemyPokemon, playerPokemon, move, false);

            if (estimatedDamage > bestDamage)
            {
                bestDamage = estimatedDamage;
                bestMoveIndex = i;
            }
        }

        return bestMoveIndex;
    }


    // =========================================================
    // DAMAGE (jugador y enemigo comparten la misma fórmula)
    // =========================================================

    private int CalculateDamage(Pokemon attacker, Pokemon defender, Move move, bool log)
    {
        if (move == null || attacker == null || defender == null)
            return 0;

        int damage = move.damage;

        if (log)
            Debug.Log($"Daño base de {move.name}: {damage}");

        // Debilidad
        if (defender.weakness != null &&
            !string.IsNullOrEmpty(defender.weakness.type) &&
            string.Equals(attacker.type, defender.weakness.type, StringComparison.OrdinalIgnoreCase))
        {
            damage += defender.weakness.value;

            if (log)
                Debug.Log($"¡Es súper efectivo! +{defender.weakness.value} de daño.");
        }

        // Resistencia
        if (defender.resistance != null &&
            !string.IsNullOrEmpty(defender.resistance.type) &&
            string.Equals(attacker.type, defender.resistance.type, StringComparison.OrdinalIgnoreCase))
        {
            damage -= defender.resistance.value;

            if (log)
                Debug.Log($"El ataque es poco efectivo. -{defender.resistance.value} de daño.");
        }

        return Mathf.Max(0, damage);
    }


    // =========================================================
    // UI
    // =========================================================

    private void UpdateBattleUI()
    {
        // Jugador
        if (playerNameText != null)
            playerNameText.text = playerPokemon != null ? playerPokemon.name : "";

        if (playerHPText != null)
            playerHPText.text = playerPokemon != null
                ? $"HP: {playerCurrentHP}/{playerPokemon.hp}"
                : "";

        // Enemigo
        if (enemyNameText != null)
            enemyNameText.text = enemyPokemon != null ? enemyPokemon.name : "";

        if (enemyHPText != null)
            enemyHPText.text = enemyPokemon != null
                ? $"HP: {enemyCurrentHP}/{enemyPokemon.hp}"
                : "";

        // Botones de ataque con el nombre de cada movimiento
        SetupAttackButton(attackButton1, attackButton1TMP, 0);
        SetupAttackButton(attackButton2, attackButton2TMP, 1);

        // Botón de reinicio: solo cuando acaba el combate
        if (resetButton != null)
            resetButton.gameObject.SetActive(currentState == BattleState.BattleOver);
    }

    private void SetupAttackButton(Button button, TMP_Text tmpLabel, int attackIndex)
    {
        if (button == null)
            return;

        bool hasAttack =
            playerPokemon != null &&
            playerPokemon.attacks != null &&
            attackIndex < playerPokemon.attacks.Length;

        button.gameObject.SetActive(hasAttack);

        // Si el texto NO es hijo del botón, hay que ocultarlo a mano
        if (tmpLabel != null)
            tmpLabel.gameObject.SetActive(hasAttack);

        if (!hasAttack)
            return;

        SetButtonLabel(button, tmpLabel, playerPokemon.attacks[attackIndex].name);

        button.interactable =
            currentState == BattleState.PlayerTurn &&
            enemyPokemon != null;
    }

    // Usa el TMP asignado en el Inspector; si no hay, busca uno dentro del botón
    private void SetButtonLabel(Button button, TMP_Text tmpLabel, string label)
    {
        if (tmpLabel == null)
            tmpLabel = button.GetComponentInChildren<TMP_Text>(true);

        if (tmpLabel != null)
            tmpLabel.text = label;
        else
            Debug.LogWarning($"No se ha encontrado ningún TMP_Text para el botón '{button.name}'.");
    }


    // =========================================================
    // END / RESET
    // =========================================================

    private void EndBattle(bool playerWon)
    {
        currentState = BattleState.BattleOver;

        UpdateBattleUI();

        Debug.Log("================================");
        Debug.Log(playerWon ? "          ¡VICTORIA!" : "           DERROTA");
        Debug.Log("================================");
    }

    public void ResetBattle()
    {
        StopAllCoroutines();

        // Limpia los animators antes de que ResetCards desactive los objetos
        ResetAnimator(playerAnimator);
        ResetAnimator(enemyAnimator);

        playerPokemon = null;
        enemyPokemon = null;
        playerCurrentHP = 0;
        enemyCurrentHP = 0;
        playerHealthBar = null;
        enemyHealthBar = null;
        playerAnimator = null;
        enemyAnimator = null;
        playerCapture = null;
        enemyCapture = null;

        currentState = BattleState.WaitingForPlayer;

        if (trackingManager != null)
            trackingManager.ResetCards();

        UpdateBattleUI();

        Debug.Log("Combate reiniciado. Escanea la carta del jugador.");
    }


    // =========================================================
    // GETTERS
    // =========================================================

    public Pokemon GetPlayerPokemon() { return playerPokemon; }
    public Pokemon GetEnemyPokemon() { return enemyPokemon; }
    public int GetPlayerHP() { return playerCurrentHP; }
    public int GetEnemyHP() { return enemyCurrentHP; }
    public BattleState GetCurrentState() { return currentState; }
}


// =============================================================
// JSON DATA CLASSES
// =============================================================

[Serializable]
public class PokemonDatabase
{
    public Pokemon[] pokemon;
}

[Serializable]
public class Pokemon
{
    public string name;
    public int hp;
    public string type;
    public string stage;

    public Move[] attacks;

    public Weakness weakness;
    public Resistance resistance;
}

[Serializable]
public class Move
{
    public string name;
    public int damage;
}

[Serializable]
public class Weakness
{
    public string type;
    public int value;
}

[Serializable]
public class Resistance
{
    public string type;
    public int value;
}