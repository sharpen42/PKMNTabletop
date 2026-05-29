using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Player : MonoBehaviour
{
    public string playerName;
    public string[] cardNames = new string[4];
    public PKMNCard[] cards = new PKMNCard[4];
    public int playerIndex = 0;
    public ActionType MoveChoice { get; protected set; } = 0;
    public int pokemonIndex { get; protected set; } = -1;

    public int[] cardIndex = {-1, -1};
    public bool teamSet { get {
            bool isSet = true;
            for (int i = 0; i < team.Length; i++)
                {
                    if (team[i] == null)
                    {
                        isSet = false;
                        break;
                    }
                }
            return isSet;
        } }

    [SerializeField] private PokemonInfo[] pokemonInfos = new PokemonInfo[4];
    public Pokemon[] team = new Pokemon[2] { null, null };
    private bool[] revealed = new bool[2] { false, false };

    public event Action<int> OnPlayerReady;
    public event Action<int, int, int> OnTeamChosen;
    public event Action<int, int> OnPokemonChosen;
    public event Action<int, ActionType> OnMoveChosen;

    private void Start()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (pokemonInfos[i] != null)
            {
                pokemonInfos[i].gameObject.SetActive(false);
            }
        }

    }

    private void OnDestroy()
    {

    }

    public void InvokeOnPlayerReady()
    {
        OnPlayerReady?.Invoke(playerIndex);
    }

    public void InvokeOnTeamChosen(int card1Index, int card2Index)
    {
        cardIndex = new int[] { card1Index, card2Index };
        OnTeamChosen?.Invoke(playerIndex, card1Index, card2Index);
    }

    public void InvokeOnPokemonChosen(int p)
    {
        pokemonIndex = p;
        OnPokemonChosen?.Invoke(playerIndex, p);
    }

    public void InvokeOnMoveChosen(ActionType action)
    {
        MoveChoice = action;
        OnMoveChosen?.Invoke(playerIndex, action);
    }

    virtual public void StartBattle()
    {
        for(int i = 0; i < cards.Length; i++)
        {
            cards[i] = PokedeckManager.GetCard(cardNames[i]);
            var pkmncard = cards[i];
            if (pkmncard != null)
            {
                Debug.Log($"Player {playerName} loaded card {pkmncard.cardName}");
            }
            else
            {
                Debug.LogError($"Player {playerName} failed to load card {cardNames[i]}");
            }
            //PokedeckManager.PrintCard(cards[i]);

        }

        for (int i = 0; i < cards.Length; i++)
        {
            if(pokemonInfos[i] != null)
            {
                pokemonInfos[i].gameObject.SetActive(true);
                pokemonInfos[i].SetPokemonInfo(cards[i]);
            }
            else Debug.LogWarning($"Player {playerName} has no PokemonInfo assigned for card index {i}");
        }

        InvokeOnPlayerReady();
    }

    virtual public void ChooseTeam()
    {
        cardIndex[0] = 0;
        cardIndex[1] = Random.Range(1, cards.Length);
        team = new Pokemon[] { new Pokemon(cards[cardIndex[0]]), new Pokemon(cards[cardIndex[1]]) };
        InvokeOnTeamChosen(cardIndex[0], cardIndex[1]);
    }

    virtual public void ChoosePokemon()
    {
        int p = Random.Range(0, team.Length);
        InvokeOnPokemonChosen(p);
    }

    virtual public void ChooseMove()
    {
        ActionType[] allActions = new ActionType[] { ActionType.Swap, ActionType.NormalMove, ActionType.SpecialMove };
        ActionType action = allActions[Random.Range(0, allActions.Length)];
        InvokeOnMoveChosen(action);
    }

    virtual public void EndBattle()
    {
        for (int i = 0; i < pokemonInfos.Length; i++)
        {
            if (pokemonInfos[i] != null)
            {
                pokemonInfos[i].gameObject.SetActive(false);
                pokemonInfos[i].SetPokemonInfo(cards[i]);
            }
        }

        for(int i = 0; i < team.Length; i++)
        {
            team[i].Reset();
        }
    }
}
