using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    Suicide,
    Melee,
    Ranged
}

public enum DamageSource
{
    DirectWeapon,
    PlayerChainExplosion,
    UnattributedExplosion
}

public readonly struct KillContext
{
    public KillContext(int weaponSlot, bool isHeadshot, DamageSource source)
    {
        WeaponSlot = Mathf.Clamp(weaponSlot, 1, 3);
        IsHeadshot = isHeadshot;
        Source = source;
    }

    public int WeaponSlot { get; }
    public bool IsHeadshot { get; }
    public DamageSource Source { get; }

    public bool IsPlayerAttributed => Source != DamageSource.UnattributedExplosion;

    public static KillContext Direct(int weaponSlot, bool isHeadshot)
    {
        return new KillContext(weaponSlot, isHeadshot, DamageSource.DirectWeapon);
    }

    public static KillContext Chain(int weaponSlot, bool isPlayerAttributed)
    {
        return new KillContext(
            weaponSlot,
            false,
            isPlayerAttributed ? DamageSource.PlayerChainExplosion : DamageSource.UnattributedExplosion);
    }
}

[DisallowMultipleComponent]
public sealed class ScoreSystem : MonoBehaviour
{
    private const int k_MaxComboLevel = 10;
    private const float k_ComboMultiplierStep = 0.2f;
    private const float k_MaxComboDuration = 3f;
    private const float k_MinComboDuration = 1.5f;
    private const float k_BasicCoefficient = 0.7f;
    private const float k_SingleConditionCoefficient = 1f;
    private const float k_PerfectCoefficient = 1.5f;

    private GameplayHUD m_hud;
    private float m_survivalAccumulator;
    private float m_comboExpiresAt;
    private int m_combatScore;
    private int m_survivalScore;
    private int m_totalKills;
    private int m_headshotKills;
    private int m_chainKills;
    private int m_maxComboLevel;
    private readonly int[] m_enemyKills = new int[3];
    private readonly int[] m_weaponKills = new int[3];
    private bool m_runComplete;

    public int ComboLevel { get; private set; }
    public int TotalScore => m_combatScore + m_survivalScore;
    public float ComboMultiplier => GetComboMultiplier(ComboLevel);

    public void Initialize(GameplayHUD hud)
    {
        RunResultStore.Clear();
        m_hud = hud;
        RefreshHud();
    }

    private void Update()
    {
        if (m_runComplete)
        {
            return;
        }

        m_survivalAccumulator += Time.deltaTime;
        if (m_survivalAccumulator >= 1f)
        {
            int elapsedSeconds = Mathf.FloorToInt(m_survivalAccumulator);
            m_survivalAccumulator -= elapsedSeconds;
            m_survivalScore += elapsedSeconds;
            m_hud?.RefreshScore(TotalScore);
        }

        if (ComboLevel > 0 && Time.time >= m_comboExpiresAt)
        {
            ComboLevel = 0;
            m_comboExpiresAt = 0f;
        }

        float remaining = ComboLevel > 0 ? Mathf.Max(0f, m_comboExpiresAt - Time.time) : 0f;
        float duration = GetComboDuration(ComboLevel);
        m_hud?.RefreshCombo(ComboLevel, ComboMultiplier, duration > 0f ? remaining / duration : 0f);
    }

    public void RegisterDirectKill(EnemyType enemyType, int weaponSlot, bool isHeadshot)
    {
        ComboLevel = Mathf.Min(k_MaxComboLevel, ComboLevel + 1);
        m_maxComboLevel = Mathf.Max(m_maxComboLevel, ComboLevel);
        RegisterKillStats(enemyType, weaponSlot, isHeadshot, false);
        float coefficient = GetDirectKillCoefficient(enemyType, weaponSlot, isHeadshot);
        int points = CalculateKillScore(enemyType, coefficient, ComboLevel);
        m_combatScore += points;
        m_comboExpiresAt = Time.time + GetComboDuration(ComboLevel);

        string reason = GetFeedbackReason(enemyType, weaponSlot, isHeadshot);
        m_hud?.ShowScoreFeedback(points, reason);
        RefreshHud();
    }

    public void RegisterChainBatch(IReadOnlyList<EnemyType> killedEnemies, int comboLevelSnapshot, int sourceWeaponSlot)
    {
        if (killedEnemies == null || killedEnemies.Count == 0)
        {
            return;
        }

        int scoringLevel = Mathf.Clamp(comboLevelSnapshot, 0, k_MaxComboLevel);
        int points = 0;
        for (int index = 0; index < killedEnemies.Count; index++)
        {
            points += CalculateKillScore(killedEnemies[index], k_BasicCoefficient, scoringLevel);
            RegisterKillStats(killedEnemies[index], sourceWeaponSlot, false, true);
        }

        m_combatScore += points;
        ComboLevel = Mathf.Min(k_MaxComboLevel, scoringLevel + killedEnemies.Count);
        m_maxComboLevel = Mathf.Max(m_maxComboLevel, ComboLevel);
        m_comboExpiresAt = Time.time + GetComboDuration(ComboLevel);
        m_hud?.ShowScoreFeedback(points, killedEnemies.Count > 1 ? $"CHAIN x{killedEnemies.Count}" : "CHAIN");
        RefreshHud();
    }

