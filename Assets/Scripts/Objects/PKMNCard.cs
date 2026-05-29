using System;
using UnityEngine;

public class PKMNCard
{
    public enum Stats { HP, ATK, DEF, SPE, DMG, D6, _err };
    public enum Version { A, B, C, S };
    public string cardName { get; private set; }
    public string pkmnName { get; private set; }
    public int copies { get; private set; }

    private Version version;

    public Pokemon pokemon { get; private set; } = null;

    public Pokemon.Type type { get; private set; }
    public int[] stats { get; private set; } = new int[(int) Stats._err];
    public string specialMoveName { get; private set; }
    public Pokemon.Type specialMoveType { get; private set; }
    public Effect specialMoveEff { get; private set; }
    public Sprite image { get; private set; }

    public PKMNCard(string cardName, string pkmnName, Version version, int copies, Pokemon.Type type, 
        int hp, int atk, int def, int spe, int dmg, int d6, 
        string specialMoveName, Pokemon.Type specialMoveType, Effect specialMoveEff, Sprite image)
    {
        this.cardName = cardName;
        this.pkmnName = pkmnName;
        this.version = version;
        this.copies = copies;
        this.type = type;
        this.specialMoveName = specialMoveName;
        this.specialMoveType = specialMoveType;
        this.specialMoveEff = specialMoveEff;
        this.image = image;

        stats[(int) Stats.HP] = hp;
        stats[(int) Stats.ATK] = atk;
        stats[(int) Stats.DEF] = def;
        stats[(int) Stats.SPE] = spe;
        stats[(int) Stats.DMG] = dmg;
        stats[(int) Stats.D6] = d6;
    }

    public void SetPokemon(Pokemon pokemon)
    {
        this.pokemon = pokemon;
    }
}
