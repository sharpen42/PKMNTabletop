using System;
using System.Collections.Generic;
using System.Diagnostics;

public class Effect
{
    public static List<Effect> allEffects = new();

    public enum Status { Healthy, Burned, Frozen, Paralyzed, Poisoned, Asleep };
    public enum Category { NoEff, Status, Debuff, Buff, TypeEffModifier, SkipTurn, AutoKO, Heal };

    public const int ATKVAR1 = 2;
    public const int ATKVAR2 = 4;
    public const int ATKVAR3 = 6;

    public const int DEFVAR1 = 1;
    public const int DEFVAR2 = 2;

    public const int SPEVAR1 = 1;
    public const int SPEVAR2 = 2;

    public const int HPHEAL1 = 5;
    public const int HPHEAL2 = 10;
    public const int HPHEAL3 = 20;

    public const int BRN_ATKDEBUFF = 6;
    public const int FRZ_DEFDEBUFF = 2;
    public const int PAR_SPEDEBUFF = 2;
    public const int SLP_D6MIN = 3;
    public const int SLP_DURMAX = 3;
    public const int PSN_HPLOSS = 1;

    public int ID { get; private set; }
    public string description { get; private set; }

    public Category category { get; private set; }
    private Action<Pokemon, Pokemon> effect;

    public static int RollD6()
    {
        int result = BattleRng.Roll(1, 7);
        BattleManager.InvokeD6Roll(-1, result);
        return result;
    }

    private Effect(string text, Action<Pokemon, Pokemon> effect, Category category)
    {
        this.description = text;
        this.effect = effect;
        this.category = category;

        ID = allEffects.Count;
        allEffects.Add(this);
    }

    public bool Equals(Effect eff)
    {
        return this.ID == eff.ID;
    }

    public void Apply(Pokemon self, Pokemon opponent)
    {
        effect(self, opponent);
    }

    public static Effect NoEff = new Effect(
        "Nessun effetto.", 
        (_, _) => { }, 
        Category.NoEff
        );

    public static Effect AtkDebuff2 = new Effect(
        $"-{ATKVAR2} ATK all'avversario.",
        (_, opp) => { opp.ModifyAtk(-ATKVAR2); },
        Category.Debuff
        );

    public static Effect DefDebuff2 = new Effect(
        $"-{DEFVAR1} DEF all'avversario.",
        (_, opp) => { opp.ModifyDef(-DEFVAR1); },
        Category.Debuff
        );

    public static Effect SpeDebuff2 = new Effect(
        $"-{SPEVAR1} SPE all'avversario.",
        (_, opp) => { opp.ModifySpe(-SPEVAR1); },
        Category.Debuff
        );

    public static Effect DefBuff2 = new Effect(
        $"+{DEFVAR1} DEF.",
        (self, _) => { self.ModifyDef(DEFVAR1); },
        Category.Buff
        );


    public static Effect SpeBuff1 = new Effect(
        $"+{SPEVAR1} SPE.",
        (self, _) => { self.ModifySpe(SPEVAR1); },
        Category.Buff
        );

    public static Effect Heal1 = new Effect(
        $"Cura +{HPHEAL1} HP.",
        (self, _) => { self.Heal(5); },
        Category.Heal
        );

    public static Effect Heal2 = new Effect(
        $"Cura +{HPHEAL2} HP.",
        (self, _) => { self.Heal(10); },
        Category.Heal
        );

    public static Effect Paralyze = new Effect(
        "L'avversario è Paralizzato.",
        (_, opp) => { opp.SetStatus(Status.Paralyzed); },
        Category.Status
        );

    public static Effect Burn = new Effect(
        "L'avversario è Scottato.", 
        (_, opp) => { opp.SetStatus(Status.Burned); }, 
        Category.Status
        );

    public static Effect Freeze = new Effect(
        "L'avversario è Congelato.",
        (_, opp) => { opp.SetStatus(Status.Frozen); },
        Category.Status
        );

    public static Effect Sleep = new Effect(
        "L'avversario è Addormentato.",
        (_, opp) => { opp.SetStatus(Status.Asleep); },
        Category.Status
        );

    public static Effect Poison = new Effect(
        "L'avversario è Avvelenato.",
        (_, opp) => { opp.SetStatus(Status.Poisoned); },
        Category.Status
        );

    public static Effect TriAttack = new Effect(
        "Tira un D6: L'avversario è \n- 1/2: Scottato\n- 3/4: Paralizzato\n- 5/6: Congelato\n",
        (_, opp) => {
            int d6 = RollD6();
            switch(d6)
            {
                case 1:
                case 2:
                    opp.SetStatus(Status.Burned);
                    break;
                case 3:
                case 4:
                    opp.SetStatus(Status.Paralyzed);
                    break;
                case 5:
                case 6:
                    opp.SetStatus(Status.Frozen);
                    break;
            }
        },
        Category.Status
        );

    public static Effect SupereffOnGround = new Effect(
        "Superefficace se l'avversario è di Tipo Terra",
        (self, opp) => { self.SetEffectiveness((opp.currentType == Pokemon.Type.Ground) ? 2f : 1f); },
        Category.TypeEffModifier
        );

    public static Effect SkipTurn = new Effect(
        "Salta il prossimo turno.",
        (self, _) => { self.RequestSkipTurn(); },
        Category.SkipTurn
        );

    public static Effect AutoKO = new Effect(
        "Scende a 0 HP (KO) dopo l'attacco.",
        (self, _) => { self.ApplyDamage(self.currentHp); },
        Category.AutoKO
        );

    public static Effect SoftBoiled = new Effect(
        "Cura 20 HP.",
        (self, _) => { self.Heal(HPHEAL3); },
        Category.Heal
        );

    public static Effect Transform = new Effect(
        "Copia Tipo, ATK, DEF e SPE dell'avversario.",
        (self, opp) => { self.TransformInto(opp, true); },
        Category.Buff
        );
}
