using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(PokemonInfo))]
[RequireComponent(typeof(Animator))]
public class CardContainer : MonoBehaviour
{
    public string cardNickname = "";
    /*
    [SerializeField] private Image cardImage;
    [SerializeField] private TextMeshProUGUI cardNameText;

    [SerializeField] private Image cardTypeImage;
    [SerializeField] private TextMeshProUGUI[] cardStatsText = new TextMeshProUGUI[(int)PKMNCard.Stats._err];

    [SerializeField] private Image cardMoveTypeImage;
    [SerializeField] private TextMeshProUGUI cardMoveNameText;
    [SerializeField] private TextMeshProUGUI cardMoveEffText;
    */

    public PokemonInfo pokemonInfo;
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private RectTransform hpBar;
    [SerializeField] private GameObject[] statusImage;
    [SerializeField] private Animator anim;
    [SerializeField] private BoxCollider cardCollider;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color healColor = Color.green;

    public UnityEvent OnPointerEnter;
    public UnityEvent OnPointerExit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();
        cardCollider = GetComponent<BoxCollider>();
        pokemonInfo = GetComponent<PokemonInfo>();
    }

    private void OnDestroy()
    {
        pokemonInfo.pokemon.OnHpChanged -= UpdateHpBar;
        pokemonInfo.pokemon.OnTakeDamage -= UpdateDamageText;
    }

    private void OnMouseDown()
    {
        OnPointerEnter?.Invoke();
        pokemonInfo.TriggerPointerEnter();
    }

    private void OnMouseUp()
    {
        OnPointerExit?.Invoke();
        pokemonInfo.TriggerPointerExit();
    }

    public void Initialize(Pokemon pokemon)
    {
        if(pokemonInfo.pokemon != null)
        {
            pokemonInfo.pokemon.OnHpChanged -= UpdateHpBar;
            pokemonInfo.pokemon.OnTakeDamage -= UpdateDamageText;
        }
        pokemonInfo.SetPokemonInfo(pokemon);
        pokemonInfo.pokemon.OnHpChanged += UpdateHpBar;
        pokemonInfo.pokemon.OnTakeDamage += UpdateDamageText;
        UpdateHpBar(pokemon.currentHp);
        for(int i = 0; i < statusImage.Length; i++)
        {
            statusImage[i].SetActive(pokemon.status == (Effect.Status) (i + 1));
        }

        //Debug.Log($"Initialized CardContainer with Pokemon: {pokemon.name} (HP: {pokemon.currentHp}/{pokemon.maxHp})");
    }

    public void Initialize(PKMNCard card)
    {
        pokemonInfo.SetPokemonInfo(card);
        pokemonInfo.pokemon.OnHpChanged += UpdateHpBar;
        pokemonInfo.pokemon.OnTakeDamage += UpdateDamageText;
    }
    
    private void UpdateHpBar(int damage)
    {
        float hpPercent = ((float) pokemonInfo.pokemon.currentHp) / ((float) pokemonInfo.pokemon.maxHp);
        hpBar.transform.localScale = new Vector3(hpPercent, 1, 1);

        //Debug.Log($"Updated HP bar: {currentHp}/{pokemonInfo.pokemon.maxHp} ({hpBarSize} * {hpPercent * 100}%)");
    }

    private void UpdateDamageText(int damage)
    {
        bool heal = damage >= 0;
        damageText.text = $"{((heal) ? "+":"")}{damage}";
        damageText.color = (heal) ? healColor : damageColor;
        anim.SetTrigger("Damage");
    }

    private void PlayDamageAnimation()
    {
        anim.SetTrigger("Damage");
    }
}
