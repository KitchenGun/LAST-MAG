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
    private const int k_HeadshotBonus = 30;
    private const int k_SwapKillBonus = 70;
    private const int k_ComboBonusStep = 10;
    private const float k_ComboDuration = 5f;
    private const float k_SwapKillWindow = 2f;

    private GameplayHUD m_hud;
    private float m_survivalAccumulator;
    private float m_comboExpiresAt;
    private int m_combatScore;
    private int m_survivalScore;
    private int m_totalKills;
    private int m_headshotKills;
    private int m_chainKills;
    private int m_skillKills;
    private int m_maxComboCount;
    private readonly int[] m_enemyKills = new int[3];
    private readonly int[] m_weaponKills = new int[4];
    private bool m_bulletTimeActive;
    private bool m_runComplete;
    private WeaponId m_lastDirectKillWeapon = WeaponId.Unknown;
    private float m_lastDirectKillTime = float.NegativeInfinity;

    public int ComboCount { get; private set; }
    public int TotalScore => m_combatScore + m_survivalScore;
    public bool IsBulletTimeActive => m_bulletTimeActive;

    public void Initialize(GameplayHUD hud)
    {
        RunResultStore.ClearResult();
        m_hud = hud;
        m_bulletTimeActive = false;
        m_survivalAccumulator = 0f;
        m_survivalScore = 0;
        ComboCount = 0;
        m_comboExpiresAt = 0f;
        ResetSwapCandidate();
        m_hud?.RefreshSurvivalTime(0f);
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
            m_hud?.RefreshSurvivalTime(m_survivalScore + m_survivalAccumulator);
        }

        ExpireCombo(Time.unscaledTime);
        m_hud?.RefreshCombo(ComboCount, GetComboRemainingSeconds(Time.unscaledTime));
    }

    public void RegisterDirectKill(EnemyType enemyType, WeaponId weapon, bool isHeadshot)
    {
        float now = Time.unscaledTime;
        ExpireCombo(now);
        bool isSwapKill = IsSwapKill(m_lastDirectKillWeapon, weapon, now - m_lastDirectKillTime);
        bool isSkillKill = m_bulletTimeActive;
        AdvanceCombo(now);
        int points = CalculateKillScore(
            enemyType, ComboCount, isHeadshot, isSwapKill, isSkillKill);
        RegisterKillStats(enemyType, weapon, isHeadshot, false, isSkillKill);
        m_combatScore += points;
        m_lastDirectKillWeapon = weapon;
        m_lastDirectKillTime = now;
        int baseScore = GetBaseScore(enemyType);
        if (isSkillKill)
        {
            m_hud?.ShowScoreFeedback(baseScore * 2, "SKILL KILL");
        }
        if (isSwapKill)
        {
            m_hud?.ShowScoreFeedback(k_SwapKillBonus, "SWAP KILL");
        }
        if (isHeadshot)
        {
            m_hud?.ShowScoreFeedback(k_HeadshotBonus, "HEADSHOT");
        }
        m_hud?.ShowScoreFeedback(baseScore + GetComboBonus(ComboCount), "ENEMY KILLED");
        RefreshHud();
    }

    public void RegisterSkillBatch(IReadOnlyList<EnemyType> killedEnemies)
    {
        RegisterBatch(killedEnemies, WeaponId.Unknown, false, true);
    }

    public void RegisterChainBatch(IReadOnlyList<EnemyType> killedEnemies,
        WeaponId sourceWeapon, bool isClassSkillAttributed = false)
    {
        RegisterBatch(killedEnemies, sourceWeapon, true, isClassSkillAttributed);
    }

    private void RegisterBatch(IReadOnlyList<EnemyType> killedEnemies,
        WeaponId weapon, bool isChain, bool isClassSkillAttributed)
    {
        if (killedEnemies == null || killedEnemies.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        ExpireCombo(now);
        ResetSwapCandidate();
        int points = 0;
        for (int index = 0; index < killedEnemies.Count; index++)
        {
            AdvanceCombo(now);
            points += CalculateKillScore(
                killedEnemies[index], ComboCount, false, false, isClassSkillAttributed);
            RegisterKillStats(killedEnemies[index], weapon, false, isChain, isClassSkillAttributed);
        }

        m_combatScore += points;
        string label = GetBatchFeedbackLabel(
            killedEnemies.Count, isChain, isClassSkillAttributed);
        m_hud?.ShowScoreFeedback(points, label);
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
            m_maxComboCount,
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

    public static int CalculateKillScore(
        EnemyType enemyType,
        int comboCount,
        bool isHeadshot,
        bool isSwapKill,
        bool isSkillKill)
    {
        int baseScore = GetBaseScore(enemyType);
        return baseScore
            + (isHeadshot ? k_HeadshotBonus : 0)
            + (isSwapKill ? k_SwapKillBonus : 0)
            + GetComboBonus(comboCount)
            + (isSkillKill ? baseScore * 2 : 0);
    }

    public static int GetBaseScore(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Suicide => 50,
            EnemyType.Melee => 70,
            EnemyType.Ranged => 100,
            _ => 0
        };
    }

    private static int GetComboBonus(int comboCount)
    {
        return Mathf.Max(0, comboCount - 1) * k_ComboBonusStep;
    }

    private static bool IsSwapKill(WeaponId previousWeapon, WeaponId currentWeapon, float elapsedSeconds)
    {
        return IsScoringWeapon(previousWeapon)
            && IsScoringWeapon(currentWeapon)
            && previousWeapon != currentWeapon
            && elapsedSeconds >= 0f
            && elapsedSeconds <= k_SwapKillWindow;
    }

    private static bool IsScoringWeapon(WeaponId weapon)
    {
        return weapon is >= WeaponId.Pistol and <= WeaponId.DMR;
    }

    private static string GetBatchFeedbackLabel(
        int killCount, bool isChain, bool isSkillKill)
    {
        string suffix = killCount > 1 ? $" x{killCount}" : string.Empty;
        if (isSkillKill && isChain)
        {
            return $"SKILL CHAIN{suffix}";
        }
        if (isSkillKill)
        {
            return $"SKILL KILL{suffix}";
        }
        if (isChain)
        {
            return $"CHAIN KILL{suffix}";
        }
        return $"ENEMY KILLED{suffix}";
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
        if (isSkill)
        {
            m_skillKills++;
        }
    }

    private void AdvanceCombo(float now)
    {
        ComboCount++;
        m_maxComboCount = Mathf.Max(m_maxComboCount, ComboCount);
        m_comboExpiresAt = now + k_ComboDuration;
    }

    private void ExpireCombo(float now)
    {
        if (!HasComboExpired(ComboCount, m_comboExpiresAt, now))
        {
            return;
        }

        ComboCount = 0;
        m_comboExpiresAt = 0f;
        ResetSwapCandidate();
    }

    private static bool HasComboExpired(int comboCount, float expiryTime, float now)
    {
        return comboCount > 0 && now >= expiryTime;
    }

    private float GetComboRemainingSeconds(float now)
    {
        return ComboCount > 0 ? Mathf.Max(0f, m_comboExpiresAt - now) : 0f;
    }

    private void ResetSwapCandidate()
    {
        m_lastDirectKillWeapon = WeaponId.Unknown;
        m_lastDirectKillTime = float.NegativeInfinity;
    }

    private void RefreshHud()
    {
        m_hud?.RefreshScore(TotalScore);
        m_hud?.RefreshCombo(ComboCount, GetComboRemainingSeconds(Time.unscaledTime));
    }

    [ContextMenu("Run Score System Self Check")]
    private void RunSelfCheck()
    {
        Debug.Assert(GetBaseScore(EnemyType.Suicide) == 50);
        Debug.Assert(GetBaseScore(EnemyType.Melee) == 70);
        Debug.Assert(GetBaseScore(EnemyType.Ranged) == 100);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 1, false, false, false) == 100);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 2, true, true, false) == 210);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 1, false, false, true) == 300);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 2, true, true, true) == 410);
        Debug.Assert(CalculateKillScore(EnemyType.Ranged, 100, false, false, false) == 1090);
        Debug.Assert(GetBaseScore(EnemyType.Ranged) + GetComboBonus(2) == 110);
        int skillBatch = CalculateKillScore(EnemyType.Suicide, 1, false, false, true)
            + CalculateKillScore(EnemyType.Melee, 2, false, false, true)
            + CalculateKillScore(EnemyType.Ranged, 3, false, false, true);
        int regularChainBatch = CalculateKillScore(EnemyType.Suicide, 1, false, false, false)
            + CalculateKillScore(EnemyType.Melee, 2, false, false, false)
            + CalculateKillScore(EnemyType.Ranged, 3, false, false, false);
        Debug.Assert(skillBatch == 690);
        Debug.Assert(regularChainBatch == 250);
        Debug.Assert(IsSwapKill(WeaponId.Pistol, WeaponId.Rifle, 1.999f));
        Debug.Assert(IsSwapKill(WeaponId.Pistol, WeaponId.Rifle, 2f));
        Debug.Assert(!IsSwapKill(WeaponId.Pistol, WeaponId.Rifle, 2.001f));
        Debug.Assert(!IsSwapKill(WeaponId.Pistol, WeaponId.Pistol, 1f));
        Debug.Assert(!IsSwapKill(WeaponId.Unknown, WeaponId.Pistol, 1f));
        Debug.Assert(!HasComboExpired(1, 5f, 4.999f));
        Debug.Assert(HasComboExpired(1, 5f, 5f));
        Debug.Assert(KillContext.Skill(WeaponId.Rifle).IsClassSkillAttributed);
        Debug.Assert(KillContext.Chain(WeaponId.Rifle, true, true).IsClassSkillAttributed);
        Debug.Assert(!KillContext.Chain(WeaponId.Rifle, true).IsClassSkillAttributed);
        Debug.Assert(!KillContext.Chain(WeaponId.Rifle, false).IsPlayerAttributed);
        Debug.Assert(GetBatchFeedbackLabel(3, true, false) == "CHAIN KILL x3");
        Debug.Assert(GetBatchFeedbackLabel(3, true, true) == "SKILL CHAIN x3");
    }
}
