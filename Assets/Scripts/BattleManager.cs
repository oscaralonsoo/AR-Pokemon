using System;
using UnityEditor;
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
        BattleOver
    }

    [Header("Battle State")]
    [SerializeField] private BattleState currentState;

    [Header("UI")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text enemyNameText;

    [SerializeField] private Text playerHPText;
    [SerializeField] private Text enemyHPText;

    [SerializeField] private Button attackButton1;
    [SerializeField] private Button attackButton2;

    [SerializeField] private Text attackButton1Text;
    [SerializeField] private Text attackButton2Text;

    private PokemonDatabase database;

    private Pokemon playerPokemon;
    private Pokemon enemyPokemon;

    private int playerCurrentHP;
    private int enemyCurrentHP;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        LoadDatabase();

        currentState = BattleState.WaitingForPlayer;

        UpdateBattleUI();
        UpdateAttackButtons();
    }


    // =========================================================
    // LOAD DATABASE
    // =========================================================

    private void LoadDatabase()
    {
#if UNITY_EDITOR

        TextAsset jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Assets/Pokemon/pokemonsdb.json"
        );

        if (jsonFile == null)
        {
            Debug.LogError(
                "No se ha encontrado el JSON en Assets/Assets/Pokemon/pokemonsdb.json"
            );

            return;
        }

        database = JsonUtility.FromJson<PokemonDatabase>(
            jsonFile.text
        );

        if (database == null || database.pokemon == null)
        {
            Debug.LogError(
                "No se ha podido cargar la base de datos de Pokémon."
            );

            return;
        }

        Debug.Log(
            $"Base de datos cargada correctamente: " +
            $"{database.pokemon.Length} Pokémon."
        );

#else

        Debug.LogError(
            "La carga mediante AssetDatabase solo funciona dentro del Editor de Unity."
        );

