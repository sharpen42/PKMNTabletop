using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class BattleManager : MonoBehaviour
{
    public enum BattlePhase
    {
        Idle, StartBattle, ChooseTeam, ChoosePokemon, ChooseMove, /* PlayerTurn, EnemyTurn, */ ResolveTurn, EndTurn, EndBattle,
        
    }

    public static BattleManager Instance = null;
    [SerializeField] private BattleUI battleUI;
    [SerializeField] private float stdPhaseDuration = 3.0f;

    public BattlePhase currentPhase { get; private set; } = BattlePhase.Idle;

    public Player[] players = new Player[2];
    public int[] onFieldPkmn { get; private set; } = new int[2] { -1, -1 };
    [SerializeField] private ActionType[] chosenMoves = new ActionType[2] { ActionType._err, ActionType._err };

    private bool[] playersReady = new bool[] { false, false };

    public bool onlineBattle { get; private set; } = false;
    public int localPlayerIndex = 0; // 0 per host, 1 per guest — da impostare alla connessione
    public int otherPlayerIndex { get { return 1 - localPlayerIndex; } } 

    public event Action<BattlePhase> OnPhaseChange;
    public event Action<BattleLog> OnBattleLogEntry;
    public event Action<int, int> OnDiceRoll;

    public UnityEvent OnBattleStart;
    public UnityEvent OnBattleEnd;
    public UnityEvent OnFirstTurn;
    public UnityEvent OnTurnStart;
    public UnityEvent OnTurnEnd;
    public UnityEvent OnDamageCalc;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        OnPhaseChange += HandlePhase;
        /* if onlineBattle is true
         * Subscribe PlayersReady() a OnlineBattleManager.OnAllPlayersReady
         */
    }

    private void Start()
    {
        SetPhase(BattlePhase.Idle);
    }

    public void StartBattle()
    {
        OnBattleStart?.Invoke();
        SetPhase(BattlePhase.StartBattle);
    }

    public void LogEntry(string message, Color color)
    {
        //Debug.Log(message); // mantieni anche il debug
        OnBattleLogEntry?.Invoke(new BattleLog { message = message, color = color });
    }

    /*
     * 1. Inizia Battaglia // StartBattle
     * 2. I giocatori scelgono il Pokémon da mettere in campo // ChoosePokemon
     *      2a. mostra schermata di scelta Pokémon
     *      2b. attendi selezione player 1
     *      2c. attendi selezione player 2
     *      2d. scopri i Pokémon scelti
     * 3. Inizio turno
     * 4. I giocatori scelgono la mossa (scambio, strumento, attacco1, attacco2) // ChooseMove
     * 5. Risoluzione del turno. // ResolveTurn
     *      5a. Se un giocatore o entrambi hanno scelto lo scambio, il Pokémon in campo di ciascuno viene sostituito da quello nel team
     *      5b. Se un giocatore o entrambi hanno scelto lo strumento, ciascuno deve scegliere lo strumento da utilizzare
     *      5c. Se un giocatore o entrambi hanno scelto l'attacco 1 o 2, ciascuno deve tirare un d6
     *      5d. In base ai risultati dei d6, vengono applicati eventuali effetti secondari
     *      5e. In base ai risultati dei d6, viene applicato il danno sui Pokémon che hanno ricevuto un attacco
     * 6. Fine del turno // EndTurn
     *      10a. Se un Pokémon va K0, viene sostituito.
     *      10b. Se non può essere sostituito, la lotta finisce.
     *      10c. Altrimenti vai a 3.
     * 7. Vince il giocatore che ha almeno un Pokémon con HP > 0
     * 
     */

    private void HandlePhase(BattlePhase phase)
    {
        switch(phase)
        {
            case BattlePhase.Idle: default: break;
            case BattlePhase.StartBattle:
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].playerIndex = i;
                    players[i].OnPlayerReady += SetPlayerReady;
                    players[i].OnTeamChosen += (_, _, _) => SetTeam();
                    players[i].OnPokemonChosen += SetPokemon;
                    players[i].OnMoveChosen += SetMove;
                    playersReady[i] = false;
                }
                HideContainers();
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].StartBattle();
                }

                break;
            case BattlePhase.ChooseTeam:
                HideContainers();
                for (int i = 0; i < players.Length; i++)
                {
                    for (int j = 0; j < players[i].team.Length; j++)
                    {
                        players[i].team[j] = null;
                    }
                }
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].ChooseTeam();
                }
                break;
            case BattlePhase.ChoosePokemon:
                for (int i = 0; i < players.Length; i++)
                {
                    for (int j = 0; j < players[i].team.Length; j++)
                    {
                        onFieldPkmn[i] = -1;
                    }
                }
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].ChoosePokemon();
                }
                break;
            case BattlePhase.ChooseMove:
                OnTurnStart?.Invoke();
                SetContainers();
                int seed = BattleRng.GenerateSeed();
                if (onlineBattle)
                {
                    OnlineBattleManager.Instance.BroadcastTurnSeed(seed);
                } else
                {
                    BattleRng.SetSeed(seed);
                }

                for(int i = 0; i < chosenMoves.Length; i++)
                    chosenMoves[i] = ActionType._err;
                // reset scelte

                for (int i = 0; i < players.Length; i++)
                {
                    Pokemon onField = players[i].team[onFieldPkmn[i]];
                    if (onField.skipTurn)
                    {
                        onField.ConsumeSkipTurn();
                        LogEntry($"{onField.name} bloccato, salta il turno.", Color.yellow);
                        players[i].InvokeOnMoveChosen(ActionType.Skip); // bypassa la scelta
                    }
                    else
                    {
                        players[i].ChooseMove();
                    }
                }
                break;
            case BattlePhase.ResolveTurn:
                // 1. Gli scambi vanno sempre prima degli strumenti
                for (int i = 0; i < players.Length; i++)
                {
                    if (chosenMoves[i] == ActionType.Swap)
                        SwapPokemon(i);
                }

                // 1. Gli strumenti vanno sempre prima degli attacchi
                for (int i = 0; i < players.Length; i++)
                {
                    if (chosenMoves[i] == ActionType.Item)
                        UseItem(i);
                }

                // 2. Costruisci la coda a priorità per gli attacchi
                List<int> attackers = new List<int>();
                for (int i = 0; i < players.Length; i++)
                {
                    if (chosenMoves[i] == ActionType.NormalMove || chosenMoves[i] == ActionType.SpecialMove)
                        attackers.Add(i);
                }

                // 3. Tira i d6 per tutti gli attaccanti
                int[] rolls = new int[players.Length];
                for (int i = 0; i < attackers.Count; i++)
                {
                    rolls[attackers[i]] = rollD(6);
                    LogEntry($"Player {players[attackers[i]].playerName} rolls D6: {rolls[attackers[i]]}", Color.white);
                    OnDiceRoll?.Invoke(attackers[i], rolls[attackers[i]]);
                }

                // 4. Ordina per priorità: SPE > SPE+D6 > 50/50
                attackers.Sort((a, b) =>
                {
                    Pokemon pkA = players[a].team[onFieldPkmn[a]];
                    Pokemon pkB = players[b].team[onFieldPkmn[b]];

                    if (pkA.currentSpe != pkB.currentSpe)
                        return pkB.currentSpe.CompareTo(pkA.currentSpe); // SPE più alta prima

                    int scoreA = pkA.currentSpe + rolls[a];
                    int scoreB = pkB.currentSpe + rolls[b];

                    if (scoreA != scoreB)
                        return scoreB.CompareTo(scoreA); // SPE+D6 più alto prima

                    int res = rollD(2); // 50/50
                    LogEntry($"Player {players[a].playerName} {((res == 1) ? "perde" : "vince")} il 50/50", Color.gray);
                    OnDiceRoll?.Invoke(-1, res);
                    return (res == 1) ? -1 : 1; // 50/50
                });

                // 5. Risolvi gli attacchi in ordine
                foreach (int i in attackers)
                {
                    ResolveTurn(i, rolls[i]);
                    SetContainers();
                }

                SetPhase(BattlePhase.EndTurn);
                break;
            case BattlePhase.EndTurn:
                // 6. Controlla KO e avanza
                CheckPostTurn();
                OnTurnEnd?.Invoke();
                break;
            case BattlePhase.EndBattle:
                SetContainers();
                LogEntry("Battaglia terminata!", Color.green);
                for(int i = 0; i < players.Length; i++)
                {
                    players[i].EndBattle();
                } 
                OnBattleEnd?.Invoke();
                break;
        }
    }

    private bool CanMove(int i, int roll)
    {
        Pokemon attacker = players[i].team[onFieldPkmn[i]];

        // controlla sonno — il roll e' lo stesso usato per colpire
        if (!attacker.TryWakeUp(roll))
        {
            LogEntry($"{attacker.name} addormentato, non attacca.", Color.yellow);
            return false;
        }

        if (attacker.currentHp <= 0)
        {
            LogEntry($"{attacker.name} KO, non può attaccare.", Color.red);
            return false;
        }

        return true;
    }

    private int rollD(int d)
    {
        //return UnityEngine.Random.Range(1, d + 1);
        return BattleRng.Roll(1, d + 1);
    }

    private void ResolveTurn(int playerIndex, int roll)
    {
        if(CanMove(playerIndex, roll))
        {
            // se il Pokémon non può muovere (es. e' addormentato), ignora la sua azione
            switch (chosenMoves[playerIndex])
            {
                case ActionType.NormalMove: NormalMove(playerIndex, roll); break;
                case ActionType.SpecialMove: SpecialMove(playerIndex, roll); break;
                case ActionType.Skip: LogEntry($"Player {players[playerIndex].playerName} salta il turno.", Color.yellow); break;
                default: break;
            }
        }
    }

    private void UseItem(int playerIndex)
    {
        // da implementare
        LogEntry($"Player {players[playerIndex].name} used item [NOT IMPLEMENTED]", Color.magenta);
    }

    private void SwapPokemon(int playerIndex)
    {
        int currentField = onFieldPkmn[playerIndex];
        int inTeam = 1 - currentField; // l'altro pokemon

        if (players[playerIndex].team[inTeam] == null 
            || players[playerIndex].team[inTeam].currentHp <= 0) return;

        players[playerIndex].team[currentField].ResetModifiers(); // reset modificatori prima di uscire
        onFieldPkmn[playerIndex] = inTeam;
        SetContainers();
        LogEntry($"Player {players[playerIndex].playerName} ha scambiato con {players[playerIndex].team[inTeam].name}", Color.cyan);
    }

    private void NormalMove(int playerIndex, int roll)
    {
        int opponentIndex = 1 - playerIndex;
        Pokemon attacker = players[playerIndex].team[onFieldPkmn[playerIndex]];
        Pokemon defender = players[opponentIndex].team[onFieldPkmn[opponentIndex]];

        bool hits = (attacker.currentSpe + roll) > defender.currentDef;
        LogEntry($"{attacker.name} attacco normale: SPE({attacker.currentSpe}) + D6({roll}) vs DEF({defender.currentDef}) → {(hits ? "colpisce" : "manca")}", hits ? Color.green : Color.red);
        if (!hits) return;

        int damage = attacker.RegularMoveDamage(defender);
        defender.ApplyDamage(damage);
        LogEntry($"{attacker.name} infligge {damage} danni a {defender.name} (HP: {defender.currentHp})", Color.red);
    }

    private void SpecialMove(int playerIndex, int roll)
    {
        int opponentIndex = 1 - playerIndex;
        Pokemon attacker = players[playerIndex].team[onFieldPkmn[playerIndex]];
        Pokemon defender = players[opponentIndex].team[onFieldPkmn[opponentIndex]];

        bool hits = (attacker.currentSpe + roll) > defender.currentDef;
        LogEntry($"{attacker.name} mossa speciale: SPE({attacker.currentSpe}) + D6({roll}) vs DEF({defender.currentDef}) → {(hits ? "colpisce" : "manca")}", hits ? Color.green : Color.red);
        if (!hits) return;

        // applica SupereffOnGround prima del danno se l'effetto e' TypeEffModifier
        if (attacker.CurrentSpecialMoveEffect.category == Effect.Category.TypeEffModifier)
        {
            attacker.CurrentSpecialMoveEffect.Apply(attacker, defender);

            LogEntry($"Effetto attivato: {attacker.CurrentSpecialMoveEffect.description}", Color.magenta);
        }

        int damage = attacker.SpecialMoveDamage(defender);
        defender.ApplyDamage(damage);
        LogEntry($"{attacker.name} infligge {damage} danni con {attacker.CurrentSpecialMoveName} a {defender.name} (HP: {defender.currentHp})", Color.red);

        // effetto secondario: si attiva se roll >= soglia D6 della mossa
        if ((attacker.CurrentD6 == 0 || roll >= attacker.CurrentD6)
            && attacker.CurrentSpecialMoveEffect.category != Effect.Category.TypeEffModifier
            && attacker.CurrentSpecialMoveEffect.category != Effect.Category.NoEff)
        {
            attacker.CurrentSpecialMoveEffect.Apply(attacker, defender);

            if(attacker.CurrentSpecialMoveEffect.category == Effect.Category.SkipTurn)
            {
                LogEntry($"{defender.name} salta il turno successivo!", Color.yellow);
            }

            LogEntry($"Effetto attivato: {attacker.CurrentSpecialMoveEffect.description}", Color.magenta);
        }
    }

    public static void InvokeD6Roll(int playerIndex, int roll)
    {
        if(Instance != null)
            Instance.OnDiceRoll?.Invoke(playerIndex, roll);
    }

    private void CheckPostTurn()
    {
        // tick veleno
        for (int i = 0; i < players.Length; i++)
        {
            Pokemon pkmn = players[i].team[onFieldPkmn[i]];
            pkmn.PoisonTick();
            if(pkmn.status == Effect.Status.Poisoned)
                LogEntry($"{pkmn.name} subisce -1 danni da veleno (HP: {pkmn.currentHp})", Color.green);
        }

        bool battleOver = false;

        for (int i = 0; i < players.Length; i++)
        {
            int opponentIndex = 1 - i;
            Pokemon onField = players[i].team[onFieldPkmn[i]];

            if (onField.currentHp <= 0)
            {
                int inTeam = 1 - onFieldPkmn[i];
                Pokemon reserve = players[i].team[inTeam];

                if (reserve == null || reserve.currentHp <= 0)
                {
                    battleOver = true;
                    LogEntry($"Player {players[opponentIndex].playerName} vince!", Color.green);
                }
                else
                {
                    SwapPokemon(i);
                    LogEntry($"Player {players[i].playerName}: {onField.name} KO, entra {reserve.name}", Color.yellow);
                }
            }
        }

        if (battleOver)
            SetPhase(BattlePhase.EndBattle);
        else
            SetPhase(BattlePhase.ChooseMove);
    }

    private void SetPlayerReady(int playerIndex)
    {
        playersReady[playerIndex] = true;
        if(playersReady.All(p => p)) 
            SetPhase(BattlePhase.ChooseTeam);
    }

    private void SetTeam()
    {
        var teamsSet = true;
        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i].teamSet)
            {
                teamsSet = false;
                break;
            }
        }
        if(!teamsSet) return;
        SetPhase(BattlePhase.ChoosePokemon);
    }

    private void SetPokemon(int playerIndex, int pokemon)
    {
        onFieldPkmn[playerIndex] = pokemon;
        var pkmnSet = true;
        for (int i = 0; i < players.Length; i++)
        {
            if (onFieldPkmn[i] == -1)
            {
                pkmnSet = false;
                break;
            }
        }
        LogEntry($"Player {players[playerIndex].playerName} ha scelto {players[playerIndex].team[pokemon].name}", Color.cyan);
        if (!pkmnSet) return;

        OnFirstTurn?.Invoke();
        SetPhase(BattlePhase.ChooseMove);
    }

    private void SetMove(int playerIndex, ActionType action)
    {
        chosenMoves[playerIndex] = action;
        var movesSet = true;
        for (int i = 0; i < players.Length; i++)
        {
            if (chosenMoves[i] == ActionType._err)
            {
                movesSet = false;
                break;
            }
        }
        if (!movesSet) return;
        SetPhase(BattlePhase.ResolveTurn);
    }

    private void SetPhase(BattlePhase newPhase)
    {
        currentPhase = newPhase;
        StartCoroutine(HandlePhaseCoroutine(currentPhase));
    }

    private IEnumerator HandlePhaseCoroutine(BattlePhase phase)
    {
        yield return new WaitForSeconds(stdPhaseDuration);
        OnPhaseChange?.Invoke(phase);
    }

    private void HideContainers()
    {
        battleUI.HideContainers();
    }

    private void SetContainers() 
    {
        battleUI.SetContainers(
            GetPlayer(localPlayerIndex).team[onFieldPkmn[localPlayerIndex]],
            GetPlayer(otherPlayerIndex).team[onFieldPkmn[otherPlayerIndex]]
            );

        LogEntry($"Player {localPlayerIndex} pokemon {GetPlayer(localPlayerIndex).team[onFieldPkmn[localPlayerIndex]].cardName}", Color.gray);
        LogEntry($"Player {otherPlayerIndex} pokemon {GetPlayer(otherPlayerIndex).team[onFieldPkmn[otherPlayerIndex]].cardName}", Color.gray);
    }

    public Player GetLocalPlayer() => players[localPlayerIndex];
    public Player GetPlayer(int index) => players[index];

    public void SetOnline(bool isOnline)
    {
        onlineBattle = isOnline;
    }

    public void SetLocal(int localIndex)
    {
        localPlayerIndex = localIndex;
    }
}

public enum ActionType { Item, Swap, NormalMove, SpecialMove, Skip, _err }

[Serializable]
public struct BattleAction
{
    public ActionType type;
    public int targetIndex; // per Swap o per Chansey
    public int d6roll;
}

public interface IBattleEntity
{
    int PlayerId { get; }

}

public struct BattleLog
{
    public string message;
    public Color color;
}
