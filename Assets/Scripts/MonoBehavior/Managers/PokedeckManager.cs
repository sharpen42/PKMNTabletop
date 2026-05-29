using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PokedeckManager : MonoBehaviour
{
    public static PokedeckManager Instance;

    public static Dictionary<string, PKMNCard> allCards = new();


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void AddCard(string Key, PKMNCard card)
    {
        var key = SanitizeKey(Key);
        if (!allCards.ContainsKey(key))
        {
            allCards.Add(key, card);
            Debug.Log($"Carta aggiunta: '{key}'");
        }
        else
        {
            Debug.LogWarning($"Carta duplicata: '{key}'");
        }
    }

    public static PKMNCard GetCard(string Key)
    {
        var key = SanitizeKey(Key);
        if (allCards.TryGetValue(key, out var card))
            return card;

        Debug.LogError($"Carta non trovata: '{key}'");
        return null;
    }

    public static bool CardExists(string Key)
    {
        var key = SanitizeKey(Key);
        //Debug.Log($"allCards count: {allCards.Values.Count}, key '{key}'");
        //return allCards.Keys.Any(k => stringCompare(k, key));
        return allCards.ContainsKey(key);
    }

    public static bool TryGetCard(string Key, out PKMNCard card)
    {
        var key = SanitizeKey(Key);
        card = null;

        //Debug.Log($"allCards count: {allCards.Values.Count}, key '{key}'");
        //return allCards.Keys.Any(k => stringCompare(k, key));
        if (allCards.ContainsKey(key))
        {
            card = allCards[key];
            return true;
        }

        return false;
    }

    public static void PrintCard(PKMNCard card)
    {
        if (card != null)
        {
            Debug.Log($"Carta: {card.cardName}, Tipo: {card.type}, HP: {card.stats[(int)PKMNCard.Stats.HP]}, ATK: {card.stats[(int)PKMNCard.Stats.ATK]}, DEF: {card.stats[(int)PKMNCard.Stats.DEF]}, SPE: { card.stats[(int)PKMNCard.Stats.SPE]}");
        } 
        {
            Debug.LogWarning("Carta null non stampata.");
        }
    }

    public static void ClearDeck()
    {
        allCards.Clear();
        Debug.Log("Pokedeck svuotato.");
    }

    private static string SanitizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        // rimuove whitespace, newline, zero-width spaces e altri caratteri invisibili
        return new string(key.Where(c => !char.IsControl(c) && c != '\u200B' && c != '\u200C' && c != '\u200D' && c != '\uFEFF').ToArray()).Trim();
    }
}
