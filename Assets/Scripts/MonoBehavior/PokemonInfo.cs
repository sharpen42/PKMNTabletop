using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PokemonInfo : MonoBehaviour
{
    public static event Action<Pokemon> OnPointerEnterGlobal;
    public static event Action OnPointerExitGlobal;

    [SerializeField] private TextMeshProUGUI pokemonName;
    [SerializeField] private TextMeshProUGUI hp;
    [SerializeField] private TextMeshProUGUI attack;
    [SerializeField] private TextMeshProUGUI defense;
    [SerializeField] private TextMeshProUGUI speed;
    [SerializeField] private TextMeshProUGUI move2name;
    [SerializeField] private TextMeshProUGUI move2dmg;
    [SerializeField] private TextMeshProUGUI move2d6;
    [SerializeField] private TextMeshProUGUI move2effect;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image typeImage;
    [SerializeField] private Image move2typeImage;

    public Pokemon pokemon { get; private set; } = null; 

    public void TriggerPointerEnter() => OnPointerEnterGlobal?.Invoke(pokemon);

    public void TriggerPointerExit() => OnPointerExitGlobal?.Invoke();

    private string zeroToEmpty(int stat)
    {
        return (stat == 0) ? "--" : stat.ToString();
    }

    public void SetPokemonInfo(PKMNCard card)
    {
        var p = (card.pokemon == null) ? new Pokemon(card) : card.pokemon;
        SetPokemonInfo(p);
    }

    public void SetPokemonInfo(Pokemon pokemon)
    {
        this.pokemon = pokemon;

        // text: name, hp, attack, defense, speed, move2name, move2dmg, move2d6, move2effect
        if (pokemonName != null)
            pokemonName.text = pokemon.name;

        if(hp != null)
            hp.text =           $"{pokemon.currentHp}";

        if(attack != null)
            attack.text =       $"{pokemon.currentAtk}";

        if(defense != null)
            defense.text =      $"{pokemon.currentDef}";

        if(speed != null)
            speed.text =        $"{pokemon.currentSpe}";

        if(move2name != null)
            move2name.text =    $"{pokemon.CurrentSpecialMoveName}";

        if(move2dmg != null)
            move2dmg.text =     $"{zeroToEmpty(pokemon.CurrentDmg)}";

        if(move2d6 != null)
            move2d6.text =      $"{zeroToEmpty(pokemon.CurrentD6)}";

        if(move2effect != null)
            move2effect.text =  $"{pokemon.CurrentSpecialMoveEffect.description}";

        // image: card, type, move2type
        if(cardImage != null)
            cardImage.sprite = PokedeckManager.GetCard(pokemon.cardName).image;

        if(typeImage != null)
            typeImage.sprite = LoadManager.LoadTypeSprite(pokemon.currentType);

        if(move2typeImage != null)
            move2typeImage.sprite = LoadManager.LoadTypeSprite(pokemon.CurrentSpecialMoveType);
    }

    public void UpdatePokemonInfo()
    {
        if (pokemon != null)
        {
            SetPokemonInfo(pokemon);
        }
    }
}
