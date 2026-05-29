using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class OnlineBattleManager : NetworkBehaviour
{
    public static OnlineBattleManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI[] localTeam = new TextMeshProUGUI[4];
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private HumanPlayer humanPlayer;
    [SerializeField] private RemotePlayer remotePlayer;

    [SerializeField] private GameObject OnlineUI;
    [SerializeField] private GameObject startButton;

    // evento che BattleManager ascolta per sapere quando entrambi i team sono pronti
    public event Action OnAllTeamsReceived;

    private bool[] validCards = new bool[] { false, false, false, false };

    // team ricevuto dal remoto — riempito da ReceiveRemoteTeamRpc
    private string[] receivedRemoteTeam = new string[4];
    private bool remoteTeamReceived = false;

    private bool remoteTeamChoiceReceived = false;
    private (int, int) receivedTeamChoice;

    private bool remotePokemonReceived = false;
    private int receivedPokemonChoice = -1;

    private bool remoteMoveReceived = false;
    private ActionType receivedMove = ActionType._err;

    private Action<int> onPlayerReady;
    private Action<int, int, int> onTeamChosen;
    private Action<int, int> onPokemonChosen;
    private Action<int, ActionType> onMoveChosen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void StartOnline(int localPlayerIndex)
    {
        BattleManager.Instance.SetOnline(true);
        BattleManager.Instance.SetLocal(localPlayerIndex);
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsHost ||
            NetworkManager.Singleton.IsServer ||
            NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }

        remoteTeamReceived = false;
        receivedRemoteTeam = new string[4];

        joinCodeText.text = "";
    }

    public async void StartHostButton()
    {
        Disconnect();

        try
        {
            await Unity.Services.Core.UnityServices.InitializeAsync();

            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                await Unity.Services.Authentication.AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            var allocation =
                await Unity.Services.Relay.RelayService.Instance
                    .CreateAllocationAsync(1);

            string joinCode =
                await Unity.Services.Relay.RelayService.Instance
                    .GetJoinCodeAsync(allocation.AllocationId);

            joinCodeText.text = "Password: " + joinCode;

            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            StartOnline(0);

            Debug.Log($"Host avviato. Join code: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore StartHost: {e}");
        }
    }

    public async void JoinGameButton()
    {
        Disconnect();

        try
        {
            await Unity.Services.Core.UnityServices.InitializeAsync();

            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
            {
                await Unity.Services.Authentication.AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            string joinCode = joinCodeInput.text.Trim().ToUpper();

            var joinAllocation =
                await Unity.Services.Relay.RelayService.Instance
                    .JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            StartOnline(1);

            Debug.Log($"Connesso all'host con codice: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore JoinGame: {e}");
        }
    }

    public void CheckCardName(int playercard)
    {
        bool res = false;

        if (localTeam[playercard] != null)
        {
            Debug.Log($"Cercando chiave: '{localTeam[playercard].text}'");
            res = PokedeckManager.CardExists(localTeam[playercard].text);
        }

        validCards[playercard] = res;
        localTeam[playercard].color = (res == true) ? Color.green : Color.red;

        Debug.Log($"Card name '{localTeam[playercard].text}' is {(res ? "valid" : "invalid")}");

        var allValid = validCards.All(p => p);
        if (allValid)
        {
            startButton.SetActive(allValid);
            Debug.Log("All card names are valid. Start button enabled.");
        }
    }

    public async void PlayerReady()
    {
        SendLocalTeam(humanPlayer.cardNames[0], humanPlayer.cardNames[1], humanPlayer.cardNames[2], humanPlayer.cardNames[3]);
        Debug.Log("Team locale inviato, ora aspetto il team remoto.");

        await ReceiveRemoteTeam();
        // questo codice viene eseguito dopo che il team remoto è stato ricevuto e copiato in remotePlayer.cardNames
        Debug.Log("Team remoto processato, avvio battaglia se online.");

        for (int i = 0; i < remotePlayer.cardNames.Length; i++)
        {
            remotePlayer.cardNames[i] = receivedRemoteTeam[i];
            Debug.Log($"Remote team card {i + 1}: {remotePlayer.cardNames[i]}");
        }
    }

    public async void ChooseTeam(int playerIndex, int teamChoice1, int teamChoice2)
    {
        SendLocalTeamChoice(playerIndex, teamChoice1, teamChoice2);

        await ReceiveRemoteTeamChoice();
    }

    public async void ChoosePokemon(int playerIndex, int pokemonChoice)
    {
        SendLocalPokemonChoice(playerIndex, pokemonChoice);

        await ReceiveRemotePokemonChoice();
    }

    public async void ChooseMove(int playerIndex, int moveChoice)
    {
        SendLocalMoveChoice(playerIndex, moveChoice);

        await ReceiveRemoteMoveChoice();
    }

    public void StartBattle()
    {
        if (!BattleManager.Instance.onlineBattle)
        {
            BattleManager.Instance.LogEntry("Choose Host or Guest!", Color.red);
            return;
        }

        int localIndex = BattleManager.Instance.localPlayerIndex;
        int remoteIndex = BattleManager.Instance.otherPlayerIndex;

        Debug.Log($"IsServer: {IsServer}, IsHost: {IsHost}, IsClient: {IsClient}");
        Debug.Log($"localPlayerIndex: {BattleManager.Instance.localPlayerIndex}");

        BattleManager.Instance.players[localIndex] = humanPlayer;
        BattleManager.Instance.players[remoteIndex] = remotePlayer;

        for(int i = 0; i < humanPlayer.cardNames.Length; i++)
        {
            humanPlayer.cardNames[i] = localTeam[i].text.Trim();
        }
        humanPlayer.playerName = "" + playerName.text;

        BattleManager.Instance.OnBattleEnd.AddListener(EndBattle);

        // deregistra eventuali listener precedenti
        humanPlayer.OnPlayerReady -= onPlayerReady;
        humanPlayer.OnTeamChosen -= onTeamChosen;
        humanPlayer.OnPokemonChosen -= onPokemonChosen;
        humanPlayer.OnMoveChosen -= onMoveChosen;

        // salva i delegate per poterli deregistrare dopo
        onPlayerReady = (_) => PlayerReady();
        onTeamChosen = (_, c1, c2) => ChooseTeam(localIndex, c1, c2);
        onPokemonChosen = (_, p) => ChoosePokemon(localIndex, p);
        onMoveChosen = (_, a) => ChooseMove(localIndex, (int)a);

        humanPlayer.OnPlayerReady += onPlayerReady;
        humanPlayer.OnTeamChosen += onTeamChosen;
        humanPlayer.OnPokemonChosen += onPokemonChosen;
        humanPlayer.OnMoveChosen += onMoveChosen;

        OnlineUI.SetActive(false);
        BattleManager.Instance.StartBattle();
    }

    private void EndBattle()
    {
        Disconnect();
        BattleManager.Instance.OnBattleEnd.RemoveListener(EndBattle);
    }

    // ── invio e ricezione team ────────────────────────────────────────────

    /// <summary>
    /// Chiamato da HumanPlayer.StartBattle() — invia i 4 nomi carte all'avversario.
    /// Controlla che i nomi siano validi, sostituisce gli sconosciuti con DittoA.
    /// </summary>
    public void SendLocalTeam(string p0, string p1, string p2, string p3)
    {
        string[] names = new string[] { p0, p1, p2, p3 };
        for (int i = 0; i < names.Length; i++)
        {
            if (!PokedeckManager.CardExists(names[i]))
            {
                Debug.LogWarning($"Pokemon '{names[i]}' non riconosciuto, sostituito con DittoA.");
                names[i] = "DittoA";
            }
        }

        if (IsServer)
            ReceiveRemoteTeamRpc(names[0], names[1], names[2], names[3]);
        else
            SendTeamToHostRpc(names[0], names[1], names[2], names[3]);
    }

    /// <summary>
    /// Guest → Host: invia il team del guest all'host.
    /// L'host lo salva, poi lo rimanda al guest come conferma e notifica BattleManager.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void SendTeamToHostRpc(string p0, string p1, string p2, string p3)
    {
        StoreRemoteTeam(p0, p1, p2, p3);
        // l'host invia il proprio team al guest (già validato in SendLocalTeam)
        // il guest lo riceverà via ReceiveRemoteTeamRpc
        ReceiveRemoteTeamRpc(
            humanPlayer.cardNames[0], humanPlayer.cardNames[1],
            humanPlayer.cardNames[2], humanPlayer.cardNames[3]
        );
    }

    /// <summary>
    /// Host → Guest: invia il team dell'host al guest.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void ReceiveRemoteTeamRpc(string p0, string p1, string p2, string p3)
    {
        StoreRemoteTeam(p0, p1, p2, p3);
    }

    private void StoreRemoteTeam(string p0, string p1, string p2, string p3)
    {
        receivedRemoteTeam = new string[] { p0, p1, p2, p3 };
        remoteTeamReceived = true;
        Debug.Log($"Team remoto ricevuto: {p0}, {p1}, {p2}, {p3}");
        OnAllTeamsReceived?.Invoke();
    }


    /// <summary>
    /// Chiamato da RemotePlayer.StartBattle() per ottenere i nomi del team remoto.
    /// Aspetta finché il team non è stato ricevuto, poi riempie il vettore.
    /// </summary>
    public async Task ReceiveRemoteTeam()
    {
        // aspetta fino a quando il team remoto è disponibile
        while (!remoteTeamReceived)
            await Task.Yield();

        remotePlayer.SetCards(receivedRemoteTeam[0], receivedRemoteTeam[1], receivedRemoteTeam[2], receivedRemoteTeam[3]);
        remoteTeamReceived = false; // reset per la prossima battaglia
    }

    private void StoreRemoteTeamChoice(int c1, int c2)
    {
        receivedTeamChoice = (c1, c2);
        remoteTeamChoiceReceived = true;
    }

    public void  SendLocalTeamChoice(int playerIndex, int c1, int c2)
    {
        if(IsServer)
            ReceiveTeamChoiceRpc(playerIndex, c1, c2);
        else 
            SendTeamChoiceRpc(playerIndex, c1, c2);
    }

    public async Task ReceiveRemoteTeamChoice()
    {
        // aspetta fino a quando il team remoto è disponibile
        while (!remoteTeamChoiceReceived)
            await Task.Yield();

        remotePlayer.SetTeam(receivedTeamChoice.Item1, receivedTeamChoice.Item2);
        remoteTeamChoiceReceived = false;
    }

    public void SendLocalPokemonChoice(int playerIndex, int p)
    {
        if(IsServer)
            ReceivePokemonChoiceRpc(playerIndex, p);
        else
            SendPokemonChoiceRpc(playerIndex, p);
    }

    private void StoreRemotePokemon(int p)
    {
        receivedPokemonChoice = p;
        remotePokemonReceived = true;
    }

    public async Task ReceiveRemotePokemonChoice()
    {
        // aspetta fino a quando il pokemon remoto è disponibile
        while(!remotePokemonReceived)
            await Task.Yield();

        remotePlayer.SetPokemon(receivedPokemonChoice);
        remotePokemonReceived = false;
    }

    public void SendLocalMoveChoice(int playerIndex, int move)
    {
        if (IsServer)
            ReceiveMoveChoiceRpc(BattleManager.Instance.localPlayerIndex, move);
        else 
            SendMoveChoiceRpc(playerIndex, move);
    }

    private void StoreRemoteMove(ActionType a)
    {
        receivedMove = a;
        remoteMoveReceived = true;
    }

    public async Task ReceiveRemoteMoveChoice()
    {
        while(!remoteMoveReceived) 
            await Task.Yield();

        remotePlayer.SetMove(receivedMove);
        remoteMoveReceived = false;
    }

    [Rpc(SendTo.Server)]
    public void SendTeamChoiceRpc(int playerIndex, int c1, int c2)
    {
        StoreRemoteTeamChoice(c1, c2);
        ReceiveTeamChoiceRpc(playerIndex, c1, c2);
    }

    [Rpc(SendTo.NotServer)]
    public void ReceiveTeamChoiceRpc(int playerIndex, int c1, int c2)
    {
        StoreRemoteTeamChoice(c1, c2);
    }

    [Rpc(SendTo.Server)]
    public void SendPokemonChoiceRpc(int playerIndex, int p)
    {
        StoreRemotePokemon(p);
        ReceivePokemonChoiceRpc(playerIndex, p);
    }

    [Rpc(SendTo.NotServer)]
    public void ReceivePokemonChoiceRpc(int playerIndex, int p)
    {
        StoreRemotePokemon(p);
    }

    [Rpc(SendTo.Server)]
    public void SendMoveChoiceRpc(int playerIndex, int a)
    {
        StoreRemoteMove((ActionType)a);
        ReceiveMoveChoiceRpc(playerIndex, a);
    }

    [Rpc(SendTo.NotServer)]
    public void ReceiveMoveChoiceRpc(int playerIndex, int a)
    {
        StoreRemoteMove((ActionType)a);
    }

    /*

    // ── scelta pokemon nel team ───────────────────────────────────────────


    private void SetRemoteTeam(int choice1, int choice2)
    {
        remotePlayer.SetTeam(choice1, choice2);
    }

    [Rpc(SendTo.Server)]
    public void SendTeamChoiceRpc(int playerIndex, int card1Index, int card2Index)
    {
        SetRemoteTeam(card1Index, card2Index);

        if (IsServer)
        {
            // propaga al guest
            //ReceiveTeamChoiceRpc(humanPlayer.playerIndex, humanPlayer.cardIndex[0], humanPlayer.cardIndex[1]);
            ReceiveTeamChoiceRpc(playerIndex, card1Index, card2Index);
        }
    }

    [Rpc(SendTo.NotServer)]
    public void ReceiveTeamChoiceRpc(int playerIndex, int card1Index, int card2Index)
    {
        // sul guest, playerIndex è l'indice del remoto (l'host, quindi 0)
        // non chiamare SetMove se è la mossa del giocatore locale
        int localIndex = BattleManager.Instance.localPlayerIndex;
        if (playerIndex == localIndex) return; // il locale ha già invocato il suo evento

        SetRemoteTeam(card1Index, card2Index);
    }


    // ── scelta pokemon in campo ───────────────────────────────────────────

    private void SetPokemonChoice(int playerIndex, int pokemonIndex)
    {
        receivedPokemonChoice = pokemonIndex;
        remotePlayer.SetPokemon(pokemonIndex);
    }

    [Rpc(SendTo.Server)]
    public void SendPokemonChoiceRpc(int playerIndex, int pokemonIndex)
    {
        SetPokemonChoice(playerIndex, pokemonIndex);
        if (IsServer)
        {
            // propaga al guest
            ReceivePokemonChoiceRpc(playerIndex, pokemonIndex);
        }
    }

    [Rpc(SendTo.NotServer)]
    public void ReceivePokemonChoiceRpc(int playerIndex, int pokemonIndex)
    {
        // sul guest, playerIndex è l'indice del remoto (l'host, quindi 0)
        // non chiamare SetMove se è la mossa del giocatore locale
        int localIndex = BattleManager.Instance.localPlayerIndex;
        if (playerIndex == localIndex) return; // il locale ha già invocato il suo evento

        SetPokemonChoice(playerIndex, pokemonIndex);
    }


    private void SetMoveChoice(int playerIndex, int actionType)
    {
        remotePlayer.SetMove((ActionType)actionType);

    }

    [Rpc(SendTo.Server)]
    public void SendMoveChoiceRpc(int playerIndex, int actionType)
    {
        SetMoveChoice(playerIndex, actionType);
        if (IsServer)
        {
            // propaga al guest per aggiornare la sua UI
            ReceiveMoveChoiceRpc(humanPlayer.pokemonIndex, (int) humanPlayer.MoveChoice);
        }
    }

    [Rpc(SendTo.NotServer)]
    public void ReceiveMoveChoiceRpc(int playerIndex, int actionType)
    {
        // sul guest, playerIndex è l'indice del remoto (l'host, quindi 0)
        // non chiamare SetMove se è la mossa del giocatore locale
        int localIndex = BattleManager.Instance.localPlayerIndex;
        if (playerIndex == localIndex) return; // il locale ha già invocato il suo evento

        // il guest sa cosa ha scelto il remoto — utile per la UI
        SetMoveChoice(playerIndex, actionType);
    }
    */

    // ── inizio turno: seed + mossa ────────────────────────────────────────

    // ── helper per l'host: invia seed al guest all'inizio del turno ───────

    public void BroadcastTurnSeed(int seed)
    {
        if (!IsServer) return;
        BattleRng.SetSeed(seed);          // imposta anche sull'host
        ReceiveTurnSeedRpc(seed);          // invia al guest
    }

    // l'host genera il seed e lo trasmette al guest prima che i giocatori scelgano
    [Rpc(SendTo.NotServer)]
    public void ReceiveTurnSeedRpc(int seed)
    {
        BattleRng.SetSeed(seed);
        // il guest ora ha lo stesso seed dell'host per questo turno
    }
}