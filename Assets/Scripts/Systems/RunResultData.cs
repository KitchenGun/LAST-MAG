using UnityEngine;

public enum PlayerDeathCause
{
    Unknown,
    SuicideBacteriophage,
    MeleeHumanoid,
    RangedHumanoid,
    GrenadeSelfDamage,
    RocketSelfDamage
}

public enum PlayerClassId
{
    Unknown,
    Grenadier,
    Engineer,
    Sniper
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
        int maxComboCount,
        int pistolKills,
        int shotgunKills,
        int rifleKills,
        int personalBest,
        bool isNewPersonalBest,
        PlayerClassId playerClass,
        PlayerDeathCause deathCause,
        int dmrKills = 0,
        int skillKills = 0)
    {
        FinalScore = finalScore;
        SurvivalTime = survivalTime;
        TotalKills = totalKills;
        SuicideKills = suicideKills;
        MeleeKills = meleeKills;
        RangedKills = rangedKills;
        HeadshotKills = headshotKills;
        ChainKills = chainKills;
        MaxComboCount = maxComboCount;
        PistolKills = pistolKills;
        ShotgunKills = shotgunKills;
        RifleKills = rifleKills;
        PersonalBest = personalBest;
        IsNewPersonalBest = isNewPersonalBest;
        PlayerClass = playerClass;
        DeathCause = deathCause;
        DmrKills = dmrKills;
        SkillKills = skillKills;
    }

    public int FinalScore { get; }
    public float SurvivalTime { get; }
    public int TotalKills { get; }
    public int SuicideKills { get; }
    public int MeleeKills { get; }
    public int RangedKills { get; }
    public int HeadshotKills { get; }
    public int ChainKills { get; }
    public int MaxComboCount { get; }
    public int PistolKills { get; }
    public int ShotgunKills { get; }
    public int RifleKills { get; }
    public int PersonalBest { get; }
    public bool IsNewPersonalBest { get; }
    public PlayerClassId PlayerClass { get; }
    public PlayerDeathCause DeathCause { get; }
    public int DmrKills { get; }
    public int SkillKills { get; }
}

public static class RunResultStore
{
    private const string k_PersonalBestKey = "Gulag.PersonalBestScore";

    public static RunResultSnapshot Current { get; private set; }
    public static PlayerClassId SelectedClass { get; private set; }
    public static int PersonalBest => PlayerPrefs.GetInt(k_PersonalBestKey, 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
        SelectedClass = PlayerClassId.Unknown;
    }

    public static void SelectClass(PlayerClassId playerClass)
    {
        SelectedClass = playerClass;
    }

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

    public static void ClearResult()
    {
        Current = null;
    }

    public static void Clear()
    {
        Current = null;
        SelectedClass = PlayerClassId.Unknown;
    }

    public static string GetPlayerClassName(PlayerClassId playerClass)
    {
        return playerClass switch
        {
            PlayerClassId.Grenadier => "GRENADIER",
            PlayerClassId.Engineer => "ENGINEER",
            PlayerClassId.Sniper => "SNIPER",
            _ => "UNKNOWN"
        };
    }

    public static WeaponId GetPrimaryWeapon(PlayerClassId playerClass)
    {
        return playerClass switch
        {
            PlayerClassId.Grenadier => WeaponId.Rifle,
            PlayerClassId.Engineer => WeaponId.Shotgun,
            PlayerClassId.Sniper => WeaponId.DMR,
            _ => WeaponId.Unknown
        };
    }
}
