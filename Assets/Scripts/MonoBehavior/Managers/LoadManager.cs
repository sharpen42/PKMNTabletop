using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class LoadManager : MonoBehaviour
{
    public static LoadManager Instance;

    public string PkmnCardsImagesFolder = "Images/PKMNCards";
    public string TypeIconsFolder = "Images/Types";
    public string PkmnCardsFileName = "Data/PKMNCards.txt";
    public string TypeChartFileName = "Data/TypeChart.txt";

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardParent;

    private Dictionary<string, Effect> effectMap;
    private Dictionary<string, Sprite> cardSprites = new();


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

    private void Start()
    {
        InitEffectMap();
        LoadCardSprites();
        LoadPkmnCards();
        LoadTypeChart();
    }

    private void InitEffectMap()
    {
        effectMap = new Dictionary<string, Effect>();

        void Add(string raw, Effect eff)
        {
            effectMap.Add(NormalizeEffectString(raw), eff);
        }

        Add("Nessun effetto.", Effect.NoEff);

        Add($"-{Effect.DEFVAR2} DEF all'avversario \n(min. 1).", Effect.DefDebuff2);
        Add($"-{Effect.ATKVAR2} ATK all'avversario \n(min. 1).", Effect.AtkDebuff2);
        Add($"-{Effect.SPEVAR2} SPE all'avversario \n(min. 1).", Effect.SpeDebuff2);

        Add($"+{Effect.DEFVAR2} DEF (max. {Pokemon.MAX_DEF})", Effect.DefBuff2);
        Add($"+{Effect.SPEVAR1} SPE (max. {Pokemon.MAX_SPE})", Effect.SpeBuff1);

        Add($"Recupera +{Effect.HPHEAL1} HP.", Effect.Heal1);
        Add($"Recupera +{Effect.HPHEAL2} HP.", Effect.Heal2);

        Add($"L'avversario è <b>Paralizzato</b> \n(-{Effect.PAR_SPEDEBUFF} SPE, min. 1).", Effect.Paralyze);
        Add($"L'avversario è <b>Scottato</b> \n(-{Effect.BRN_ATKDEBUFF} ATK, min. 1).", Effect.Burn);
        Add($"L'avversario è <b>Congelato</b>\n(-{Effect.FRZ_DEFDEBUFF} DEF, min. 1).", Effect.Freeze);
        Add($"L'avversario è <b>Addormentato</b>\n(non può attaccare \nfinché D6<{Effect.SLP_D6MIN+1}, o per {Effect.SLP_DURMAX} turni).", Effect.Sleep);
        Add($"L'avversario è <b>Avvelenato</b>\n (-{Effect.PSN_HPLOSS} HP ogni turno).", Effect.Poison);

        Add("Tira un D6: L'avversario è \n- 1/2: <b>Scottato</b> (-2 ATK)\n- 3/4: <b>Paralizzato</b> (-2 SPE)\n- 5/6: <b>Congelato</b> (-2 DEF)\n(per tutti: min. 1)", Effect.TriAttack);

        Add("<b>Superefficace</b> (DMGx2)\nse l'avversario è {pokemon/icons/types/ground.png}", Effect.SupereffOnGround);

        Add("Per 1 turno: \nNon puoi attaccare, cambiare Pokémon o usare Strumenti.", Effect.SkipTurn);

        Add("Scende a 0 HP (KO) dopo l'attacco.", Effect.AutoKO);

        Add("<b>In Lotta o fuori \n(una volta per turno):</b>\nUn Pokémon nella tua squadra recupera +20 HP.", Effect.SoftBoiled);

        Add("Copia Tipo, ATK, DEF e SPE dell'avversario.", Effect.Transform);

        // -> FORZATI A NoEff
        Add("Pesca 1 carta Strumento.", Effect.NoEff);
        Add("<b>Fuori dalla Lotta:</b>\nPuoi mandare Porygon KO\n (0 HP) per evitare una Lotta o uno Scambio.", Effect.NoEff);
    }

    private string NormalizeEffectString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        string s = input;

        s = s.Replace("<b>", "").Replace("</b>", "");
        s = s.Replace("\r", "");
        s = s.Replace("\\n", "\n"); // se arriva come stringa literal
        s = s.Trim();

        // normalizza spazi multipli
        while (s.Contains("  "))
            s = s.Replace("  ", " ");

        return s;
    }

    private void LoadPkmnCards()
    {
        //string path = Path.Combine(Application.dataPath, PkmnCardsFileName);
        TextAsset file = Resources.Load<TextAsset>("Data/PKMNCards");

        //if (!File.Exists(path))
        if (file == null)
        {
            //Debug.LogError("File PKMNCards non trovato: " + path);
            Debug.LogError("File PKMNCards non trovato: " + file.name);
            return;
        }

        //string[] lines = File.ReadAllLines(path);
        string[] lines = file.text.Split('\n');
        int cardCount = 0;

        for (int i = 0; i < lines.Length; i++) // skip header
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> cols = ParseCSVLine(lines[i]);

            try
            {
                string codename = cols[0];
                string name = cols[4];

                PKMNCard.Version version = ParseVersion(cols[3]);

                Pokemon.Type type = ParseType(cols[5]);

                int copies = ParseInt(cols[1]);
                int hp = ParseInt(cols[6]);
                int atk = ParseInt(cols[7]);
                int def = ParseInt(cols[8]);
                int spe = ParseInt(cols[9]);

                string moveName = cols[10];
                Pokemon.Type moveType = ParseType(cols[11]);

                int dmg = ParseInt(cols[12]);
                int d6 = ParseInt(cols[13]);

                Effect effect = ParseEffect(cols[14]);

                Sprite image = LoadCardSprite(codename);

                if(image == null)
                    Debug.LogWarning($"Immagine non trovata per {codename} ({name})");

                PKMNCard card = new PKMNCard(
                    codename,
                    name,
                    version,
                    copies,
                    type,
                    hp,
                    atk,
                    def,
                    spe,
                    dmg,
                    d6,
                    moveName,
                    moveType,
                    effect,
                    image
                );

                PokedeckManager.AddCard(codename, card);
                cardCount += copies;

                /*
                for(int j = 0; j < copies; j++)
                {
                    InstantiateCard(card, cardCount);
                    cardCount++;
                }
                */
            }
            catch (Exception e)
            {
                Debug.LogError($"Errore parsing riga {i}: {e.Message}");
            }
        }

        Debug.Log($"Carte caricate: {PokedeckManager.allCards.Count}");
        int c = 1;
        foreach (var key in PokedeckManager.allCards.Keys)
            Debug.Log($"Chiave disponibile: '{key}', #{(c++).ToString("000")}");
    }

    private void LoadTypeChart()
    {
        //string path = Path.Combine(Application.dataPath, TypeChartFileName); 
        TextAsset typeChart = Resources.Load<TextAsset>("Data/TypeChart");

        //if (!File.Exists(path))
        if(typeChart == null)
        {
            //Debug.LogError("File TypeChart non trovato: " + path);
            Debug.LogError("File TypeChart non trovato: " + typeChart.name);
            return;
        }

        //string[] lines = File.ReadAllLines(path);
        string[] lines = typeChart.text.Split('\n');

        // prima riga = header
        List<string> header = ParseCSVLine(lines[0]);

        // mappa colonna -> tipo difensore
        Dictionary<int, Pokemon.Type> colTypeMap = new();

        for (int i = 1; i < header.Count; i++)
        {
            if (TryParseType(header[i], out var type))
            {
                colTypeMap[i] = type;
            }
        }

        // parsing righe
        for (int r = 1; r < lines.Length; r++)
        {
            if (string.IsNullOrWhiteSpace(lines[r]))
                continue;

            List<string> cols = ParseCSVLine(lines[r]);

            if (!TryParseType(cols[0], out var attackerType))
                continue;

            foreach (var kvp in colTypeMap)
            {
                int colIndex = kvp.Key;
                Pokemon.Type defenderType = kvp.Value;

                if (colIndex >= cols.Count)
                    continue;

                float value = ParseFloat(cols[colIndex]);

                Pokemon.SetTypeEffectiveness(attackerType, defenderType, value);
            }
        }

        Debug.Log("TypeChart caricata correttamente");
        PrintTypeChart();
    }
    private void PrintTypeChart()
    {
        int typeNum = (int)Pokemon.Type._err;
        string header = "ATK\\DEF\t";
        for (int def = 0; def < typeNum; def++)
            header += $"{(Pokemon.Type)def}\t";
        Debug.Log(header);

        for (int atk = 0; atk < typeNum; atk++)
        {
            string row = $"{(Pokemon.Type)atk}\t";
            for (int def = 0; def < typeNum; def++)
                row += $"{Pokemon.GetEffectiveness((Pokemon.Type)atk, (Pokemon.Type)def)}\t";
            Debug.Log(row);
        }
    }

    private float ParseFloat(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 1f;

        value = value.Replace("\"", "").Trim();
        value = value.Replace(",", "."); // fondamentale

        if (float.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        return 1f;
    }

    private bool TryParseType(string t, out Pokemon.Type type)
    {
        return Enum.TryParse(t, true, out type) && type != Pokemon.Type._err;
    }

    private void LoadCardSprites()
    {
        Sprite[] allSprites = Resources.LoadAll<Sprite>(PkmnCardsImagesFolder);
        foreach (Sprite sprite in allSprites)
            cardSprites[sprite.name] = sprite;

        Debug.Log($"Sprite carte caricati: {cardSprites.Count}");
    }

    public Sprite LoadCardSprite(string codename)
    {
        foreach (var key in cardSprites.Keys)
        {
            if (key.EndsWith(codename))
                return cardSprites[key];
        }

        Debug.LogWarning($"Sprite non trovato per: {codename}");
        return null;
    }

    public static Sprite LoadTypeSprite(Pokemon.Type type)
    {
        string path = Path.Combine(Instance.TypeIconsFolder, type.ToString().ToLower());

        // IMPORTANTISSIMO: Resources vuole "/" sempre
        path = path.Replace("\\", "/");

        //Debug.Log("Loading sprite at: " + path);
        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
            Debug.LogWarning("Sprite tipo non trovato (Resources path): " + path);

        return sprite;
    }

    private List<string> ParseCSVLine(string line)
    {
        List<string> result = new List<string>();

        bool inQuotes = false;
        string current = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        result.Add(current);
        return result;
    }

    private int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "--")
            return 0;

        value = value.Replace("\"", "").Trim();

        return int.TryParse(value, out int result) ? result : 0;
    }

    private PKMNCard.Version ParseVersion(string v)
    {
        return v switch
        {
            "A" => PKMNCard.Version.A,
            "B" => PKMNCard.Version.B,
            "C" => PKMNCard.Version.C,
            "S" => PKMNCard.Version.S,
            _ => PKMNCard.Version.A
        };
    }

    private Pokemon.Type ParseType(string t)
    {
        return Enum.TryParse(t, true, out Pokemon.Type result)
            ? result
            : Pokemon.Type.Normal;
    }

    private Effect ParseEffect(string raw)
    {
        string key = NormalizeEffectString(raw);

        if (effectMap.TryGetValue(key, out var effect))
            return effect;

        Debug.LogWarning("Effetto non trovato: " + raw);
        return Effect.NoEff;
    }
}
