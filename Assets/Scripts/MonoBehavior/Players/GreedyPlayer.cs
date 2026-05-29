using System.Collections.Generic;
using UnityEngine;

public class GreedyPlayer : Player
{
    private int swapCount = 0;

    private int swapLimit = 3; // Limite di swap per evitare abusi

    // Sceglie i 2 pokemon con l'attacco più alto tra le 4 carte
    override public void ChooseTeam()
    {
        List<PKMNCard> available = new List<PKMNCard>(cards);
        available.Sort((a, b) => b.stats[(int)PKMNCard.Stats.ATK].CompareTo(a.stats[(int)PKMNCard.Stats.ATK]));
        team = new Pokemon[] { new Pokemon(available[0]), new Pokemon(available[1]) };
        InvokeOnTeamChosen(-1, -1);
    }

    // Sceglie il pokemon con l'attacco più alto
    override public void ChoosePokemon()
    {
        int best = 0;
        for (int i = 1; i < team.Length; i++)
        {
            if (team[i].currentAtk > team[best].currentAtk)
                best = i;
        }
        InvokeOnPokemonChosen(best);
    }

    private int SpecialMoveScore(Pokemon self, Pokemon opponent)
    {
        // valuta se la mossa speciale è vantaggiosa (es. se è super efficace o se ha effetti benefici)
        int score = (self.CurrentDmg > 0) ? self.SpecialMoveDamage(opponent) : 0;
        if(self.CurrentSpecialMoveEffect.category == Effect.Category.AutoKO || self.CurrentSpecialMoveEffect.category == Effect.Category.SkipTurn)
        {
            score /= 2; 
        }
        return score;
    }

    override public void ChooseMove()
    {
        int PokemonOnField = BattleManager.Instance.onFieldPkmn[playerIndex];
        int PokemonInTeam = 1 - PokemonOnField; // l'altro pokemon in squadra

        Pokemon self = team[PokemonOnField];
        Pokemon opponent = BattleManager.Instance.players[1 - playerIndex].team[BattleManager.Instance.onFieldPkmn[1 - playerIndex]];

        // calcola danno atteso per attacco normale
        int normalDmg = self.RegularMoveDamage(opponent);

        // calcola danno atteso per mossa speciale (solo se ha DMG > 0)
        int specialDmg = SpecialMoveScore(self, opponent);

        // valuta se conviene scambiare: il pokemon in squadra fa più danno?
        Pokemon reserve = team[PokemonInTeam];
        int reserveNormalDmg = reserve.RegularMoveDamage(opponent);
        int reserveSpecialDmg = SpecialMoveScore(reserve, opponent);
        int reserveBestDmg = Mathf.Max(reserveNormalDmg, reserveSpecialDmg);

        int selfBestDmg = Mathf.Max(normalDmg, specialDmg);

        // scambia solo se la riserva fa significativamente più danno
        if (reserveBestDmg > selfBestDmg * 1.5f && reserve.currentHp > 0 && swapCount < swapLimit)
        {
            InvokeOnMoveChosen(ActionType.Swap);
            swapCount++;
            return;
        }

        // altrimenti usa la mossa che fa più danno
        if (specialDmg >= normalDmg && self.CurrentDmg > 0)
            InvokeOnMoveChosen(ActionType.SpecialMove);
        else
            InvokeOnMoveChosen(ActionType.NormalMove);
        swapCount = 0; // resetta il contatore se non si è scambiato
    }
}
