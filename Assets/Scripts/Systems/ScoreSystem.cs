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
    PlayerSkill,
    PlayerChainExplosion,
    PlayerSkillChainExplosion,
    UnattributedExplosion
}

public readonly struct KillContext
{
    public KillContext(WeaponId weapon, bool isHeadshot, DamageSource source)
    {
        Weapon = weapon;
        IsHeadshot = isHeadshot;
        Source = source;
    }

    public WeaponId Weapon { get; }
    public bool IsHeadshot { get; }
    public DamageSource Source { get; }
    public bool IsPlayerAttributed => Source != DamageSource.UnattributedExplosion;
    public bool IsClassSkillAttributed => Source is DamageSource.PlayerSkill or DamageSource.PlayerSkillChainExplosion;

    public static KillContext Direct(WeaponId weapon, bool isHeadshot)
    {
        return new KillContext(weapon, isHeadshot, DamageSource.DirectWeapon);
    }

    public static KillContext Skill(WeaponId equippedWeapon)
    {
        return new KillContext(equippedWeapon, false, DamageSource.PlayerSkill);
    }

    public static KillContext Chain(WeaponId weapon, bool isPlayerAttributed, bool isClassSkillAttributed = false)
    {
        DamageSource source = !isPlayerAttributed
            ? DamageSource.UnattributedExplosion
            : isClassSkillAttributed ? DamageSource.PlayerSkillChainExplosion : DamageSource.PlayerChainExplosion;
        return new KillContext(weapon, false, source);
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
    private int m_skillKills;
    private int m_maxComboLevel;
    private readonly int[] m_enemyKills = new int[3];
    private readonly int[] m_weaponKills = new int[4];
    private bool m_bulletTimeActive;
    private bool m_runComplete;

    public int ComboLevel { get; private set; }
    public int TotalScore => m_combatScore + m_survivalScore;
    public float ComboMultiplier => GetComboMultiplier(ComboLevel);

    public void Initialize(GameplayHUD hud)
    {
        RunResultStore.ClearResult();
        m_hud = hud;
        m_bulletTimeActive = false;
        RefreshHud();
    }

    public void SetBulletTimeActive(bool active)
    {
        m_bulletTimeActive = active;
    }

    private void Update()
    {
        if (m_runComplete)
        {
            return;
        }

        m_survivalAccumulator += Time.unscaledDeltaTime;
        if (m_survivalAccumulator >= 1f)
        {
            int elapsedSeconds = Mathf.FloorToInt(m_survivalAccumulator);
            m_survivalAccumulator -= elapsedSeconds;
            m_survivalScore += elapsedSeconds;
            m_hud?.RefreshScore(TotalScore);
        }

        if (ComboLevel > 0 && Time.unscaledTime >= m_comboExpiresAt)
        {
            ComboLevel = 0;
            m_comboExpiresAt = 0f;
        }

        float remaining = ComboLevel > 0 ? Mathf.Max(0f, m_comboExpiresAt - Time.unscaledTime) : 0f;
        float duration = GetComboDuration(ComboLevel);
        m_hud?.RefreshCombo(ComboLevel, ComboMultiplier, duration > 0f ? remaining / duration : 0f);
    }

    public void RegisterDirectKill(EnemyType enemyType, WeaponId weapon, bool isHeadshot)
    {
        ComboLevel = Mathf.Min(k_MaxComboLevel, ComboLevel + 1);
        m_maxComboLevel = Mathf.Max(m_maxComboLevel, ComboLevel);
        RegisterKillStats(enemyType, weapon, isHeadshot, false, false);
        float coefficient = GetDirectKillCoefficient(enemyType, weapon, isHeadshot);
        int points = CalculateKillScore(enemyType, coefficient, ComboLevel);
        m_combatScore += points;
        m_comboExpiresAt = Time.unscaledTime + GetComboDuration(ComboLevel);
        m_hud?.ShowScoreFeedback(points, GetFeedbackReason(enemyType, weapon, isHeadshot));
        RefreshHud();
    }

    public void RegisterSkillBatch(IReadOnlyList<EnemyType> killedEnemies, int comboLevelSnapshot)
    {
        RegisterBatch(killedEnemies, comboLevelSnapshot, WeaponId.Unknown, k_SingleConditionCoefficient, false, true);
    }

    public void RegisterChainBatch(IReadOnlyList<EnemyType> killedEnemies, int comboLevelSnapshot,
        WeaponId sourceWeapon, bool isClassSkillAttributed = false)
    {
        RegisterBatch(killedEnemies, comboLevelSnapshot, sourceWeapon, k_BasicCoefficient, true, isClassSkillAttributed);
    }

    private void RegisterBatch(IReadOnlyList<EnemyType> killedEnemies, int comboLevelSnapshot,
        WeaponId weapon, float coefficient, bool isChain, bool isClassSkillAttributed)
    {
        if (killedEnemies == null || killedEnemies.Count == 0)
        {
            return;
        }

        int scoringLevel = Mathf.Clamp(comboLevelSnapshot, 0, k_MaxComboLevel);
        int points = 0;
        for (int index = 0; index < killedEnemies.Count; index++)
        {
            points += CalculateKillScore(killedEnemies[index], coefficient, scoringLevel);
            RegisterKillStats(killedEnemies[index], weapon, false, isChain, isClassSkillAttributed);
        }

        m_combatScore += points;
        ComboLevel = Mathf.Min(k_MaxComboLevel, scoringLevel + killedEnemies.Count);
        m_maxComboLevel = Mathf.Max(m_maxComboLevel, ComboLevel);
        m_comboExpiresAt = Time.unscaledTime + GetComboDuration(ComboLevel);
        string reason = isChain ? (killedEnemies.Count > 1 ? $"CHAIN x{killedEnemies.Count}" : "CHAIN") : "SKILL";
        m_hud?.ShowScoreFeedback(points, reason);
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
            m_weaponKills[(int)WeaponId.Pistol - 1],
            m_weaponKills[(int)WeaponId.Shotgun - 1],
            m_weaponKills[(int)WeaponId.Rifle - 1],
            Mathf.Max(previousBest, TotalScore),
            isNewPersonalBest,
            RunResultStore.SelectedClass,
            deathCause,
            m_weaponKills[(int)WeaponId.DMR - 1],
            m_skillKills));
    }

    public static float GetDirectKillCoefficient(EnemyType enemyType, WeaponId weapon, bool isHeadshot)
    {
        bool weaponMatches = WeaponMatches(enemyType, weapon);
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

    private static bool WeaponMatches(EnemyType enemyType, WeaponId weapon)
    {
        return enemyType switch
        {
            EnemyType.Suicide => weapon == WeaponId.Pistol,
            EnemyType.Melee => weapon == WeaponId.Shotgun,
            EnemyType.Ranged => weapon == WeaponId.Rifle || weapon == WeaponId.DMR,
            _ => false
        };
    }

    private static string GetFeedbackReason(EnemyType enemyType, WeaponId weapon, bool isHeadshot)
    {
        bool weaponMatches = WeaponMatches(enemyType, weapon);
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

    private void RegisterKillStats(EnemyType enemyType, WeaponId weapon, bool isHeadshot, bool isChain, bool isSkill)
    {
        m_totalKills++;
        m_enemyKills[(int)enemyType]++;
        int weaponIndex = (int)weapon - 1;
        if (weaponIndex >= 0 && weaponIndex < m_weaponKills.Length)
        {
            m_weaponKills[weaponIndex]++;
        }
        if (isHeadshot)
        {
            m_headshotKills++;
        }
        if (isChain)
        {
            m_chainKills++;
        }
        if (isSkill || m_bulletTimeActive)
        {
            m_skillKills++;
        }
    }

    private void RefreshHud()
    {
        m_hud?.RefreshScore(TotalScore);
        float remaining = ComboLevel > 0 ? Mathf.Max(0f, m_comboExpiresAt - Time.unscaledTime) : 0f;
        float duration = GetComboDuration(ComboLevel);
        m_hud?.RefreshCombo(ComboLevel, ComboMultiplier, duration > 0f ? remaining / duration : 0f);
    }

    [ContextMenu("Run Score System Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Suicide, WeaponId.Pistol, true), 1.5f));
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Melee, WeaponId.Shotgun, false), 1f));
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Ranged, WeaponId.DMR, false), 1f));
        Debug.Assert(Mathf.Approximately(GetDirectKillCoefficient(EnemyType.Ranged, WeaponId.Pistol, false), 0.7f));
        Debug.Assert(CalculateKillScore(EnemyType.Suicide, 1.5f, 1) == 150);
        Debug.Assert(Mathf.Approximately(GetComboMultiplier(10), 2.8f));
        Debug.Assert(Mathf.Approximately(GetComboDuration(10), 1.5f));
        Debug.Assert(KillContext.Skill(WeaponId.Rifle).IsClassSkillAttributed);
        Debug.Assert(KillContext.Chain(WeaponId.Rifle, true, true).IsClassSkillAttributed);
        Debug.Assert(!KillContext.Chain(WeaponId.Rifle, true).IsClassSkillAttributed);
    }
}
