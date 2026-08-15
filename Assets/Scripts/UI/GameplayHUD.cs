using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed class GameplayHUD : MonoBehaviour
{
    private const int k_WeaponSlotCount = 2;
    private const int k_HudRowCount = 3;
    private const int k_PickupPopupPoolSize = 4;
    private const int k_ScoreFeedbackPoolSize = 6;
    private const int k_ComboBulletCount = 5;
    private const float k_EmptyAmmoFeedbackDuration = 0.2f;
    private const float k_EmptyAmmoBlinkInterval = 0.2f;
    private const float k_InactiveWeaponAlpha = 0.4f;
    private const float k_ActiveWeaponFontSize = 22f;
    private const float k_InactiveWeaponFontSize = 17f;
    private const float k_PickupPopupDuration = 2.25f;
    private const float k_PickupPopupFadeDuration = 0.3f;
    private const float k_PickupPopupSpacing = 38f;
    private const float k_PickupPopupMoveSpeed = 260f;
    private const float k_ScoreFeedbackDuration = 2.25f;
    private const float k_ScoreFeedbackFadeDuration = 0.3f;
    private const float k_ScoreFeedbackSpacing = 32f;
    private const float k_ComboDuration = 5f;
    private const float k_HitMarkerDuration = 0.12f;
    private const float k_HitMarkerFadeDuration = 0.04f;
    private const float k_HitMarkerPulseDuration = 0.06f;
    private const float k_SkillReadyBlinkInterval = 0.5f;
    private const float k_ComboBulletPitch = 24.5f;
    private const float k_ComboBulletVisibleHeight = 23.5f;
    private const float k_ComboClipVisibleHeightRatio = 0.975f;
    private const float k_ComboStackVisibleHeight =
        k_ComboBulletVisibleHeight + (k_ComboBulletCount - 1) * k_ComboBulletPitch;
    private const float k_MaxDamageVignetteAlpha = 0.68f;
    private const float k_DeathDamageVignetteAlpha = 0.92f;
    private const float k_DeathTintAlpha = 0.22f;
    private const float k_DamageVignetteIncreaseSpeed = 3.5f;
    private const float k_DamageVignetteRecoverySpeed = 1.2f;
    private const float k_DmrScopeVignetteAlpha = 0.68f;
    private const float k_DmrScopeTransitionDuration = 0.12f;
    private static readonly Vector2 k_PickupPopupBasePosition = new(-170f, 48f);
    private static readonly Vector2 k_ComboBulletStartPosition = new(86f, -55f);
    private static readonly Vector2 k_ComboBulletSize = new(128f, 32f);
    private static readonly Vector2 k_ComboClipPosition = new(24f, -104f);
    private static readonly Vector2 k_ComboClipSize =
        new(25f, k_ComboStackVisibleHeight / k_ComboClipVisibleHeightRatio);
    private static readonly Color k_MaxComboColor = new Color32(234, 64, 71, 255);
    private static readonly Vector2 k_WeaponBorderSize = new(500f, 54f);
    private static readonly Vector2 k_WeaponBackgroundSize = new(496f, 50f);
    private static readonly Color k_ActiveBorderColor = new(1f, 1f, 1f, 0.65f);
    private static readonly Color k_InactiveBorderColor = new(0.28f, 0.34f, 0.37f, k_InactiveWeaponAlpha);
    private static readonly Color k_WeaponBackgroundColor = new(0.025f, 0.035f, 0.045f, 1f);
    private static readonly Color k_SkillInactiveColor = new(0.55f, 0.62f, 0.66f, 0.75f);
    private static readonly Vector2 k_WeaponSilhouetteSize = new(136f, 44f);
    private static readonly Vector2 k_DmrSilhouetteSize = new(136f, 136f);
    private static readonly Vector2 k_SquareSkillSilhouetteSize = new(56f, 56f);
    private static readonly Vector2 k_RocketSkillSilhouetteSize = new(136f, 136f);
    private static readonly Color k_NormalHitMarkerColor = Color.white;
    private static readonly Color k_HeadshotHitMarkerColor = new Color32(255, 210, 72, 255);
    private static readonly Color k_KillHitMarkerColor = new Color32(234, 64, 71, 255);
    private static readonly Vector2 k_NormalHitMarkerSize = new(44f, 44f);
    private static readonly Vector2 k_HeadshotHitMarkerSize = new(52f, 52f);
    private static readonly Vector2 k_KillHitMarkerSize = new(60f, 60f);

    [SerializeField] private TMP_FontAsset m_font;
    [SerializeField] private Sprite m_dmrSilhouette;
    [SerializeField] private Sprite m_grenadeSkillSilhouette;
    [SerializeField] private Sprite m_rocketSkillSilhouette;
    [SerializeField] private Sprite m_bulletTimeSkillSilhouette;
    [SerializeField] private Sprite m_comboBulletSprite;
    [SerializeField] private Sprite m_comboClipSprite;

    private readonly TextMeshProUGUI[] m_weaponNumberTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly TextMeshProUGUI[] m_weaponNameTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly TextMeshProUGUI[] m_weaponAmmoTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly Image[] m_weaponSilhouetteImages = new Image[k_HudRowCount];
    private readonly Image[] m_weaponBorderImages = new Image[k_HudRowCount];
    private readonly Image[] m_weaponBackgroundImages = new Image[k_HudRowCount];
    private readonly Sprite[] m_weaponSprites = new Sprite[4];
    private readonly TextMeshProUGUI[] m_pickupPopups = new TextMeshProUGUI[k_PickupPopupPoolSize];
    private readonly float[] m_pickupPopupExpiry = new float[k_PickupPopupPoolSize];
    private readonly TextMeshProUGUI[] m_scoreFeedbackTexts = new TextMeshProUGUI[k_ScoreFeedbackPoolSize];
    private readonly float[] m_scoreFeedbackExpiry = new float[k_ScoreFeedbackPoolSize];
    private readonly Image[] m_comboBulletImages = new Image[k_ComboBulletCount];
    private TextMeshProUGUI m_activeWeaponText;
    private TextMeshProUGUI m_scoreText;
    private TextMeshProUGUI m_survivalTimeText;
    private TextMeshProUGUI m_comboText;
    private RectTransform m_comboPanel;
    private Image m_comboClipImage;
    private Image m_skillCooldownFill;
    private Image m_hitMarkerImage;
    private Image m_damageVignetteImage;
    private Image m_deathTintImage;
    private Image m_crosshairImage;
    private TextMeshProUGUI m_emptyAmmoText;
    private Image m_dmrScopeVignetteImage;
    private PlayerHealth m_playerHealth;
    private Texture2D m_damageVignetteTexture;
    private Sprite m_damageVignetteSprite;
    private Color m_activeWeaponBaseColor = Color.white;
    private Color m_hitMarkerBaseColor = Color.white;
    private float m_emptyAmmoFeedbackUntil;
    private float m_emptyAmmoBlinkStartedAt;
    private float m_hitMarkerUntil;
    private float m_hitMarkerPulseScale = 1f;
    private float m_damageVignetteTargetAlpha;
    private float m_dmrScopeVignetteTargetAlpha;
    private float m_deathPresentationStartedAt = -1f;
    private float m_deathPresentationDuration;
    private int m_pickupPopupCount;
    private int m_scoreFeedbackCount;
    private Vector2 m_scoreFeedbackBasePosition = new(0f, -72f);
    private int m_lastComboCount = -1;
    private int m_lastVisibleComboBullets = -1;
    private int m_lastDisplayedSurvivalSecond = -1;
    private string m_lastSkillName;
    private string m_lastSkillStatus;
    private bool m_lastSkillHighlighted;
    private bool m_emptyAmmoActive;
    private PlayerSkillState m_lastSkillState;

    private void Awake()
    {
        if (m_font == null)
        {
            m_font = TMP_Settings.defaultFontAsset;
        }

        Debug.Assert(m_font != null);
        if (m_font == null)
        {
            enabled = false;
            return;
        }

        m_weaponNumberTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-492f, 164f), new Vector2(48f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNumberTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-492f, 98f), new Vector2(48f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNumberTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-492f, 38f), new Vector2(48f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNameTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-148f, 164f), new Vector2(184f, 34f), 17, TextAlignmentOptions.MidlineLeft);
        m_weaponNameTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-148f, 98f), new Vector2(184f, 34f), 17, TextAlignmentOptions.MidlineLeft);
        m_weaponNameTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-148f, 38f), new Vector2(184f, 34f), 17, TextAlignmentOptions.MidlineLeft);
        m_weaponAmmoTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 164f), new Vector2(82f, 34f), 17, TextAlignmentOptions.MidlineRight);
        m_weaponAmmoTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 98f), new Vector2(82f, 34f), 17, TextAlignmentOptions.MidlineRight);
        m_weaponAmmoTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 38f), new Vector2(82f, 34f), 17, TextAlignmentOptions.MidlineRight);
        for (int index = 0; index < k_HudRowCount; index++)
        {
            int slot = index + 1;
            m_weaponSilhouetteImages[index] = GetImage($"Layer_WeaponSilhouettes/WeaponSlot{slot}Silhouette");
            m_weaponBorderImages[index] = GetImage($"Layer_WeaponSlots/WeaponSlot{slot}Border");
            m_weaponBackgroundImages[index] = GetImage($"Layer_WeaponSlots/WeaponSlot{slot}Background");
            if (m_weaponBorderImages[index] != null)
            {
                m_weaponBorderImages[index].rectTransform.sizeDelta = k_WeaponBorderSize;
            }

            if (m_weaponBackgroundImages[index] != null)
            {
                m_weaponBackgroundImages[index].rectTransform.sizeDelta = k_WeaponBackgroundSize;
                m_weaponBackgroundImages[index].color = k_WeaponBackgroundColor;
            }

            Image silhouette = m_weaponSilhouetteImages[index];
            if (silhouette != null)
            {
                RectTransform rectTransform = silhouette.rectTransform;
                rectTransform.anchorMin = new Vector2(1f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(
                    -408f, m_weaponNumberTexts[index].rectTransform.anchoredPosition.y + 17f);
                rectTransform.sizeDelta = k_WeaponSilhouetteSize;
                silhouette.preserveAspect = true;
            }
        }
        InitializeSkillCooldownFill();

        m_weaponSprites[(int)WeaponId.Pistol - 1] = m_weaponSilhouetteImages[0]?.sprite;
        m_weaponSprites[(int)WeaponId.Shotgun - 1] = m_weaponSilhouetteImages[1]?.sprite;
        m_weaponSprites[(int)WeaponId.Rifle - 1] = m_weaponSilhouetteImages[2]?.sprite;
        m_weaponSprites[(int)WeaponId.DMR - 1] = m_dmrSilhouette;

        InitializePickupPopupPool();
        InitializeScoreHud();
        InitializeHitMarker();
        InitializeScoreFeedbackPool();
        InitializeDamageVignette();
        InitializeDeathTint();
        InitializeDmrScopeVignette();

        RefreshWeapon(1, WeaponId.Rifle, 0, false);
        RefreshWeapon(2, WeaponId.Pistol, 0, false);
        RefreshSkill("GRENADE", PlayerSkillState.Ready, 0f);
    }

    private void Update()
    {
        if (m_activeWeaponText != null)
        {
            m_activeWeaponText.color = GameplayClock.Now < m_emptyAmmoFeedbackUntil ? Color.red : m_activeWeaponBaseColor;
        }

        UpdateEmptyAmmoText();
        UpdatePickupPopups();
        UpdateScoreFeedbacks();
        UpdateHitMarker();
        UpdateDamageVignette();
        UpdateDeathTint();
        UpdateDmrScopeVignette();
        UpdateSkillReadyBlink();
    }

    public void BindPlayerHealth(PlayerHealth playerHealth)
    {
        if (m_playerHealth == playerHealth)
        {
            return;
        }
        if (m_playerHealth != null)
        {
            m_playerHealth.HealthNormalizedChanged -= SetHealthFeedback;
        }

        m_playerHealth = playerHealth;
        Debug.Assert(m_playerHealth != null, "Missing PlayerHealth for damage vignette.");
        if (m_playerHealth != null)
        {
            m_playerHealth.HealthNormalizedChanged += SetHealthFeedback;
            SetHealthFeedback(m_playerHealth.CurrentHealth / m_playerHealth.MaxHealth);
        }
    }

    internal void BeginDeathPresentation(float duration)
    {
        m_deathPresentationStartedAt = GameplayClock.Now;
        m_deathPresentationDuration = Mathf.Max(0.01f, duration);
        m_damageVignetteTargetAlpha = k_DeathDamageVignetteAlpha;
        m_dmrScopeVignetteTargetAlpha = 0f;
        if (m_crosshairImage != null)
        {
            m_crosshairImage.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (m_playerHealth != null)
        {
            m_playerHealth.HealthNormalizedChanged -= SetHealthFeedback;
        }

        if (m_damageVignetteSprite != null)
        {
            Destroy(m_damageVignetteSprite);
        }

        if (m_damageVignetteTexture != null)
        {
            Destroy(m_damageVignetteTexture);
        }
    }

    public void RefreshWeapon(int slot, WeaponId weapon, int ammo, bool isActive)
    {
        Debug.Assert(slot >= 1 && slot <= k_WeaponSlotCount);
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        TextMeshProUGUI numberText = m_weaponNumberTexts[slot - 1];
        TextMeshProUGUI nameText = m_weaponNameTexts[slot - 1];
        TextMeshProUGUI ammoText = m_weaponAmmoTexts[slot - 1];
        Color weaponColor = GetWeaponColor(weapon);
        weaponColor.a = isActive ? 1f : k_InactiveWeaponAlpha;
        numberText.text = slot.ToString();
        nameText.text = GetWeaponName(weapon);
        ammoText.text = ammo.ToString();
        float fontSize = isActive ? k_ActiveWeaponFontSize : k_InactiveWeaponFontSize;
        numberText.fontSize = fontSize;
        nameText.fontSize = fontSize;
        ammoText.fontSize = fontSize;
        numberText.color = weaponColor;
        nameText.color = weaponColor;
        ammoText.color = weaponColor;
        Image silhouette = m_weaponSilhouetteImages[slot - 1];
        if (silhouette != null)
        {
            silhouette.sprite = GetWeaponSprite(weapon);
            silhouette.color = weaponColor;
            silhouette.rectTransform.sizeDelta = weapon == WeaponId.DMR
                ? k_DmrSilhouetteSize : k_WeaponSilhouetteSize;
        }
        SetBorderState(m_weaponBorderImages[slot - 1], isActive);
        if (isActive)
        {
            m_activeWeaponText = ammoText;
            m_activeWeaponBaseColor = weaponColor;
            bool isEmpty = ammo <= 0;
            if (m_emptyAmmoActive != isEmpty)
            {
                m_emptyAmmoActive = isEmpty;
                m_emptyAmmoBlinkStartedAt = GameplayClock.Now;
            }
            UpdateEmptyAmmoText();
        }
    }

    public void SetDmrAimState(bool dmrEquipped, bool zoomed)
    {
        if (m_crosshairImage != null)
        {
            m_crosshairImage.enabled = ShouldShowCrosshair(dmrEquipped, zoomed);
        }
        m_dmrScopeVignetteTargetAlpha = zoomed ? k_DmrScopeVignetteAlpha : 0f;
    }

    internal static bool ShouldShowCrosshair(bool dmrEquipped, bool zoomed)
    {
        return !dmrEquipped || zoomed;
    }

    public void RefreshSkill(string skillName, PlayerSkillState state, float cooldownNormalized)
    {
        bool highlighted = state is PlayerSkillState.Ready or PlayerSkillState.Armed;
        m_lastSkillState = state;
        string status = state == PlayerSkillState.Cooldown
            ? string.Empty
            : state == PlayerSkillState.Armed ? "READY" : state.ToString().ToUpperInvariant();
        Sprite skillSprite = skillName == "GRENADE" ? m_grenadeSkillSilhouette
            : skillName == "ROCKET" ? m_rocketSkillSilhouette : m_bulletTimeSkillSilhouette;
        Vector2 skillSize = skillName == "ROCKET"
            ? k_RocketSkillSilhouetteSize : k_SquareSkillSilhouetteSize;
        UpdateSkillCooldownFill(skillName, state, cooldownNormalized, skillSprite, skillSize);
        if (m_lastSkillName == skillName && m_lastSkillStatus == status
            && m_lastSkillHighlighted == highlighted)
        {
            return;
        }
        m_lastSkillName = skillName;
        m_lastSkillStatus = status;
        m_lastSkillHighlighted = highlighted;

        int row = k_HudRowCount - 1;
        m_weaponNumberTexts[row].text = "F";
        m_weaponNameTexts[row].text = skillName;
        m_weaponAmmoTexts[row].text = status;
        Color color = highlighted
            ? Color.white
            : k_SkillInactiveColor;
        m_weaponNumberTexts[row].color = color;
        m_weaponNameTexts[row].color = color;
        m_weaponAmmoTexts[row].color = color;
        if (m_weaponSilhouetteImages[row] != null)
        {
            m_weaponSilhouetteImages[row].sprite = skillSprite;
            m_weaponSilhouetteImages[row].enabled = skillSprite != null;
            m_weaponSilhouetteImages[row].color = color;
            m_weaponSilhouetteImages[row].rectTransform.sizeDelta = skillSize;
        }
        SetBorderState(m_weaponBorderImages[row], highlighted);
    }

    public void ShowEmptyAmmoFeedback()
    {
        m_emptyAmmoFeedbackUntil = GameplayClock.Now + k_EmptyAmmoFeedbackDuration;
    }

    public void RefreshScore(int score)
    {
        if (m_scoreText != null)
        {
            m_scoreText.text = $"SCORE  {Mathf.Max(0, score):000000}";
        }
    }

    public void RefreshSurvivalTime(float survivalTime)
    {
        int elapsedSeconds = Mathf.FloorToInt(Mathf.Max(0f, survivalTime));
        if (elapsedSeconds == m_lastDisplayedSurvivalSecond)
        {
            return;
        }

        m_lastDisplayedSurvivalSecond = elapsedSeconds;
        if (m_survivalTimeText != null)
        {
            m_survivalTimeText.text = FormatSurvivalTime(elapsedSeconds);
        }
    }

    public void RefreshCombo(int comboCount, float remainingSeconds)
    {
        int safeCount = Mathf.Max(0, comboCount);
        int visibleBullets = GetVisibleComboBulletCount(remainingSeconds);
        bool isVisible = safeCount > 0 && remainingSeconds > 0f;
        if (m_comboPanel != null)
        {
            m_comboPanel.gameObject.SetActive(isVisible);
        }
        if (!isVisible)
        {
            m_lastComboCount = safeCount;
            m_lastVisibleComboBullets = visibleBullets;
            return;
        }

        if (m_comboText != null && m_lastComboCount != safeCount)
        {
            Color countColor = GetComboCountColor(safeCount);
            m_comboText.text = $"COMBO  <color=#{ColorUtility.ToHtmlStringRGB(countColor)}>x{safeCount}</color>";
            m_lastComboCount = safeCount;
        }
        if (m_lastVisibleComboBullets == visibleBullets)
        {
            return;
        }

        m_lastVisibleComboBullets = visibleBullets;
        for (int index = 0; index < m_comboBulletImages.Length; index++)
        {
            Image bullet = m_comboBulletImages[index];
            if (bullet != null)
            {
                bullet.enabled = m_comboBulletSprite != null && index < visibleBullets;
            }
        }
    }

    public void SetScoreFeedbackBasePosition(Vector2 position)
    {
        m_scoreFeedbackBasePosition = position;
        for (int index = 0; index < m_scoreFeedbackTexts.Length; index++)
        {
            if (m_scoreFeedbackTexts[index] != null)
            {
                m_scoreFeedbackTexts[index].rectTransform.anchoredPosition =
                    GetScoreFeedbackTargetPosition(index);
            }
        }
    }

    public void ShowScoreFeedback(int points, string label)
    {
        if (points <= 0 || m_scoreFeedbackTexts[0] == null)
        {
            return;
        }

        TextMeshProUGUI recycled = m_scoreFeedbackCount < k_ScoreFeedbackPoolSize
            ? m_scoreFeedbackTexts[m_scoreFeedbackCount]
            : m_scoreFeedbackTexts[k_ScoreFeedbackPoolSize - 1];
        int newCount = Mathf.Min(m_scoreFeedbackCount + 1, k_ScoreFeedbackPoolSize);
        for (int index = newCount - 1; index > 0; index--)
        {
            m_scoreFeedbackTexts[index] = m_scoreFeedbackTexts[index - 1];
            m_scoreFeedbackExpiry[index] = m_scoreFeedbackExpiry[index - 1];
        }

        m_scoreFeedbackCount = newCount;
        m_scoreFeedbackTexts[0] = recycled;
        m_scoreFeedbackExpiry[0] = GameplayClock.Now + k_ScoreFeedbackDuration + k_ScoreFeedbackFadeDuration;
        string trimmedLabel = label?.Trim();
        recycled.text = string.IsNullOrEmpty(trimmedLabel)
            ? $"<b>+{points}</b>"
            : $"{trimmedLabel}  <b>+{points}</b>";
        recycled.color = Color.white;
        recycled.gameObject.SetActive(true);
        for (int index = 0; index < m_scoreFeedbackCount; index++)
        {
            m_scoreFeedbackTexts[index].rectTransform.anchoredPosition =
                GetScoreFeedbackTargetPosition(index);
        }
    }

    public void ShowHitMarker(bool isHeadshot, bool isKill)
    {
        if (m_hitMarkerImage == null)
        {
            return;
        }

        m_hitMarkerBaseColor = isKill
            ? k_KillHitMarkerColor
            : isHeadshot ? k_HeadshotHitMarkerColor : k_NormalHitMarkerColor;
        m_hitMarkerImage.rectTransform.sizeDelta = isKill
            ? k_KillHitMarkerSize
            : isHeadshot ? k_HeadshotHitMarkerSize : k_NormalHitMarkerSize;
        m_hitMarkerImage.color = m_hitMarkerBaseColor;
        m_hitMarkerPulseScale = isKill ? 1.35f : isHeadshot ? 1.25f : 1.15f;
        m_hitMarkerImage.rectTransform.localScale = Vector3.one * m_hitMarkerPulseScale;
        m_hitMarkerImage.gameObject.SetActive(true);
        m_hitMarkerUntil = GameplayClock.Now + k_HitMarkerDuration;
    }

    public void ShowAmmoPickup(WeaponId weapon, int amount)
    {
        if (weapon < WeaponId.Pistol || weapon > WeaponId.DMR || amount <= 0)
        {
            return;
        }

        ShowAmmoPopup(weapon, $"+{amount}  {GetWeaponName(weapon)} AMMO");
    }

    public void ShowEmptyAmmoPopup(WeaponId weapon)
    {
        if (weapon < WeaponId.Pistol || weapon > WeaponId.DMR)
        {
            return;
        }
        ShowAmmoPopup(weapon, $"EMPTY {GetWeaponName(weapon)} AMMO");
    }

    private void ShowAmmoPopup(WeaponId weapon, string message)
    {
        if (m_pickupPopupCount == k_PickupPopupPoolSize)
        {
            RemoveOldestPickupPopup();
        }

        TextMeshProUGUI popup = m_pickupPopups[m_pickupPopupCount];
        popup.text = message;
        popup.color = GetWeaponColor(weapon);
        popup.rectTransform.anchoredPosition = k_PickupPopupBasePosition;
        popup.gameObject.SetActive(true);
        m_pickupPopupExpiry[m_pickupPopupCount] = GameplayClock.Now + k_PickupPopupDuration;
        m_pickupPopupCount++;
    }

    [ContextMenu("Run HUD Self Check")]
    private void RunHudSelfCheck()
    {
        Debug.Assert(k_WeaponSlotCount == 2);
        Debug.Assert(FormatSurvivalTime(3599) == "59:59");
        Debug.Assert(FormatSurvivalTime(3600) == "60:00");
        Debug.Assert(FormatSurvivalTime(-1) == "00:00");
        RefreshWeapon(1, WeaponId.Rifle, 30, true);
        Debug.Assert(m_activeWeaponText.text == "30");
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[0].color.a, 1f));
        Debug.Assert(Mathf.Approximately(m_weaponSilhouetteImages[0].color.a, 1f));
        Debug.Assert(m_weaponBorderImages[0].color == k_ActiveBorderColor);
        Debug.Assert(m_weaponBorderImages[0].rectTransform.sizeDelta == k_WeaponBorderSize);
        Debug.Assert(m_weaponBackgroundImages[0].color == k_WeaponBackgroundColor);
        Debug.Assert(m_weaponBackgroundImages[0].rectTransform.sizeDelta == k_WeaponBackgroundSize);
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[0].fontSize, k_ActiveWeaponFontSize));
        Debug.Assert(m_weaponSilhouetteImages[0].color == new Color32(44, 135, 232, 255));
        RefreshWeapon(1, WeaponId.Rifle, 0, true);
        Debug.Assert(m_emptyAmmoText.enabled);
        Debug.Assert(IsEmptyAmmoBlinkVisible(true, 0f));
        Debug.Assert(!IsEmptyAmmoBlinkVisible(true, k_EmptyAmmoBlinkInterval));
        Debug.Assert(IsEmptyAmmoBlinkVisible(true, k_EmptyAmmoBlinkInterval * 2f));
        Debug.Assert(!IsEmptyAmmoBlinkVisible(false, 0f));
        RefreshWeapon(2, WeaponId.Pistol, 15, true);
        Debug.Assert(!m_emptyAmmoText.enabled);
        RefreshWeapon(2, WeaponId.Pistol, 0, true);
        Debug.Assert(m_emptyAmmoText.enabled);
        RefreshWeapon(2, WeaponId.Pistol, 5, true);
        Debug.Assert(!m_emptyAmmoText.enabled);
        RefreshWeapon(2, WeaponId.Pistol, 15, false);
        Debug.Assert(Mathf.Approximately(m_weaponAmmoTexts[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(Mathf.Approximately(m_weaponSilhouetteImages[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(Mathf.Approximately(m_weaponBorderImages[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(m_weaponBorderImages[1].color == k_InactiveBorderColor);
        Debug.Assert(m_weaponSilhouetteImages[1].color == new Color32(234, 64, 71, 102));
        RefreshWeapon(1, WeaponId.DMR, 12, true);
        Debug.Assert(m_weaponSilhouetteImages[0].color == new Color32(44, 135, 232, 255));
        Debug.Assert(m_weaponSilhouetteImages[0].rectTransform.sizeDelta == k_DmrSilhouetteSize);
        RefreshSkill("GRENADE", PlayerSkillState.Cooldown, 0.04f);
        Debug.Assert(m_skillCooldownFill.gameObject.activeSelf);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0f));
        Debug.Assert(m_skillCooldownFill.sprite == m_grenadeSkillSilhouette);
        RefreshSkill("GRENADE", PlayerSkillState.Cooldown, 0.16f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0.1f));
        RefreshSkill("ROCKET", PlayerSkillState.Cooldown, 0.56f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0.5f));
        Debug.Assert(m_skillCooldownFill.sprite == m_rocketSkillSilhouette);
        Debug.Assert(m_skillCooldownFill.rectTransform.sizeDelta == k_RocketSkillSilhouetteSize);
        RefreshSkill("BULLET TIME", PlayerSkillState.Cooldown, 1f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 1f));
        RefreshSkill("BULLET TIME", PlayerSkillState.Ready, 0f);
        Debug.Assert(!m_skillCooldownFill.gameObject.activeSelf);
        Debug.Assert(IsSkillReadyBlinkVisible(0f));
        Debug.Assert(!IsSkillReadyBlinkVisible(k_SkillReadyBlinkInterval));
        Debug.Assert(IsSkillReadyBlinkVisible(k_SkillReadyBlinkInterval * 2f));
        RectTransform skillNameRect = m_weaponNameTexts[k_HudRowCount - 1].rectTransform;
        RectTransform skillStatusRect = m_weaponAmmoTexts[k_HudRowCount - 1].rectTransform;
        Debug.Assert(skillNameRect.anchoredPosition.x
            <= skillStatusRect.anchoredPosition.x - skillStatusRect.sizeDelta.x);
        Debug.Assert(m_weaponBorderImages[1].rectTransform.sizeDelta == k_WeaponBorderSize);
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[1].fontSize, k_InactiveWeaponFontSize));
        ShowEmptyAmmoFeedback();
        Debug.Assert(m_emptyAmmoFeedbackUntil > GameplayClock.Now);
        int popupCount = m_pickupPopupCount;
        ShowAmmoPickup(WeaponId.Pistol, 0);
        Debug.Assert(m_pickupPopupCount == popupCount);
        ShowEmptyAmmoPopup(WeaponId.Pistol);
        Debug.Assert(m_pickupPopups[m_pickupPopupCount - 1].text == "EMPTY PISTOL AMMO");
        ShowHitMarker(false, false);
        Debug.Assert(m_hitMarkerImage.color == k_NormalHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta == k_NormalHitMarkerSize);
        ShowHitMarker(true, false);
        Debug.Assert(m_hitMarkerImage.color == k_HeadshotHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta == k_HeadshotHitMarkerSize);
        ShowHitMarker(true, true);
        Debug.Assert(m_hitMarkerImage.color == k_KillHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta == k_KillHitMarkerSize);
        Debug.Assert(Mathf.Approximately(m_hitMarkerPulseScale, 1.35f));
        Debug.Assert(m_hitMarkerUntil > GameplayClock.Now);
        SetDmrAimState(true, false);
        Debug.Assert(!m_crosshairImage.enabled && m_hitMarkerImage.gameObject.activeSelf);
        SetDmrAimState(true, true);
        Debug.Assert(m_crosshairImage.enabled
            && Mathf.Approximately(m_dmrScopeVignetteTargetAlpha, k_DmrScopeVignetteAlpha));
        SetDmrAimState(false, false);
        Debug.Assert(m_crosshairImage.enabled && Mathf.Approximately(m_dmrScopeVignetteTargetAlpha, 0f));
        Debug.Assert(GetVisibleComboBulletCount(5f) == 5);
        Debug.Assert(GetVisibleComboBulletCount(4.999f) == 5);
        Debug.Assert(GetVisibleComboBulletCount(4f) == 4);
        Debug.Assert(GetVisibleComboBulletCount(1f) == 1);
        Debug.Assert(GetVisibleComboBulletCount(0f) == 0);
        Debug.Assert(GetComboCountColor(1) == Color.white);
        Debug.Assert(GetComboCountColor(10) == k_MaxComboColor);
        Debug.Assert(GetComboCountColor(11) == k_MaxComboColor);
        RefreshCombo(11, 5f);
        Debug.Assert(m_comboPanel.gameObject.activeSelf && m_comboText.text.Contains("x11"));
        for (int index = 0; index < m_comboBulletImages.Length; index++)
        {
            Debug.Assert(m_comboBulletImages[index].sprite == m_comboBulletSprite);
            Debug.Assert(m_comboBulletImages[index].preserveAspect);
            Debug.Assert(m_comboBulletImages[index].color == Color.white);
            Debug.Assert(m_comboBulletImages[index].enabled == (m_comboBulletSprite != null));
            if (index > 0)
            {
                float pitch = m_comboBulletImages[index - 1].rectTransform.anchoredPosition.y
                    - m_comboBulletImages[index].rectTransform.anchoredPosition.y;
                Debug.Assert(Mathf.Approximately(pitch, k_ComboBulletPitch));
            }
        }
        Debug.Assert(m_comboClipImage.sprite == m_comboClipSprite);
        Debug.Assert(m_comboClipImage.preserveAspect);
        Debug.Assert(m_comboClipImage.transform.GetSiblingIndex()
            == m_comboClipImage.transform.parent.childCount - 1);
        Debug.Assert(Mathf.Abs(
            k_ComboClipSize.y * k_ComboClipVisibleHeightRatio - k_ComboStackVisibleHeight) < 0.01f);
        RefreshCombo(11, 4f);
        Debug.Assert(!m_comboBulletImages[4].enabled);
        int pickupCountBeforeScoreFeedback = m_pickupPopupCount;
        for (int index = 1; index <= 7; index++)
        {
            ShowScoreFeedback(index, $"TEST {index}");
        }
        Debug.Assert(m_scoreFeedbackCount == k_ScoreFeedbackPoolSize);
        Debug.Assert(m_scoreFeedbackTexts[0].text == "TEST 7  <b>+7</b>");
        Debug.Assert(m_scoreFeedbackTexts[5].rectTransform.anchoredPosition
            == GetScoreFeedbackTargetPosition(5));
        Debug.Assert(GetScoreFeedbackTargetPosition(0).y > GetScoreFeedbackTargetPosition(1).y);
        Debug.Assert(Mathf.Approximately(GetScoreFeedbackAlpha(2.55f, 0f), 1f));
        Debug.Assert(Mathf.Abs(GetScoreFeedbackAlpha(2.55f, 2.4f) - 0.5f) < 0.001f);
        Debug.Assert(Mathf.Approximately(GetScoreFeedbackAlpha(2.55f, 2.55f), 0f));
        Debug.Assert(m_pickupPopupCount == pickupCountBeforeScoreFeedback);
        ClearScoreFeedbacks();
        RefreshCombo(0, 0f);
        m_hitMarkerImage.gameObject.SetActive(false);
        m_hitMarkerImage.rectTransform.localScale = Vector3.one;
        m_hitMarkerUntil = 0f;
    }

    private void InitializeHitMarker()
    {
        Transform crosshair = transform.Find("Crosshair");
        m_crosshairImage = crosshair != null ? crosshair.GetComponent<Image>() : null;
        Debug.Assert(m_crosshairImage != null, "Missing HUD Image: Crosshair");
        Transform emptyAmmo = crosshair != null ? crosshair.Find("EmptyAmmoText") : null;
        m_emptyAmmoText = emptyAmmo != null ? emptyAmmo.GetComponent<TextMeshProUGUI>() : null;
        Debug.Assert(m_emptyAmmoText != null, "Missing HUD Text: Crosshair/EmptyAmmoText");
        if (m_emptyAmmoText != null)
        {
            m_emptyAmmoText.enabled = false;
        }
        Transform marker = crosshair != null ? crosshair.Find("HitMarker") : null;
        m_hitMarkerImage = marker != null ? marker.GetComponent<Image>() : null;
        Debug.Assert(m_hitMarkerImage != null, "Missing HUD Image: Crosshair/HitMarker");
        if (m_hitMarkerImage == null)
        {
            return;
        }

        RectTransform rectTransform = m_hitMarkerImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = k_NormalHitMarkerSize;
        m_hitMarkerImage.raycastTarget = false;
        m_hitMarkerImage.transform.SetAsLastSibling();
        m_hitMarkerImage.gameObject.SetActive(false);
    }

    private void UpdateEmptyAmmoText()
    {
        if (m_emptyAmmoText != null)
        {
            m_emptyAmmoText.enabled = IsEmptyAmmoBlinkVisible(m_emptyAmmoActive,
                GameplayClock.Now - m_emptyAmmoBlinkStartedAt);
        }
    }

    private static bool IsEmptyAmmoBlinkVisible(bool isEmpty, float elapsed)
    {
        return isEmpty && Mathf.FloorToInt(Mathf.Max(0f, elapsed) / k_EmptyAmmoBlinkInterval) % 2 == 0;
    }

    private void InitializeDamageVignette()
    {
        Transform existing = transform.Find("Layer_DamageVignette");
        if (existing == null)
        {
            GameObject vignetteObject = new("Layer_DamageVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            vignetteObject.transform.SetParent(transform, false);
            existing = vignetteObject.transform;
        }

        m_damageVignetteImage = existing.GetComponent<Image>();
        ConfigureFullscreenVignette(m_damageVignetteImage);
        m_damageVignetteImage.raycastTarget = false;
        m_damageVignetteImage.transform.SetAsFirstSibling();

        m_damageVignetteTexture = CreateDamageVignetteTexture(512);
        m_damageVignetteSprite = Sprite.Create(
            m_damageVignetteTexture,
            new Rect(0f, 0f, m_damageVignetteTexture.width, m_damageVignetteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        m_damageVignetteImage.sprite = m_damageVignetteSprite;
        m_damageVignetteImage.color = new Color(0.55f, 0.015f, 0.02f, 0f);

    }

    private void InitializeDmrScopeVignette()
    {
        Transform existing = transform.Find("Layer_DmrScopeVignette");
        if (existing == null)
        {
            GameObject vignetteObject = new(
                "Layer_DmrScopeVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            vignetteObject.transform.SetParent(transform, false);
            existing = vignetteObject.transform;
        }

        m_dmrScopeVignetteImage = existing.GetComponent<Image>();
        ConfigureFullscreenVignette(m_dmrScopeVignetteImage);
        m_dmrScopeVignetteImage.raycastTarget = false;
        m_dmrScopeVignetteImage.sprite = m_damageVignetteSprite;
        m_dmrScopeVignetteImage.color = new Color(0f, 0f, 0f, 0f);
        m_dmrScopeVignetteImage.enabled = false;
        m_dmrScopeVignetteImage.transform.SetAsFirstSibling();
    }

    private void InitializeDeathTint()
    {
        Transform existing = transform.Find("Layer_DeathTint");
        if (existing == null)
        {
            GameObject tintObject = new("Layer_DeathTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tintObject.transform.SetParent(transform, false);
            existing = tintObject.transform;
        }

        m_deathTintImage = existing.GetComponent<Image>();
        ConfigureFullscreenVignette(m_deathTintImage);
        m_deathTintImage.sprite = null;
        m_deathTintImage.color = new Color(0.45f, 0.005f, 0.01f, 0f);
        m_deathTintImage.raycastTarget = false;
        m_deathTintImage.enabled = false;
        m_deathTintImage.transform.SetAsFirstSibling();
    }

    private static void ConfigureFullscreenVignette(Image image)
    {
        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter == null)
        {
            fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        }

        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;
        Debug.Assert(fitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent
            && Mathf.Approximately(fitter.aspectRatio, 16f / 9f));
    }

    private static Texture2D CreateDamageVignetteTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime_DamageVignette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float normalizedY = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x + 0.5f) / size * 2f - 1f;
                float edgeDistance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, edgeDistance));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void SetHealthFeedback(float normalizedHealth)
    {
        float missingHealth = 1f - Mathf.Clamp01(normalizedHealth);
        m_damageVignetteTargetAlpha = Mathf.Pow(missingHealth, 1.25f) * k_MaxDamageVignetteAlpha;
    }

    private void UpdateDamageVignette()
    {
        if (m_damageVignetteImage == null)
        {
            return;
        }

        Color color = m_damageVignetteImage.color;
        float speed = m_damageVignetteTargetAlpha > color.a
            ? k_DamageVignetteIncreaseSpeed
            : k_DamageVignetteRecoverySpeed;
        color.a = Mathf.MoveTowards(color.a, m_damageVignetteTargetAlpha, speed * GameplayClock.DeltaTime);
        m_damageVignetteImage.color = color;
        m_damageVignetteImage.enabled = color.a > 0.001f;
    }

    private void UpdateDeathTint()
    {
        if (m_deathTintImage == null || m_deathPresentationStartedAt < 0f)
        {
            return;
        }

        float progress = Mathf.Clamp01((GameplayClock.Now - m_deathPresentationStartedAt)
            / m_deathPresentationDuration);
        Color color = m_deathTintImage.color;
        color.a = Mathf.SmoothStep(0f, k_DeathTintAlpha, progress);
        m_deathTintImage.color = color;
        m_deathTintImage.enabled = color.a > 0.001f;
    }

    private void UpdateDmrScopeVignette()
    {
        if (m_dmrScopeVignetteImage == null)
        {
            return;
        }

        Color color = m_dmrScopeVignetteImage.color;
        float speed = k_DmrScopeVignetteAlpha / k_DmrScopeTransitionDuration;
        color.a = Mathf.MoveTowards(
            color.a, m_dmrScopeVignetteTargetAlpha, speed * GameplayClock.DeltaTime);
        m_dmrScopeVignetteImage.color = color;
        m_dmrScopeVignetteImage.enabled = color.a > 0.001f;
    }

    private void InitializePickupPopupPool()
    {
        Transform layer = transform.Find("Layer_PickupFeedback");
        Debug.Assert(layer != null);
        if (layer == null)
        {
            return;
        }

        Transform placeholder = layer.Find("PickupText");
        if (placeholder != null)
        {
            placeholder.gameObject.SetActive(false);
        }

        for (int index = 0; index < k_PickupPopupPoolSize; index++)
        {
            string objectName = $"PickupPopup{index}";
            Transform existing = layer.Find(objectName);
            TextMeshProUGUI popup = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (popup == null)
            {
                GameObject popupObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                popupObject.transform.SetParent(layer, false);
                popup = popupObject.GetComponent<TextMeshProUGUI>();
            }

            RectTransform rectTransform = popup.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.anchoredPosition = k_PickupPopupBasePosition;
            rectTransform.sizeDelta = new Vector2(430f, 34f);
            popup.font = m_font;
            popup.fontSize = 17f;
            popup.alignment = TextAlignmentOptions.Center;
            popup.enableAutoSizing = false;
            popup.textWrappingMode = TextWrappingModes.NoWrap;
            popup.overflowMode = TextOverflowModes.Overflow;
            popup.raycastTarget = false;
            popup.text = string.Empty;
            popup.gameObject.SetActive(false);
            m_pickupPopups[index] = popup;
        }
    }

    private void InitializeScoreHud()
    {
        m_scoreText = GetOrCreateText(
            "Layer_ScoreCombo/ScoreText", Vector2.one, Vector2.one,
            new Vector2(-48f, -34f), new Vector2(360f, 38f), 30, TextAlignmentOptions.MidlineRight);
        m_comboText = GetOrCreateText(
            "Layer_ScoreCombo/ComboText", Vector2.one, Vector2.one,
            new Vector2(-48f, -72f), new Vector2(360f, 30f), 22, TextAlignmentOptions.MidlineRight);

        Transform layer = transform.Find("Layer_ScoreCombo");
        Debug.Assert(layer != null);
        if (layer == null)
        {
            return;
        }

        DisableLegacyScoreObject(layer, "ComboGaugeBackground");
        DisableLegacyScoreObject(layer, "ComboGaugeFill");
        DisableLegacyScoreObject(layer, "ScoreFeedbackText");
        InitializeComboPanel(layer);

        RefreshScore(0);
        m_survivalTimeText = layer.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_survivalTimeText != null, "Missing HUD Text: Layer_ScoreCombo/TimeText");
        RefreshCombo(0, 0f);
    }

    private void InitializeComboPanel(Transform layer)
    {
        Transform existingPanel = layer.Find("ComboPanel");
        if (existingPanel == null)
        {
            GameObject panelObject = new("ComboPanel", typeof(RectTransform));
            panelObject.transform.SetParent(layer, false);
            existingPanel = panelObject.transform;
        }

        m_comboPanel = (RectTransform)existingPanel;
        m_comboPanel.anchorMin = new Vector2(0f, 0.5f);
        m_comboPanel.anchorMax = new Vector2(0f, 0.5f);
        m_comboPanel.pivot = new Vector2(0f, 0.5f);
        m_comboPanel.anchoredPosition = new Vector2(52f, 0f);
        m_comboPanel.sizeDelta = new Vector2(330f, 190f);

        m_comboText.transform.SetParent(m_comboPanel, false);
        RectTransform comboTextRect = m_comboText.rectTransform;
        comboTextRect.anchorMin = new Vector2(0f, 1f);
        comboTextRect.anchorMax = new Vector2(0f, 1f);
        comboTextRect.pivot = new Vector2(0f, 1f);
        comboTextRect.anchoredPosition = Vector2.zero;
        comboTextRect.sizeDelta = new Vector2(320f, 42f);
        m_comboText.fontSize = 30f;
        m_comboText.alignment = TextAlignmentOptions.MidlineLeft;
        m_comboText.richText = true;

        for (int index = 0; index < k_ComboBulletCount; index++)
        {
            Image bullet = GetOrCreateImage(
                m_comboPanel, $"ComboBullet{index}",
                k_ComboBulletStartPosition + Vector2.down * index * k_ComboBulletPitch,
                k_ComboBulletSize, Color.white);
            RectTransform bulletRect = bullet.rectTransform;
            bulletRect.anchorMin = new Vector2(0f, 1f);
            bulletRect.anchorMax = new Vector2(0f, 1f);
            bulletRect.pivot = new Vector2(0.5f, 0.5f);
            bullet.sprite = m_comboBulletSprite;
            bullet.type = Image.Type.Simple;
            bullet.preserveAspect = true;
            bullet.enabled = false;
            m_comboBulletImages[index] = bullet;
        }

        m_comboClipImage = GetOrCreateImage(
            m_comboPanel, "ComboClip", k_ComboClipPosition, k_ComboClipSize, Color.white);
        RectTransform clipRect = m_comboClipImage.rectTransform;
        clipRect.anchorMin = new Vector2(0f, 1f);
        clipRect.anchorMax = new Vector2(0f, 1f);
        clipRect.pivot = new Vector2(0.5f, 0.5f);
        m_comboClipImage.sprite = m_comboClipSprite;
        m_comboClipImage.type = Image.Type.Simple;
        m_comboClipImage.preserveAspect = true;
        m_comboClipImage.enabled = m_comboClipSprite != null;
        m_comboClipImage.transform.SetAsLastSibling();

        m_comboPanel.gameObject.SetActive(false);
    }

    private void InitializeScoreFeedbackPool()
    {
        Transform crosshair = transform.Find("Crosshair");
        Debug.Assert(crosshair != null, "Missing HUD Transform: Crosshair");
        if (crosshair == null)
        {
            return;
        }

        for (int index = 0; index < k_ScoreFeedbackPoolSize; index++)
        {
            string objectName = $"ScoreFeedback{index}";
            Transform existing = crosshair.Find(objectName);
            TextMeshProUGUI feedback = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (feedback == null)
            {
                GameObject feedbackObject = new(
                    objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                feedbackObject.transform.SetParent(crosshair, false);
                feedback = feedbackObject.GetComponent<TextMeshProUGUI>();
            }

            RectTransform rectTransform = feedback.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = GetScoreFeedbackTargetPosition(index);
            rectTransform.sizeDelta = new Vector2(560f, 32f);
            feedback.font = m_font;
            feedback.fontSize = 24f;
            feedback.alignment = TextAlignmentOptions.Center;
            feedback.enableAutoSizing = false;
            feedback.richText = true;
            feedback.textWrappingMode = TextWrappingModes.NoWrap;
            feedback.overflowMode = TextOverflowModes.Overflow;
            feedback.color = Color.white;
            feedback.raycastTarget = false;
            feedback.text = string.Empty;
            feedback.gameObject.SetActive(false);
            m_scoreFeedbackTexts[index] = feedback;
        }
    }

    private void InitializeSkillCooldownFill()
    {
        Image skillSilhouette = m_weaponSilhouetteImages[k_HudRowCount - 1];
        Debug.Assert(skillSilhouette != null, "Missing HUD Image: WeaponSlot3Silhouette");
        if (skillSilhouette == null)
        {
            return;
        }

        m_skillCooldownFill = GetOrCreateImage(
            skillSilhouette.transform, "WeaponSlot3CooldownFill", Vector2.zero,
            k_SquareSkillSilhouetteSize, Color.white);
        ConfigureSkillCooldownFillRect(m_skillCooldownFill, k_SquareSkillSilhouetteSize);
        m_skillCooldownFill.transform.SetAsLastSibling();
        m_skillCooldownFill.type = Image.Type.Filled;
        m_skillCooldownFill.fillMethod = Image.FillMethod.Horizontal;
        m_skillCooldownFill.fillOrigin = 0;
        m_skillCooldownFill.preserveAspect = true;
        m_skillCooldownFill.gameObject.SetActive(false);
    }

    private void UpdateSkillCooldownFill(string skillName, PlayerSkillState state, float cooldownNormalized,
        Sprite skillSprite, Vector2 skillSize)
    {
        if (m_skillCooldownFill == null)
        {
            return;
        }

        bool isCoolingDown = state == PlayerSkillState.Cooldown && skillSprite != null;
        m_skillCooldownFill.gameObject.SetActive(isCoolingDown);
        if (!isCoolingDown)
        {
            return;
        }

        m_skillCooldownFill.sprite = skillSprite;
        ConfigureSkillCooldownFillRect(m_skillCooldownFill, skillSize);
        float normalized = Mathf.Clamp01(cooldownNormalized);
        m_skillCooldownFill.fillAmount = normalized >= 1f
            ? 1f : Mathf.Floor(normalized * 10f) * 0.1f;
        m_skillCooldownFill.color = skillName == "GRENADE" ? new Color32(234, 64, 71, 255)
            : skillName == "ROCKET" ? new Color32(53, 199, 89, 255)
            : new Color32(44, 135, 232, 255);
    }

    private static void ConfigureSkillCooldownFillRect(Image image, Vector2 size)
    {
        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;
    }

    private void UpdateSkillReadyBlink()
    {
        Image skillIcon = m_weaponSilhouetteImages[k_HudRowCount - 1];
        if (skillIcon == null)
        {
            return;
        }

        bool isReady = m_lastSkillState == PlayerSkillState.Ready;
        skillIcon.enabled = skillIcon.sprite != null
            && (!isReady || IsSkillReadyBlinkVisible(GameplayClock.Now));
    }

    private static bool IsSkillReadyBlinkVisible(float elapsed)
    {
        return Mathf.FloorToInt(Mathf.Max(0f, elapsed) / k_SkillReadyBlinkInterval) % 2 == 0;
    }

    private void UpdateScoreFeedbacks()
    {
        while (m_scoreFeedbackCount > 0
            && m_scoreFeedbackExpiry[m_scoreFeedbackCount - 1] <= GameplayClock.Now)
        {
            RemoveOldestScoreFeedback();
        }

        for (int index = 0; index < m_scoreFeedbackCount; index++)
        {
            TextMeshProUGUI feedback = m_scoreFeedbackTexts[index];
            Color color = Color.white;
            color.a = GetScoreFeedbackAlpha(m_scoreFeedbackExpiry[index], GameplayClock.Now);
            feedback.color = color;
        }
    }

    private void RemoveOldestScoreFeedback()
    {
        int oldestIndex = m_scoreFeedbackCount - 1;
        m_scoreFeedbackTexts[oldestIndex].gameObject.SetActive(false);
        m_scoreFeedbackExpiry[oldestIndex] = 0f;
        m_scoreFeedbackCount--;
    }

    private void UpdateHitMarker()
    {
        if (m_hitMarkerImage == null || !m_hitMarkerImage.gameObject.activeSelf)
        {
            return;
        }

        float remaining = m_hitMarkerUntil - GameplayClock.Now;
        if (remaining <= 0f)
        {
            m_hitMarkerImage.gameObject.SetActive(false);
            m_hitMarkerImage.rectTransform.localScale = Vector3.one;
            return;
        }

        float elapsed = k_HitMarkerDuration - remaining;
        float pulseProgress = Mathf.Clamp01(elapsed / k_HitMarkerPulseDuration);
        m_hitMarkerImage.rectTransform.localScale = Vector3.one
            * Mathf.Lerp(m_hitMarkerPulseScale, 1f, pulseProgress);
        Color color = m_hitMarkerBaseColor;
        color.a = remaining < k_HitMarkerFadeDuration ? remaining / k_HitMarkerFadeDuration : 1f;
        m_hitMarkerImage.color = color;
    }

    private void UpdatePickupPopups()
    {
        while (m_pickupPopupCount > 0 && m_pickupPopupExpiry[0] <= GameplayClock.Now)
        {
            RemoveOldestPickupPopup();
        }

        for (int index = 0; index < m_pickupPopupCount; index++)
        {
            TextMeshProUGUI popup = m_pickupPopups[index];
            Vector2 target = k_PickupPopupBasePosition + Vector2.up * (m_pickupPopupCount - 1 - index) * k_PickupPopupSpacing;
            popup.rectTransform.anchoredPosition = Vector2.MoveTowards(
                popup.rectTransform.anchoredPosition,
                target,
                k_PickupPopupMoveSpeed * GameplayClock.DeltaTime);

            Color color = popup.color;
            color.a = Mathf.Clamp01((m_pickupPopupExpiry[index] - GameplayClock.Now) / k_PickupPopupFadeDuration);
            popup.color = color;
        }
    }

    private void RemoveOldestPickupPopup()
    {
        TextMeshProUGUI recycled = m_pickupPopups[0];
        recycled.gameObject.SetActive(false);
        for (int index = 1; index < m_pickupPopupCount; index++)
        {
            m_pickupPopups[index - 1] = m_pickupPopups[index];
            m_pickupPopupExpiry[index - 1] = m_pickupPopupExpiry[index];
        }

        m_pickupPopupCount--;
        m_pickupPopups[m_pickupPopupCount] = recycled;
        m_pickupPopupExpiry[m_pickupPopupCount] = 0f;
    }

    private void ClearScoreFeedbacks()
    {
        for (int index = 0; index < m_scoreFeedbackTexts.Length; index++)
        {
            if (m_scoreFeedbackTexts[index] != null)
            {
                m_scoreFeedbackTexts[index].gameObject.SetActive(false);
            }
            m_scoreFeedbackExpiry[index] = 0f;
        }
        m_scoreFeedbackCount = 0;
    }

    private static int GetVisibleComboBulletCount(float remainingSeconds)
    {
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp(remainingSeconds, 0f, k_ComboDuration)), 0, k_ComboBulletCount);
    }

    private static Color GetComboCountColor(int comboCount)
    {
        float progress = Mathf.InverseLerp(1f, 10f, Mathf.Max(1, comboCount));
        return Color.Lerp(Color.white, k_MaxComboColor, progress);
    }

    private Vector2 GetScoreFeedbackTargetPosition(int index)
    {
        return m_scoreFeedbackBasePosition + Vector2.down * index * k_ScoreFeedbackSpacing;
    }

    private static float GetScoreFeedbackAlpha(float expiryTime, float now)
    {
        return Mathf.Clamp01((expiryTime - now) / k_ScoreFeedbackFadeDuration);
    }

    private static void DisableLegacyScoreObject(Transform layer, string objectName)
    {
        Transform legacy = layer.Find(objectName);
        if (legacy != null)
        {
            legacy.gameObject.SetActive(false);
        }
    }

    private static Color GetWeaponColor(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Shotgun => new Color32(53, 199, 89, 255),
            WeaponId.Rifle => new Color32(44, 135, 232, 255),
            WeaponId.DMR => new Color32(44, 135, 232, 255),
            _ => new Color32(234, 64, 71, 255)
        };
    }

    private static string GetWeaponName(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Pistol => "PISTOL",
            WeaponId.Shotgun => "SHOTGUN",
            WeaponId.Rifle => "RIFLE",
            WeaponId.DMR => "DMR",
            _ => "UNKNOWN"
        };
    }

    private Sprite GetWeaponSprite(WeaponId weapon)
    {
        int index = (int)weapon - 1;
        return index >= 0 && index < m_weaponSprites.Length ? m_weaponSprites[index] : null;
    }

    private static Image GetOrCreateImage(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = parent.Find(objectName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            image = imageObject.GetComponent<Image>();
        }

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = Vector2.one;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = Vector2.one;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Image GetImage(string objectPath)
    {
        Transform existing = transform.Find(objectPath);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        Debug.Assert(image != null, $"Missing HUD Image: {objectPath}");
        return image;
    }

    private static void SetBorderState(Image image, bool isActive)
    {
        if (image != null)
        {
            image.color = isActive ? k_ActiveBorderColor : k_InactiveBorderColor;
        }
    }

    private TextMeshProUGUI GetOrCreateText(string objectName, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = transform.Find(objectName);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        text.font = m_font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static string FormatSurvivalTime(int elapsedSeconds)
    {
        int safeSeconds = Mathf.Max(0, elapsedSeconds);
        return $"{safeSeconds / 60:00}:{safeSeconds % 60:00}";
    }
}
