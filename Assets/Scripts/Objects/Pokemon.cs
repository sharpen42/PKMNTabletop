using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public class Pokemon
{
    public static int MAX_DEF = 6;
    public static int MAX_SPE = 5;

    public enum Type
    {
        Grass, Water, Fire, Electric, Psychic, Ground, Normal, Flying, Steel, Dark, Fairy, _err
    }

    private static int typeNum = (int)Type._err;
    private static float[,] typeChart = new float[typeNum, typeNum];

    public static void SetTypeEffectiveness(Type atk, Type def, float value)
    {
        typeChart[(int)atk, (int)def] = value;
    }

    public static float GetEffectiveness(Type atkType, Type defType)
    {
        return typeChart[(int)atkType, (int)defType];
    }

    private PKMNCard card;

    public int[] stats { get { return (isTransformed) ? transformedInto.card.stats : card.stats; } }

    public string name { get { return (isTransformed) ? ($"{transformedInto.card.pkmnName} ({card.pkmnName})") : card.pkmnName; } }
    public string cardName { get { return (isTransformed) ? transformedInto.card.cardName : card.cardName; } }

    public Type currentType { get; private set; }


    public int currentHp { get; private set; }
    public int maxHp { get { return (isTransformed) ? transformedInto.card.stats[(int)PKMNCard.Stats.HP] : card.stats[(int)PKMNCard.Stats.HP]; } }

    public Effect.Status status { get; private set; } = Effect.Status.Healthy;

    private int atkModifier = 0;
    private int defModifier = 0;
    private int speModifier = 0;

    public int currentAtk { 
        get
        {
            int val = stats[(int)PKMNCard.Stats.ATK] + atkModifier;
            if(status == Effect.Status.Burned) val -= Effect.BRN_ATKDEBUFF;
            return (int) Mathf.Max(1, val);
        }
    }

    public int currentDef
    {
        get
        {
            int val = stats[(int)PKMNCard.Stats.DEF] + defModifier;
            if (status == Effect.Status.Frozen) val -= Effect.FRZ_DEFDEBUFF;
            return (int) Mathf.Max(1, val);
        }
    }

    public int currentSpe
    {
        get
        {
            int val = stats[(int)PKMNCard.Stats.SPE] + speModifier;
            if (status == Effect.Status.Paralyzed) val -= Effect.PAR_SPEDEBUFF;
            return (int) Mathf.Max(1, val);
        }
    }

    private float currentEffectiveness = 1f;

    private string currentSpecialMoveName;
    public string CurrentSpecialMoveName
    {
        get
        {
            if(specialMoveChanged) return currentSpecialMoveName;
            return card.specialMoveName;
        }
    }


    private Type currentSpecialMoveType = Type.Normal;
    public Type CurrentSpecialMoveType
    {
        get
        {
            if(specialMoveChanged) return currentSpecialMoveType;
            return card.specialMoveType;
        }
    }

    private int currentDmg;
    public int CurrentDmg
    {
        get
        {
            if (specialMoveChanged) return currentDmg;
            return card.stats[(int)PKMNCard.Stats.DMG];
        }
    }

    private int currentD6;
    public int CurrentD6
    {
        get
        {
            if (specialMoveChanged) return currentD6;
            return card.stats[(int)PKMNCard.Stats.D6];
        }
    }

    private Effect currentSpecialMoveEffect;
    public Effect CurrentSpecialMoveEffect
    {
        get
        {
            if (specialMoveChanged) return currentSpecialMoveEffect;
            return card.specialMoveEff;
        }
    }
    private bool specialMoveChanged = false;

    public bool isTransformed { get; private set; } = false;
    public bool transformReversable { get; private set; } = false;

    public Pokemon transformedInto { get; private set; } = null;

    public bool skipTurn { get; private set; } = false;

    private int asleepTurns = 0;
    public bool isSleeping { get { return status == Effect.Status.Asleep; } }

    private List<Action<Pokemon, Pokemon>> endTurnEffects = new();

    public event Action OnStatsChanged;
    public event Action<int> OnHpChanged;
    public event Action<int> OnTakeDamage;
    public event Action OnKO;

    public Pokemon(PKMNCard card)
    {
        if(card == null) throw new ArgumentNullException("Card cannot be null");
        this.card = card;

        currentHp = card.stats[(int)PKMNCard.Stats.HP];
        currentType = card.type;
        currentSpecialMoveName = card.specialMoveName;
        currentSpecialMoveType = card.specialMoveType;
        currentDmg = card.stats[(int)PKMNCard.Stats.DMG];
        currentD6 = card.stats[(int)PKMNCard.Stats.D6];
        currentSpecialMoveEffect = card.specialMoveEff;

        card.SetPokemon(this);
    }

    private int DamageRounding(float damage)
    {
        return (int) Mathf.Ceil(damage);
    }

    public void SetStatus(Effect.Status newStatus)
    {
        if(status == Effect.Status.Healthy)
            status = newStatus;
    }

    public void SetEffectiveness(float newEffectiveness)
    {
        currentEffectiveness = newEffectiveness;
    }

    public int RegularMoveDamage(Pokemon opponent)
    {
        float typeEff = (currentEffectiveness == 1) ? GetEffectiveness(currentType, opponent.currentType) : currentEffectiveness;
        return DamageRounding(currentAtk * typeEff);
    }

    public int SpecialMoveDamage(Pokemon opponent)
    {
        float typeEff = (currentEffectiveness == 1) ? GetEffectiveness(CurrentSpecialMoveType, opponent.currentType) : currentEffectiveness;
        return DamageRounding(CurrentDmg * typeEff);
    }

    public void ResetModifiers()
    {
        atkModifier = 0;
        defModifier = 0;
        speModifier = 0;

        if(isTransformed && transformReversable)
        {
            isTransformed = false;
            transformedInto = null;
            currentType = card.type;
        }
        currentEffectiveness = 1f;

        OnStatsChanged?.Invoke();
    }

    public void Reset()
    {
        transformReversable = true;
        ResetModifiers();
        ClearStatus();
        currentHp = card.stats[(int) PKMNCard.Stats.HP];
    }

    public bool TryWakeUp(int roll)
    {
        if (status != Effect.Status.Asleep) return true; // non dormiva, può attaccare

        asleepTurns++;
        if (roll > Effect.SLP_D6MIN || asleepTurns >= Effect.SLP_DURMAX)
        {
            ClearStatus();
            asleepTurns = 0;
            return true; // si sveglia, può attaccare
        }
        return false; // resta addormentato
    }

    public void PoisonTick()
    {
        if (status == Effect.Status.Poisoned)
        {
            ApplyDamage(Effect.PSN_HPLOSS);
            OnHpChanged?.Invoke(currentHp);
        }
    }

    // modifica ClearStatus per resettare anche il contatore
    public void ClearStatus()
    {
        status = Effect.Status.Healthy;
        asleepTurns = 0;
    }

    private void HandleKO()
    {
        currentHp = 0;
        ResetModifiers();
        ClearStatus();
        OnKO?.Invoke();
    }

    private int modifyStat(PKMNCard.Stats stat, int modifier, int amount)
    {
        int prev = 0;
        int MAX = int.MaxValue;
        switch (stat)
        {
            case PKMNCard.Stats.ATK: prev = currentAtk; break;
            case PKMNCard.Stats.DEF: prev = currentDef; MAX = MAX_DEF; break;
            case PKMNCard.Stats.SPE: prev = currentSpe; MAX = MAX_SPE; break;
            default: prev = 0; break;
        }

        if (prev + amount <= 0)
        {
            modifier = 1 - stats[(int) stat];
        }
        else if (prev + amount > MAX)
        {
            modifier = MAX - stats[(int)stat];
        }
        else
        {
            modifier += amount;
        }

        return modifier;
    }

    public void ModifyAtk(int amount)
    {
        int prev = currentAtk;
        atkModifier = modifyStat(PKMNCard.Stats.ATK, atkModifier, amount);
        if(prev != currentAtk) OnStatsChanged?.Invoke();
    }

    public void ModifyDef(int amount)
    {
        int prev = currentDef;
        defModifier = modifyStat(PKMNCard.Stats.DEF, defModifier, amount);
        if(prev != currentDef) OnStatsChanged?.Invoke();
    }

    public void ModifySpe(int amount)
    {
        int prev = currentSpe;
        speModifier = modifyStat(PKMNCard.Stats.SPE, speModifier, amount);
        if(prev != currentSpe) OnStatsChanged?.Invoke();
    }

    public void ModifySpecialMove(string name, Type type, Effect effect, int dmg, int d6)
    {
        specialMoveChanged = true;
        if (name != null) currentSpecialMoveName = name;
        if (effect != null) currentSpecialMoveEffect = effect;
        if (type != Type._err) currentSpecialMoveType = type;
        if (dmg >= 0) currentDmg = dmg;
        if (d6 >= 0 && d6 < 6) currentD6 = d6;
        OnStatsChanged?.Invoke();
    }

    public void Heal(int amount)
    {
        currentHp = Math.Min(currentHp + amount, maxHp);
        OnStatsChanged?.Invoke();
        OnHpChanged?.Invoke(currentHp);
        OnTakeDamage?.Invoke(amount);
    }

    public void ApplyDamage(int damage)
    {
        currentHp -= damage;
        OnStatsChanged?.Invoke();
        OnHpChanged?.Invoke(currentHp);
        OnTakeDamage?.Invoke(-damage);
        if (currentHp <= 0)
        {
            HandleKO();
        }
    }

    public void RequestSkipTurn()
    {
        skipTurn = true;
    }

    public void ConsumeSkipTurn()
    {
        skipTurn = false;
    }

    public void TransformInto(Pokemon pokemon, bool reversable)
    {
        if (pokemon == null) return;

        isTransformed = true;
        transformedInto = new Pokemon(PokedeckManager.allCards[pokemon.cardName]);
        currentType = pokemon.currentType;
        this.transformReversable = reversable;
        if(!reversable) ModifySpecialMove(pokemon.CurrentSpecialMoveName, pokemon.CurrentSpecialMoveType, pokemon.CurrentSpecialMoveEffect, pokemon.CurrentDmg, pokemon.CurrentD6);

        OnStatsChanged?.Invoke();
    }
}
