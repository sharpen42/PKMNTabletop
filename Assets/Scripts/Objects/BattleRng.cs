public static class BattleRng
{
    private static System.Random rng;

    public static int GenerateSeed()
    {
        return UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }

    public static void SetSeed(int seed)
    {
        rng = new System.Random(seed);
    }

    public static int Roll(int min, int max)
    {
        return rng.Next(min, max);
    }
}