#endif
    }


    // =========================================================
    // REGISTER CARD
    // =========================================================

    public void RegisterCard(string pokemonName)
    {
        if (string.IsNullOrEmpty(pokemonName))
            return;

        if (database == null)
        {
            Debug.LogError(
                "La base de datos todavía no está cargada."
            );

            return;
        }

        switch (currentState)
        {
            case BattleState.WaitingForPlayer:

                RegisterPlayerPokemon(pokemonName);

                break;


            case BattleState.WaitingForEnemy:

                RegisterEnemyPokemon(pokemonName);

                break;


            default:

                Debug.LogWarning(
                    $"No se puede registrar una carta durante el estado: {currentState}"
                );

                break;
        }
    }


    // =========================================================
    // REGISTER PLAYER
    // =========================================================

    private void RegisterPlayerPokemon(string pokemonName)
    {
        playerPokemon = FindPokemon(pokemonName);

        if (playerPokemon == null)
        {
            Debug.LogError(
                $"No se ha encontrado '{pokemonName}' en el JSON."
            );

            return;
        }

        playerCurrentHP = playerPokemon.hp;

        Debug.Log(
            $"Jugador: {playerPokemon.name} | " +
            $"HP: {playerCurrentHP} | " +
            $"Tipo: {playerPokemon.type}"
        );

        currentState = BattleState.WaitingForEnemy;

        Debug.Log(
            "Esperando a que se escanee la carta del rival..."
        );

        UpdateBattleUI();
    }


    // =========================================================
    // REGISTER ENEMY
    // =========================================================

    private void RegisterEnemyPokemon(string pokemonName)
    {
        enemyPokemon = FindPokemon(pokemonName);

        if (enemyPokemon == null)
        {
            Debug.LogError(
                $"No se ha encontrado '{pokemonName}' en el JSON."
            );

            return;
        }

        enemyCurrentHP = enemyPokemon.hp;

        Debug.Log(
            $"Rival: {enemyPokemon.name} | " +
            $"HP: {enemyCurrentHP} | " +
            $"Tipo: {enemyPokemon.type}"
        );

        StartBattle();
    }


    // =========================================================
    // FIND POKEMON
    // =========================================================

    private Pokemon FindPokemon(string pokemonName)
    {
        foreach (Pokemon pokemon in database.pokemon)
        {
            if (string.Equals(
                pokemon.name,
                pokemonName,
                StringComparison.OrdinalIgnoreCase))
            {
                return pokemon;
            }
        }

        return null;
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
            $"HP: {playerCurrentHP}/{playerPokemon.hp} | " +
            $"Tipo: {playerPokemon.type}"
        );

        Debug.Log(
            $"Rival: {enemyPokemon.name} | " +
            $"HP: {enemyCurrentHP}/{enemyPokemon.hp} | " +
            $"Tipo: {enemyPokemon.type}"
        );

        // El jugador empieza
        currentState = BattleState.PlayerTurn;

        Debug.Log(
            $"Turno del jugador: {playerPokemon.name}"
        );

        // Mostrar ataques del jugador
        if (playerPokemon.attacks != null)
        {
            Debug.Log("Ataques disponibles:");

            for (int i = 0; i < playerPokemon.attacks.Length; i++)
            {
                Move move = playerPokemon.attacks[i];

                Debug.Log(
                    $"[{i}] {move.name} | " +
                    $"Daño: {move.damage}"
                );
            }
        }

        UpdateBattleUI();
        UpdateAttackButtons();
    }


    // =========================================================
    // PLAYER ATTACK
    // =========================================================

    public void PlayerAttack(int moveIndex)
    {
        if (currentState != BattleState.PlayerTurn)
        {
            Debug.LogWarning(
                "No es el turno del jugador."
            );

            return;
        }

        if (playerPokemon == null ||
            enemyPokemon == null)
        {
            Debug.LogWarning(
                "Todavía no hay dos Pokémon en combate."
            );

            return;
        }

        if (playerPokemon.attacks == null ||
            playerPokemon.attacks.Length == 0)
        {
            Debug.LogWarning(
                "El Pokémon del jugador no tiene ataques."
            );

            return;
        }

        if (moveIndex < 0 ||
            moveIndex >= playerPokemon.attacks.Length)
        {
            Debug.LogWarning(
                "Índice de ataque inválido."
            );

            return;
        }

        Move move = playerPokemon.attacks[moveIndex];

        Debug.Log(
            $"{playerPokemon.name} usa {move.name}!"
        );

        int damage = CalculatePlayerDamage(move);

        enemyCurrentHP -= damage;

        if (enemyCurrentHP < 0)
            enemyCurrentHP = 0;

        Debug.Log(
            $"{enemyPokemon.name} recibe {damage} de daño."
        );

        Debug.Log(
            $"HP de {enemyPokemon.name}: " +
            $"{enemyCurrentHP}/{enemyPokemon.hp}"
        );

        UpdateBattleUI();

        // El enemigo ha sido derrotado
        if (enemyCurrentHP <= 0)
        {
            EndBattle(true);
            return;
        }

        // Empieza el turno enemigo
        currentState = BattleState.EnemyTurn;

        UpdateAttackButtons();

        EnemyTurn();
    }


    // =========================================================
    // PLAYER DAMAGE
    // =========================================================

    private int CalculatePlayerDamage(Move move)
    {
        if (move == null)
            return 0;

        int damage = move.damage;

        Debug.Log(
            $"Daño base de {move.name}: {damage}"
        );

        // -----------------------------------------------------
        // DEBILIDAD
        // -----------------------------------------------------

        if (enemyPokemon.weakness != null)
        {
            if (string.Equals(
                playerPokemon.type,
                enemyPokemon.weakness.type,
                StringComparison.OrdinalIgnoreCase))
            {
                damage += enemyPokemon.weakness.value;

                Debug.Log(
                    $"¡Es súper efectivo! " +
                    $"+{enemyPokemon.weakness.value} de daño."
                );
            }
        }


        // -----------------------------------------------------
        // RESISTENCIA
        // -----------------------------------------------------

        if (enemyPokemon.resistance != null)
        {
            if (string.Equals(
                playerPokemon.type,
                enemyPokemon.resistance.type,
                StringComparison.OrdinalIgnoreCase))
            {
                damage -= enemyPokemon.resistance.value;

                Debug.Log(
                    $"El ataque es poco efectivo. " +
                    $"-{enemyPokemon.resistance.value} de daño."
                );
            }
        }


        if (damage < 0)
            damage = 0;

        return damage;
    }


    // =========================================================
    // ENEMY TURN
    // =========================================================

    private void EnemyTurn()
    {
        if (currentState != BattleState.EnemyTurn)
            return;

        if (enemyCurrentHP <= 0)
            return;

        Debug.Log(
            $"Turno del rival: {enemyPokemon.name}"
        );

        if (enemyPokemon.attacks == null ||
            enemyPokemon.attacks.Length == 0)
        {
            Debug.LogWarning(
                "El rival no tiene ataques."
            );

            currentState = BattleState.PlayerTurn;

            UpdateAttackButtons();

            return;
        }

        // La IA elige el mejor ataque
        int selectedMoveIndex = ChooseBestEnemyMove();

        Move selectedMove =
            enemyPokemon.attacks[selectedMoveIndex];

        Debug.Log(
            $"La IA ha elegido: {selectedMove.name}"
        );

        // Ejecutar ataque
        ExecuteEnemyAttack(selectedMove);
    }


    // =========================================================
    // ENEMY AI
    // =========================================================

    private int ChooseBestEnemyMove()
    {
        int bestMoveIndex = 0;
        int bestDamage = -1;

        for (int i = 0;
             i < enemyPokemon.attacks.Length;
             i++)
        {
            Move move = enemyPokemon.attacks[i];

            int estimatedDamage =
                CalculateEnemyDamage(move);

            Debug.Log(
                $"IA analiza {move.name}: " +
                $"daño estimado = {estimatedDamage}"
            );

            if (estimatedDamage > bestDamage)
            {
                bestDamage = estimatedDamage;
                bestMoveIndex = i;
            }
        }

        return bestMoveIndex;
    }


    // =========================================================
    // EXECUTE ENEMY ATTACK
    // =========================================================

    private void ExecuteEnemyAttack(Move move)
    {
        Debug.Log(
            $"{enemyPokemon.name} usa {move.name}!"
        );

        int damage = CalculateEnemyDamage(move);

        playerCurrentHP -= damage;

        if (playerCurrentHP < 0)
            playerCurrentHP = 0;

        Debug.Log(
            $"{playerPokemon.name} recibe {damage} de daño."
        );

        Debug.Log(
            $"HP de {playerPokemon.name}: " +
            $"{playerCurrentHP}/{playerPokemon.hp}"
        );

        UpdateBattleUI();

        // El jugador ha sido derrotado
        if (playerCurrentHP <= 0)
        {
            EndBattle(false);
            return;
        }

        // Volvemos al turno del jugador
        currentState = BattleState.PlayerTurn;

        UpdateAttackButtons();

        Debug.Log(
            $"Turno del jugador: {playerPokemon.name}"
        );
    }


    // =========================================================
    // ENEMY DAMAGE
    // =========================================================

    private int CalculateEnemyDamage(Move move)
    {
        if (move == null)
            return 0;

        int damage = move.damage;

        Debug.Log(
            $"Daño base de {move.name}: {damage}"
        );


        // -----------------------------------------------------
        // DEBILIDAD DEL JUGADOR
        // -----------------------------------------------------

        if (playerPokemon.weakness != null)
        {
            if (string.Equals(
                enemyPokemon.type,
                playerPokemon.weakness.type,
                StringComparison.OrdinalIgnoreCase))
            {
                damage += playerPokemon.weakness.value;

                Debug.Log(
                    $"¡Es súper efectivo! " +
                    $"+{playerPokemon.weakness.value} de daño."
                );
            }
        }


        // -----------------------------------------------------
        // RESISTENCIA DEL JUGADOR
        // -----------------------------------------------------

        if (playerPokemon.resistance != null)
        {
            if (string.Equals(
                enemyPokemon.type,
                playerPokemon.resistance.type,
                StringComparison.OrdinalIgnoreCase))
            {
                damage -= playerPokemon.resistance.value;

                Debug.Log(
                    $"El ataque es poco efectivo. " +
                    $"-{playerPokemon.resistance.value} de daño."
                );
            }
        }


        if (damage < 0)
            damage = 0;

        return damage;
    }


    // =========================================================
    // UPDATE UI
    // =========================================================

    private void UpdateBattleUI()
    {
        // -----------------------------------------------------
        // PLAYER
        // -----------------------------------------------------

        if (playerPokemon != null)
        {
            if (playerNameText != null)
            {
                playerNameText.text =
                    playerPokemon.name;
            }

            if (playerHPText != null)
            {
                playerHPText.text =
                    $"HP: {playerCurrentHP}/{playerPokemon.hp}";
            }
        }


        // -----------------------------------------------------
        // ENEMY
        // -----------------------------------------------------

        if (enemyPokemon != null)
        {
            if (enemyNameText != null)
            {
                enemyNameText.text =
                    enemyPokemon.name;
            }

            if (enemyHPText != null)
            {
                enemyHPText.text =
                    $"HP: {enemyCurrentHP}/{enemyPokemon.hp}";
            }
        }


        // -----------------------------------------------------
        // ATTACK 1
        // -----------------------------------------------------

        if (playerPokemon != null &&
            playerPokemon.attacks != null)
        {
            if (playerPokemon.attacks.Length > 0)
            {
                if (attackButton1 != null)
                {
                    attackButton1.gameObject.SetActive(true);
                }

                if (attackButton1Text != null)
                {
                    attackButton1Text.text =
                        playerPokemon.attacks[0].name;
                }
            }
            else
            {
                if (attackButton1 != null)
                {
                    attackButton1.gameObject.SetActive(false);
                }
            }


            // -------------------------------------------------
            // ATTACK 2
            // -------------------------------------------------

            if (playerPokemon.attacks.Length > 1)
            {
                if (attackButton2 != null)
                {
                    attackButton2.gameObject.SetActive(true);
                }

                if (attackButton2Text != null)
                {
                    attackButton2Text.text =
                        playerPokemon.attacks[1].name;
                }
            }
            else
            {
                if (attackButton2 != null)
                {
                    attackButton2.gameObject.SetActive(false);
                }
            }
        }

        UpdateAttackButtons();
    }


    // =========================================================
    // ATTACK BUTTONS
    // =========================================================

    private void UpdateAttackButtons()
    {
        bool canAttack =
            currentState == BattleState.PlayerTurn &&
            playerPokemon != null &&
            enemyPokemon != null;

        if (attackButton1 != null)
        {
            attackButton1.interactable =
                canAttack;
        }

        if (attackButton2 != null)
        {
            attackButton2.interactable =
                canAttack;
        }
    }


    // =========================================================
    // END BATTLE
    // =========================================================

    private void EndBattle(bool playerWon)
    {
        currentState = BattleState.BattleOver;

        UpdateAttackButtons();

        Debug.Log("================================");

        if (playerWon)
        {
            Debug.Log("          ¡VICTORIA!");
        }
        else
        {
            Debug.Log("           DERROTA");
        }

        Debug.Log("================================");
    }


    // =========================================================
    // GETTERS
    // =========================================================

    public Pokemon GetPlayerPokemon()
    {
        return playerPokemon;
    }


    public Pokemon GetEnemyPokemon()
    {
        return enemyPokemon;
    }


    public int GetPlayerHP()
    {
        return playerCurrentHP;
    }


    public int GetEnemyHP()
    {
        return enemyCurrentHP;
    }


    public BattleState GetCurrentState()
    {
        return currentState;
    }
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