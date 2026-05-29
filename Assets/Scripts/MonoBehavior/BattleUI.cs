using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BattleUI : MonoBehaviour
{
    // BattleUI.cs
    public static BattleUI Instance;

    [SerializeField] private GameObject startButton;

    [Header("Card Preview")]
    [SerializeField] private PokemonInfo cardPreview; // il PokemonInfo sull'empty game object
    [SerializeField] private GameObject cardPreviewPanel;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private bool logging = false;

    [SerializeField] private TextMeshProUGUI player1Name;
    [SerializeField] private TextMeshProUGUI[] player1CardNames = new TextMeshProUGUI[4];
    private bool[] validCardsPlayer1 = new bool[4] { false, false, false, false };
    [SerializeField] private TMP_Text player1Dice;
    [SerializeField] private CardContainer player1CardContainer;

    [SerializeField] private TextMeshProUGUI player2Name;
    [SerializeField] private TextMeshProUGUI[] player2CardNames = new TextMeshProUGUI[4];
    private bool[] validCardsPlayer2 = new bool[4] { false, false, false, false };
    [SerializeField] private TMP_Text player2Dice;
    [SerializeField] private CardContainer player2CardContainer;

    [SerializeField] private TMP_Text globalDice;

    private Animator anim;

    private Queue<BattleLog> battleLogs = new Queue<BattleLog>();

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        anim = GetComponent<Animator>();

        PokemonInfo.OnPointerEnterGlobal += ShowCardPreview;
        PokemonInfo.OnPointerExitGlobal += HideCardPreview;
        BattleManager.Instance.OnBattleLogEntry += UpdateBattleLog;
        BattleManager.Instance.OnDiceRoll += RollDice;

        startButton.SetActive(false);
        logging = true;
        StartCoroutine(UpdateBattleLogWithDelay());
    }

    private void OnDestroy()
    {
        PokemonInfo.OnPointerEnterGlobal -= ShowCardPreview;
        PokemonInfo.OnPointerExitGlobal -= HideCardPreview;
        BattleManager.Instance.OnBattleLogEntry -= UpdateBattleLog;
        BattleManager.Instance.OnDiceRoll -= RollDice;
    }

    private void RollDice(int player, int roll)
    {
        var dices = new[] { player1Dice, player2Dice };

        if (player == 0)
        {
            dices[0].text = $"{roll}";
            anim.SetTrigger("roll1");
        }
        else if (player == 1)
        {
            dices[1].text = $"{roll}";
            anim.SetTrigger("roll2");
        }
        else
        {
            globalDice.text = $"{roll}";
            anim.SetTrigger("roll0");
        }
    }

    public void HideContainers()
    {
        var cardContainers = new CardContainer[2] {player1CardContainer, player2CardContainer};
        for (int i = 0; i < cardContainers.Length; i++)
        {
            if (cardContainers[i] != null)
            {
                cardContainers[i].gameObject.SetActive(false);
            }
        }
    }

    public void SetContainers(Pokemon pokemon1, Pokemon pokemon2)
    {
        var cardContainers = new CardContainer[2] { player1CardContainer, player2CardContainer };
        
        cardContainers[0].Initialize(pokemon1);
        cardContainers[1].Initialize(pokemon2);
        cardContainers[0].gameObject.SetActive(true);
        cardContainers[1].gameObject.SetActive(true);
    }

    public void SetPlayersByInput()
    {
        var playerNames = new[] { player1Name, player2Name };
        var playerCardNames = new[] { player1CardNames, player2CardNames };
        var battlePlayers = BattleManager.Instance.players;

        for (int i = 0; i < battlePlayers.Length; i++)
        {
            if (playerNames[i] != null && playerNames[i].text != "")
                BattleManager.Instance.players[i].playerName = playerNames[i].text;

            if (playerCardNames[i].Length == 4 && playerCardNames[i].Count(tmp => tmp == null && tmp.text != "") == 0)
            {
                for (int j = 0; j < playerCardNames[i].Length; j++)
                {
                    BattleManager.Instance.players[i].cardNames[j] = playerCardNames[i][j].text;
                }
            }
        }
    }

    public void CheckCardName(int playercard)
    {
        int player = playercard / 10 - 1;
        int card = playercard % 10 - 1;

        Debug.Log($"Checking card name for player {player + 1}, card {card + 1}");

        var playerCardNames = new[] { player1CardNames, player2CardNames };
        var validCards = new[] { validCardsPlayer1, validCardsPlayer2 };
        var playerNames = new[] { player1Name, player2Name };
        bool res = false;

        if (playerCardNames[player][card] != null) 
        {
            Debug.Log($"Cercando chiave: '{playerCardNames[player][card].text}'");
            res = PokedeckManager.CardExists(playerCardNames[player][card].text); 
        }

        
        validCards[player][card] = res;
        playerCardNames[player][card].color = (res == true) ? Color.green : Color.red;

        Debug.Log($"Card name '{playerCardNames[player][card].text}' is {(res ? "valid" : "invalid")}");

        var allValid = validCards.All(p => p.All(v => v)) && playerNames.All(p => p != null && p.text != "");
        startButton.SetActive(allValid);
        if(allValid)
        {
            SetPlayersByInput();
            Debug.Log("All card names are valid. Start button enabled.");
        }
    }

    IEnumerator UpdateBattleLogWithDelay()
    {
        while (logging)
        {
            yield return new WaitForSeconds(0.5f); // Delay di 0.5 secondi

            for (int i = 0; i < 5 && battleLogs.Count > 0; i++)
            {
                var log = battleLogs.Dequeue(); 
                logText.text = $"<color=#{log.color.ToHexString()}>{log.message}</color>\n" + logText.text.Truncate(5000);

                yield return new WaitForSeconds(1.0f); // Delay di 1.0 secondi tra ogni log
            }
        }
    }

    private void UpdateBattleLog(BattleLog log)
    {
        battleLogs.Enqueue(log);
    }

    public void FlushLog()
    {
        battleLogs.Clear();
        logText.text = "";
    }

    private void ShowCardPreview(Pokemon pokemon)
    {
        cardPreview.SetPokemonInfo(pokemon);
        //cardPreviewPanel.SetActive(true);
        //anim.SetBool("appear", true);
        anim.SetTrigger("appear");
    }

    private void HideCardPreview()
    {
        //cardPreviewPanel.SetActive(false);
        //anim.SetBool("appear", false);
    }

    public void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
    Debug.Log("Quit non supportato in WebGL");
#else
    Application.Quit();
#endif
    }
}
