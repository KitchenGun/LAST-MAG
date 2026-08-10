using UnityEngine;

public enum PlayerDeathCause
{
    Unknown,
    SuicideBacteriophage,
    MeleeHumanoid,
    RangedHumanoid
}

public sealed class RunResultSnapshot
{
    public RunResultSnapshot(
        int finalScore,
        float survivalTime,
        int totalKills,
        int suicideKills,
        int meleeKills,
        int rangedKills,
        int headshotKills,
        int chainKills,
        int maxComboLevel,
        int pistolKills,
        int shotgunKills,
        int rifleKills,
        int personalBest,
        bool isNewPersonalBest,
        PlayerDeathCause deathCause)
    {
        FinalScore = finalScore;
        SurvivalTime = survivalTime;
        TotalKills = totalKills;
        SuicideKills = suicideKills;
        MeleeKills = meleeKills;
        RangedKills = rangedKills;
        HeadshotKills = headshotKills;
        ChainKills = chainKills;
        MaxComboLevel = maxComboLevel;
        PistolKills = pistolKills;
        ShotgunKills = shotgunKills;
        RifleKills = rifleKills;
        PersonalBest = personalBest;
        IsNewPersonalBest = isNewPersonalBest;
        DeathCause = deathCause;
    }

    public int FinalScore { get; }
    public float SurvivalTime { get; }
    public int TotalKills { get; }
    public int SuicideKills { get; }
    public int MeleeKills { get; }
    public int RangedKills { get; }
    public int HeadshotKills { get; }
    public int ChainKills { get; }
    public int MaxComboLevel { get; }
    public int PistolKills { get; }
    public int ShotgunKills { get; }
    public int RifleKills { get; }
    public int PersonalBest { get; }
    public bool IsNewPersonalBest { get; }
    public PlayerDeathCause DeathCause { get; }
}

public static class RunResultStore
{
    private const string k_PersonalBestKey = "Gulag.PersonalBestScore";

    public static RunResultSnapshot Current { get; private set; }
    public static int PersonalBest => PlayerPrefs.GetInt(k_PersonalBestKey, 0);

    public static void Set(RunResultSnapshot result)
    {
        Current = result;
    }

    public static void SavePersonalBest(int score)
    {
        if (score <= PersonalBest)
        {
            return;
        }

        PlayerPrefs.SetInt(k_PersonalBestKey, score);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        Current = null;
    }
}
