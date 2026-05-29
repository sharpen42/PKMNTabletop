using UnityEngine;
using UnityEngine.UI;

public class HumanPlayer : Player
{
    [SerializeField] private Transform teamPanel;
    [SerializeField] private PokemonInfo[] teamButtons;

    [SerializeField] private Transform pokemonPanel;
    [SerializeField] private PokemonInfo[] pokemonButtons;

    [SerializeField] private Transform movePanel;
    [SerializeField] private PokemonInfo[] moveButtons;

    private void Start()
    {
        teamPanel.gameObject.SetActive(false);
        pokemonPanel.gameObject.SetActive(false);
        movePanel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {

    }


    override public void ChooseTeam()
    {

        for (int i = 0; i < cards.Length; i++)
        {
            PokedeckManager.PrintCard(cards[i]);
            teamButtons[i].SetPokemonInfo(cards[i]);
            teamButtons[i].transform.parent.GetComponent<Button>().interactable = true;
        }
        teamPanel.gameObject.SetActive(true);
    }

    override public void ChoosePokemon()
    {
        for(int i = 0; i < pokemonButtons.Length; i++)
        {
            pokemonButtons[i].SetPokemonInfo(team[i]);
        }
        pokemonPanel.gameObject.SetActive(true);
    }

    override public void ChooseMove()
    {
        // 0: null, 1: swap, 2: move1, 3: move2
        int PokemonOnField = BattleManager.Instance.onFieldPkmn[playerIndex];
        int PokemonInTeam = 1 - PokemonOnField;
        Debug.Log("Choosing move for Pokemon " + PokemonOnField + ": " + team[PokemonOnField].cardName);
        Debug.Log("in team " + PokemonInTeam + ": " + team[PokemonInTeam].cardName);
        moveButtons[1].SetPokemonInfo(team[PokemonInTeam]);
        moveButtons[2].SetPokemonInfo(team[PokemonOnField]);
        moveButtons[3].SetPokemonInfo(team[PokemonOnField]);
        movePanel.gameObject.SetActive(true);
    }

    public void SetTeam(int index)
    {
        if(index < 0 || index >= cards.Length) return;
        int count = 0;
        foreach (Pokemon p in team)
        {
            if (p != null)
            {
                count++;
            }
        }
        team[count] = new Pokemon(cards[index]);
        cardIndex[count] = index;
        Debug.Log("Pokemon added to team: " + team[count].cardName);
        bool allChosen = true;
        for (int i = 0; i < team.Length; i++)
        {
            allChosen &= (team[i] != null);
        }
        if (!allChosen) return;
        teamPanel.gameObject.SetActive(false);
        InvokeOnTeamChosen(cardIndex[0], cardIndex[1]);
    }

    public void SetPokemon(int index)
    {
        if(index < 0 || index >= team.Length) return;
        Debug.Log("Pokemon chosen: " + team[index].cardName);
        pokemonIndex = index;
        pokemonPanel.gameObject.SetActive(false);
        InvokeOnPokemonChosen(index);
    }

    public void SetMove(int move)
    {
        ActionType action = (ActionType)move;
        if (action >= ActionType._err) return;
        MoveChoice = action;
        movePanel.gameObject.SetActive(false);
        InvokeOnMoveChosen(MoveChoice);
    }
}
