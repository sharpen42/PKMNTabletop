using System.Threading.Tasks;
using UnityEngine;

public class RemotePlayer : Player
{

    public override void StartBattle()
    {
        BattleManager.Instance.LogEntry("Remote player battle starting...", Color.cyan);
    }

    public override void ChooseTeam()
    {
        BattleManager.Instance.LogEntry("Remote player is choosing team...", Color.cyan);
    }

    public override void ChoosePokemon()
    {
        BattleManager.Instance.LogEntry("Remote player is choosing pokemon...", Color.cyan);
    }

    public override void ChooseMove()
    {
        BattleManager.Instance.LogEntry("Remote player is choosing move...", Color.cyan);
    }

    public void SetCards(string card0, string card1, string card2, string card3)
    {
        BattleManager.Instance.LogEntry("Remote player cards received!", Color.green);
        cardNames = new string[] { card0, card1, card2, card3 };
        base.StartBattle();
    }

    public void SetTeam(int choice1, int choice2)
    {
        //await OnlineBattleManager.Instance.WaitForRemoteTeamChoice();
        BattleManager.Instance.LogEntry("Remote player team received!", Color.green);
        if (choice1 != choice2 
            && choice1 >= 0 && choice1 < cards.Length
            && choice2 >= 0 && choice2 < cards.Length )
        {
            team = new Pokemon[2];
            team[0] = new Pokemon(cards[choice1]);
            team[1] = new Pokemon(cards[choice2]);

            InvokeOnTeamChosen(choice1, choice2);
        }
    }

    public void SetPokemon(int choice)
    {
        //await OnlineBattleManager.Instance.WaitForRemotePokemon();
        BattleManager.Instance.LogEntry("Remote player first Pokemon received!", Color.green);
        if (choice >= 0 && choice < team.Length)
        {
            pokemonIndex = choice;
            InvokeOnPokemonChosen(choice);
        }
    }

    public void SetMove(ActionType action)
    {
        //await OnlineBattleManager.Instance.WaitForRemoteMove();
        BattleManager.Instance.LogEntry("Remote player move received!", Color.green);
        if (action >= ActionType.Item  && action < ActionType._err)
        {
            MoveChoice = action;
            InvokeOnMoveChosen(action);
        }
    }
}