    public void CompleteRun(PlayerDeathCause deathCause)
    {
        if (m_runComplete)
        {
            return;
        }

        m_runComplete = true;
        int previousBest = RunResultStore.PersonalBest;
        bool isNewPersonalBest = TotalScore > previousBest;
        if (isNewPersonalBest)
        {
            RunResultStore.SavePersonalBest(TotalScore);
        }

        RunResultStore.Set(new RunResultSnapshot(
            TotalScore,
            m_survivalScore + m_survivalAccumulator,
            m_totalKills,
            m_enemyKills[(int)EnemyType.Suicide],
            m_enemyKills[(int)EnemyType.Melee],
            m_enemyKills[(int)EnemyType.Ranged],
            m_headshotKills,
            m_chainKills,
            m_maxComboLevel,
            m_weaponKills[0],
            m_weaponKills[1],
            m_weaponKills[2],
            Mathf.Max(previousBest, TotalScore),
            isNewPersonalBest,
            deathCause));
    }

    public static float GetDirectKillCoefficient(EnemyType enemyType, int weaponSlot, bool isHeadshot)
    {
        bool weaponMatches = weaponSlot == GetPreferredWeaponSlot(enemyType);
        if (weaponMatches && isHeadshot)
        {
            return k_PerfectCoefficient;
        }
        return weaponMatches || isHeadshot ? k_SingleConditionCoefficient : k_BasicCoefficient;
    }

    public static int CalculateKillScore(EnemyType enemyType, float coefficient, int comboLevel)
    {
        return Mathf.RoundToInt(GetBaseScore(enemyType) * coefficient * GetComboMultiplier(comboLevel));
    }

    public static float GetComboMultiplier(int comboLevel)
    {
        return comboLevel <= 0 ? 1f : 1f + (Mathf.Clamp(comboLevel, 1, k_MaxComboLevel) - 1) * k_ComboMultiplierStep;
    }

    public static float GetComboDuration(int comboLevel)
    {
        if (comboLevel <= 0)
        {
            return 0f;
        }

        float progress = (Mathf.Clamp(comboLevel, 1, k_MaxComboLevel) - 1f) / (k_MaxComboLevel - 1f);
        return Mathf.Lerp(k_MaxComboDuration, k_MinComboDuration, progress);
    }

    private static int GetBaseScore(EnemyType enemyType)
    {
        return enemyType == EnemyType.Ranged ? 150 : 100;
    }

    private static int GetPreferredWeaponSlot(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Melee => 2,
            EnemyType.Ranged => 3,
            _ => 1
        };
    }

    private static string GetFeedbackReason(EnemyType enemyType, int weaponSlot, bool isHeadshot)
    {
        bool weaponMatches = weaponSlot == GetPreferredWeaponSlot(enemyType);
        if (weaponMatches && isHeadshot)
        {
            return "PERFECT";
        }
        if (isHeadshot)
        {
            return "HEADSHOT";
        }
        return weaponMatches ? "WEAPON MATCH" : "BASIC";
    }

    private void RegisterKillStats(EnemyType enemyType, int weaponSlot, bool isHeadshot, bool isChain)
    {
        m_totalKills++;
        m_enemyKills[(int)enemyType]++;
        m_weaponKills[Mathf.Clamp(weaponSlot, 1, 3) - 1]++;
        if (isHeadshot)
        {
            m_headshotKills++;
        }
        if (isChain)
        {
            m_chainKills++;
        }
    }

    private void RefreshHud()
    {
        m_hud?.RefreshScore(TotalScore);
        float remaining = ComboLevel > 0 ? Mathf.Max(0f, m_comboExpiresAt - Time.time) : 0f;
        float duration = GetComboDuration(ComboLevel);
        m_hud?.RefreshCombo(ComboLevel, ComboMultiplier, duration > 0f ? remaining / duration : 0f);
    }

    [ContextMenu("Run Score System Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Suicide, 1, true), 1.5f));
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Melee, 2, false), 1f));
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Ranged, 1, false), 0.7f));
        Debug.Assert(CalculateKillScore(EnemyType.Suicide, 1.5f, 1) == 150);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 1.5f, 10) == 630);
        Debug.Assert(Mathf.Approximately(GetComboMultiplier(1), 1f));
        Debug.Assert(Mathf.Approximately(GetComboMultiplier(10), 2.8f));
        Debug.Assert(Mathf.Approximately(GetComboDuration(1), 3f));
        Debug.Assert(Mathf.Approximately(GetComboDuration(10), 1.5f));
    }
}
