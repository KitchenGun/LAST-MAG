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
    private const float k_PickupPopupDuration = 2.25f;
    private const float k_PickupPopupFadeDuration = 0.3f;
    private const float k_PickupPopupMoveSpeed = 260f;
    private const float k_ScoreFeedbackDuration = 2.25f;
    private const float k_ScoreFeedbackFadeDuration = 0.3f;
    private const float k_ComboDuration = 5f;
    private const float k_ComboPopDuration = 0.14f;
    private const float k_HitMarkerDuration = 0.12f;
    private const float k_HitMarkerFadeDuration = 0.04f;
    private const float k_HitMarkerPulseDuration = 0.06f;
    private const float k_SkillReadyBlinkInterval = 0.5f;
    private const float k_MaxDamageVignetteAlpha = 0.68f;
    private const float k_DeathDamageVignetteAlpha = 0.92f;
    private const float k_DeathTintAlpha = 0.22f;
    private const float k_DamageVignetteIncreaseSpeed = 3.5f;
    private const float k_DamageVignetteRecoverySpeed = 1.2f;
    private const float k_DmrScopeVignetteAlpha = 0.68f;
    private const float k_DmrScopeTransitionDuration = 0.12f;
    private static readonly Color k_MaxComboColor = new Color32(234, 64, 71, 255);
    private static readonly Color k_ActiveBorderColor = new(1f, 1f, 1f, 0.65f);
    private static readonly Color k_InactiveBorderColor = new(0.28f, 0.34f, 0.37f, k_InactiveWeaponAlpha);
    private static readonly Color k_WeaponBackgroundColor = new(0.025f, 0.035f, 0.045f, 1f);
    private static readonly Color k_SkillInactiveColor = new(0.55f, 0.62f, 0.66f, 0.75f);
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
    [SerializeField] private Sprite m_comboBulletEmptySprite;
    [Header("HUD Skin")]
    [SerializeField] private Sprite m_weaponRowInactiveSprite;
    [SerializeField] private Sprite m_weaponRowActiveSprite;
    [SerializeField] private Sprite m_weaponRowEmptySprite;
    [SerializeField] private Sprite m_skillRowReadySprite;
    [Header("Ammo Pickup Colors")]
    [SerializeField] private Color m_pistolAmmoPickupColor = new Color32(234, 64, 71, 255);
    [SerializeField] private Color m_shotgunAmmoPickupColor = new Color32(53, 199, 89, 255);
    [SerializeField] private Color m_rifleAmmoPickupColor = new Color32(44, 135, 232, 255);
    [SerializeField] private Color m_dmrAmmoPickupColor = new Color32(44, 135, 232, 255);

    private readonly TextMeshProUGUI[] m_weaponNumberTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly TextMeshProUGUI[] m_weaponNameTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly TextMeshProUGUI[] m_weaponAmmoTexts = new TextMeshProUGUI[k_HudRowCount];
    private readonly Image[] m_weaponSilhouetteImages = new Image[k_HudRowCount];
    private readonly Image[] m_weaponBorderImages = new Image[k_HudRowCount];
    private readonly Image[] m_weaponBackgroundImages = new Image[k_HudRowCount];
    private readonly Sprite[] m_weaponSprites = new Sprite[4];
    private readonly TextMeshProUGUI[] m_pickupPopups = new TextMeshProUGUI[k_PickupPopupPoolSize];
    private readonly float[] m_pickupPopupExpiry = new float[k_PickupPopupPoolSize];
    private readonly Vector2[] m_pickupPopupSlotPositions = new Vector2[k_PickupPopupPoolSize];
    private readonly Color[] m_pickupPopupBaseColors = new Color[k_PickupPopupPoolSize];
    private readonly TextMeshProUGUI[] m_scoreFeedbackTexts = new TextMeshProUGUI[k_ScoreFeedbackPoolSize];
    private readonly float[] m_scoreFeedbackExpiry = new float[k_ScoreFeedbackPoolSize];
    private readonly Vector2[] m_scoreFeedbackSlotPositions = new Vector2[k_ScoreFeedbackPoolSize];
    private readonly Color[] m_scoreFeedbackBaseColors = new Color[k_ScoreFeedbackPoolSize];
    private readonly Image[] m_comboBulletImages = new Image[k_ComboBulletCount];
    private TextMeshProUGUI m_activeWeaponText;
    private TextMeshProUGUI m_scoreLabel;
    private TextMeshProUGUI m_scoreText;
    private TextMeshProUGUI m_survivalTimeText;
    private TextMeshProUGUI m_comboLabel;
    private TextMeshProUGUI m_comboText;
    private TextMeshProUGUI m_comboDecayText;
    private RectTransform m_comboTextRect;
    private Vector3 m_comboTextBaseScale = Vector3.one;
    private RectTransform m_comboPanel;
    private Image m_comboProgressTrack;
    private Image m_comboProgressFill;
    private Image m_skillCooldownFill;
    private Image m_hitMarkerImage;
    private Vector2 m_hitMarkerBaseSize;
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
    private float m_comboPopStartedAt = -1f;
    private float m_hitMarkerPulseScale = 1f;
    private float m_damageVignetteTargetAlpha;
    private float m_dmrScopeVignetteTargetAlpha;
    private float m_deathPresentationStartedAt = -1f;
    private float m_deathPresentationDuration;
    private int m_pickupPopupCount;
    private int m_scoreFeedbackCount;
    private int m_lastComboCount = -1;
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
            Image silhouette = m_weaponSilhouetteImages[index];
            if (silhouette != null)
            {
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
        UpdateComboTextScale();
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
        numberText.color = weaponColor;
        nameText.color = weaponColor;
        ammoText.color = weaponColor;
        Image silhouette = m_weaponSilhouetteImages[slot - 1];
        if (silhouette != null)
        {
            silhouette.sprite = GetWeaponSprite(weapon);
            silhouette.color = weaponColor;
        }
        SetBorderState(m_weaponBorderImages[slot - 1], isActive);
        SetWeaponBackgroundState(slot - 1, isActive, ammo <= 0);
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
        UpdateSkillCooldownFill(skillName, state, cooldownNormalized, skillSprite);
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
        }
        SetBorderState(m_weaponBorderImages[row], highlighted);
        SetSkillBackgroundState(highlighted);
    }

    public void ShowEmptyAmmoFeedback()
    {
        m_emptyAmmoFeedbackUntil = GameplayClock.Now + k_EmptyAmmoFeedbackDuration;
    }

    public void RefreshScore(int score)
    {
        if (m_scoreText != null)
        {
            m_scoreText.text = $"{Mathf.Max(0, score):000000}";
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
        bool wasVisible = m_comboPanel != null && m_comboPanel.gameObject.activeSelf;
        if (m_comboPanel != null)
        {
            m_comboPanel.gameObject.SetActive(isVisible);
        }
        if (!isVisible)
        {
            m_lastComboCount = safeCount;
            if (m_comboProgressFill != null)
            {
                m_comboProgressFill.fillAmount = 0f;
            }
            m_comboPopStartedAt = -1f;
            if (m_comboTextRect != null)
            {
                m_comboTextRect.localScale = m_comboTextBaseScale;
            }
            return;
        }

        if (m_comboText != null && (m_lastComboCount != safeCount || !wasVisible))
        {
            m_comboText.text = $"x{safeCount}";
            m_lastComboCount = safeCount;
            m_comboPopStartedAt = GameplayClock.Now;
            if (m_comboTextRect != null)
            {
                m_comboTextRect.localScale = m_comboTextBaseScale * 2f;
            }
        }

        Color comboDecayColor = GetComboDecayColor(visibleBullets);
        if (m_comboText != null)
        {
            m_comboText.color = comboDecayColor;
        }

        if (m_comboDecayText != null)
        {
            float secondsUntilNextBullet = GetComboBulletDecaySeconds(remainingSeconds);
            m_comboDecayText.text = $"{secondsUntilNextBullet:0.0}s";
            m_comboDecayText.color = comboDecayColor;
        }

        if (m_comboProgressFill != null)
        {
            m_comboProgressFill.fillAmount = GetComboBulletDecaySeconds(remainingSeconds);
            m_comboProgressFill.color = comboDecayColor;
        }

        for (int index = 0; index < m_comboBulletImages.Length; index++)
        {
            Image bullet = m_comboBulletImages[index];
            if (bullet != null)
            {
                bool filled = index < visibleBullets;
                bullet.sprite = filled ? m_comboBulletSprite : m_comboBulletEmptySprite;
                bullet.color = filled ? comboDecayColor : new Color(0.35f, 0.42f, 0.46f, 0.8f);
                bullet.enabled = bullet.sprite != null;
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
        Color recycledBaseColor = m_scoreFeedbackCount < k_ScoreFeedbackPoolSize
            ? m_scoreFeedbackBaseColors[m_scoreFeedbackCount]
            : m_scoreFeedbackBaseColors[k_ScoreFeedbackPoolSize - 1];
        int newCount = Mathf.Min(m_scoreFeedbackCount + 1, k_ScoreFeedbackPoolSize);
        for (int index = newCount - 1; index > 0; index--)
        {
            m_scoreFeedbackTexts[index] = m_scoreFeedbackTexts[index - 1];
            m_scoreFeedbackExpiry[index] = m_scoreFeedbackExpiry[index - 1];
            m_scoreFeedbackBaseColors[index] = m_scoreFeedbackBaseColors[index - 1];
        }

        m_scoreFeedbackCount = newCount;
        m_scoreFeedbackTexts[0] = recycled;
        m_scoreFeedbackExpiry[0] = GameplayClock.Now + k_ScoreFeedbackDuration + k_ScoreFeedbackFadeDuration;
        m_scoreFeedbackBaseColors[0] = recycledBaseColor;
        string trimmedLabel = label?.Trim();
        recycled.text = string.IsNullOrEmpty(trimmedLabel)
            ? $"<b>+{points}</b>"
            : $"{trimmedLabel}  <b>+{points}</b>";
        recycled.color = recycledBaseColor;
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
        float sizeMultiplier = isKill ? k_KillHitMarkerSize.x / k_NormalHitMarkerSize.x
            : isHeadshot ? k_HeadshotHitMarkerSize.x / k_NormalHitMarkerSize.x : 1f;
        m_hitMarkerImage.rectTransform.sizeDelta = m_hitMarkerBaseSize * sizeMultiplier;
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
        if (m_pickupPopups[0] == null)
        {
            return;
        }

        if (m_pickupPopupCount == k_PickupPopupPoolSize)
        {
            RemoveOldestPickupPopup();
        }

        TextMeshProUGUI popup = m_pickupPopups[m_pickupPopupCount];
        Color popupColor = GetAmmoPickupColor(weapon);
        popup.text = message;
        popup.color = popupColor;
        popup.rectTransform.anchoredPosition = m_pickupPopupSlotPositions[0];
        m_pickupPopupBaseColors[m_pickupPopupCount] = popupColor;
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
        Debug.Assert(m_weaponSilhouetteImages[0].color == new Color32(44, 135, 232, 255));
        if (m_weaponRowActiveSprite != null)
        {
            Debug.Assert(m_weaponBackgroundImages[0].sprite == m_weaponRowActiveSprite);
        }
        RefreshWeapon(1, WeaponId.Rifle, 0, true);
        Debug.Assert(m_emptyAmmoText.enabled);
        if (m_weaponRowEmptySprite != null)
        {
            Debug.Assert(m_weaponBackgroundImages[0].sprite == m_weaponRowEmptySprite);
        }
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
        RefreshWeapon(1, WeaponId.DMR, 15, true);
        Debug.Assert(m_weaponSilhouetteImages[0].color == new Color32(44, 135, 232, 255));
        RefreshSkill("GRENADE", PlayerSkillState.Cooldown, 0.04f);
        Debug.Assert(m_skillCooldownFill.gameObject.activeSelf);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0f));
        Debug.Assert(m_skillCooldownFill.sprite == m_grenadeSkillSilhouette);
        RefreshSkill("GRENADE", PlayerSkillState.Cooldown, 0.16f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0.1f));
        RefreshSkill("ROCKET", PlayerSkillState.Cooldown, 0.56f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 0.5f));
        Debug.Assert(m_skillCooldownFill.sprite == m_rocketSkillSilhouette);
        RefreshSkill("BULLET TIME", PlayerSkillState.Cooldown, 1f);
        Debug.Assert(Mathf.Approximately(m_skillCooldownFill.fillAmount, 1f));
        RefreshSkill("BULLET TIME", PlayerSkillState.Ready, 0f);
        Debug.Assert(!m_skillCooldownFill.gameObject.activeSelf);
        if (m_skillRowReadySprite != null)
        {
            Debug.Assert(m_weaponBackgroundImages[2].sprite == m_skillRowReadySprite);
        }
        Debug.Assert(IsSkillReadyBlinkVisible(0f));
        Debug.Assert(!IsSkillReadyBlinkVisible(k_SkillReadyBlinkInterval));
        Debug.Assert(IsSkillReadyBlinkVisible(k_SkillReadyBlinkInterval * 2f));
        ShowEmptyAmmoFeedback();
        Debug.Assert(m_emptyAmmoFeedbackUntil > GameplayClock.Now);
        int popupCount = m_pickupPopupCount;
        TextMeshProUGUI editorConfiguredPickup = m_pickupPopups[popupCount];
        Vector2 editorConfiguredPickupSize = editorConfiguredPickup.rectTransform.sizeDelta;
        float editorConfiguredPickupFontSize = editorConfiguredPickup.fontSize;
        TextAlignmentOptions editorConfiguredPickupAlignment = editorConfiguredPickup.alignment;
        ShowAmmoPickup(WeaponId.Pistol, 0);
        Debug.Assert(m_pickupPopupCount == popupCount);
        ShowEmptyAmmoPopup(WeaponId.Pistol);
        Debug.Assert(m_pickupPopups[m_pickupPopupCount - 1].text == "EMPTY PISTOL AMMO");
        Debug.Assert(editorConfiguredPickup.rectTransform.anchoredPosition == m_pickupPopupSlotPositions[0]);
        Debug.Assert(editorConfiguredPickup.rectTransform.sizeDelta == editorConfiguredPickupSize);
        Debug.Assert(Mathf.Approximately(editorConfiguredPickup.fontSize, editorConfiguredPickupFontSize));
        Debug.Assert(editorConfiguredPickup.alignment == editorConfiguredPickupAlignment);
        Debug.Assert(editorConfiguredPickup.color == m_pistolAmmoPickupColor);
        ShowHitMarker(false, false);
        Debug.Assert(m_hitMarkerImage.color == k_NormalHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta == m_hitMarkerBaseSize);
        ShowHitMarker(true, false);
        Debug.Assert(m_hitMarkerImage.color == k_HeadshotHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta
            == m_hitMarkerBaseSize * (k_HeadshotHitMarkerSize.x / k_NormalHitMarkerSize.x));
        ShowHitMarker(true, true);
        Debug.Assert(m_hitMarkerImage.color == k_KillHitMarkerColor);
        Debug.Assert(m_hitMarkerImage.rectTransform.sizeDelta
            == m_hitMarkerBaseSize * (k_KillHitMarkerSize.x / k_NormalHitMarkerSize.x));
        Debug.Assert(Mathf.Approximately(m_hitMarkerPulseScale, 1.35f));
        Debug.Assert(m_hitMarkerUntil > GameplayClock.Now);
        SetDmrAimState(true, false);
        Debug.Assert(!m_crosshairImage.enabled && m_hitMarkerImage.gameObject.activeSelf);
        SetDmrAimState(true, true);
        Debug.Assert(m_crosshairImage.enabled
            && Mathf.Approximately(m_dmrScopeVignetteTargetAlpha, k_DmrScopeVignetteAlpha));
        SetDmrAimState(false, false);
        Debug.Assert(m_crosshairImage.enabled && Mathf.Approximately(m_dmrScopeVignetteTargetAlpha, 0f));
        Debug.Assert(m_scoreLabel.text == "SCORE");
        RefreshScore(12480);
        Debug.Assert(m_scoreText.text == "012480");
        Debug.Assert(GetVisibleComboBulletCount(5f) == 5);
        Debug.Assert(GetVisibleComboBulletCount(4.999f) == 5);
        Debug.Assert(GetVisibleComboBulletCount(4f) == 4);
        Debug.Assert(GetVisibleComboBulletCount(1f) == 1);
        Debug.Assert(GetVisibleComboBulletCount(0f) == 0);
        Debug.Assert(Mathf.Approximately(GetComboBulletDecaySeconds(5f), 1f));
        Debug.Assert(Mathf.Approximately(GetComboBulletDecaySeconds(4.2f), 0.2f));
        Debug.Assert(GetComboDecayColor(5) == Color.white);
        Debug.Assert(GetComboDecayColor(1) == k_MaxComboColor);
        RefreshCombo(11, 5f);
        Debug.Assert(m_comboPanel.gameObject.activeSelf && m_comboText.text == "x11");
        Debug.Assert(Mathf.Approximately(m_comboProgressFill.fillAmount, 1f));
        for (int index = 0; index < m_comboBulletImages.Length; index++)
        {
            Debug.Assert(m_comboBulletImages[index].sprite == m_comboBulletSprite);
            Debug.Assert(m_comboBulletImages[index].preserveAspect);
            Debug.Assert(m_comboBulletImages[index].color == Color.white);
            Debug.Assert(m_comboBulletImages[index].enabled == (m_comboBulletSprite != null));
        }
        RefreshCombo(11, 4f);
        Debug.Assert(m_comboBulletImages[4].sprite == m_comboBulletEmptySprite);
        Debug.Assert(m_comboBulletImages[4].enabled == (m_comboBulletEmptySprite != null));
        Debug.Assert(Mathf.Approximately(m_comboProgressFill.fillAmount, 1f));
        TextMeshProUGUI editorConfiguredScoreFeedback = m_scoreFeedbackTexts[0];
        Vector2 editorConfiguredScoreFeedbackSize = editorConfiguredScoreFeedback.rectTransform.sizeDelta;
        float editorConfiguredScoreFeedbackFontSize = editorConfiguredScoreFeedback.fontSize;
        TextAlignmentOptions editorConfiguredScoreFeedbackAlignment = editorConfiguredScoreFeedback.alignment;
        Color editorConfiguredScoreFeedbackColor = m_scoreFeedbackBaseColors[0];
        int pickupCountBeforeScoreFeedback = m_pickupPopupCount;
        for (int index = 1; index <= 7; index++)
        {
            ShowScoreFeedback(index, $"TEST {index}");
        }
        Debug.Assert(m_scoreFeedbackCount == k_ScoreFeedbackPoolSize);
        Debug.Assert(m_scoreFeedbackTexts[0].text == "TEST 7  <b>+7</b>");
        Debug.Assert(m_scoreFeedbackTexts[5].rectTransform.anchoredPosition
            == GetScoreFeedbackTargetPosition(5));
        Debug.Assert(editorConfiguredScoreFeedback.rectTransform.sizeDelta
            == editorConfiguredScoreFeedbackSize);
        Debug.Assert(Mathf.Approximately(editorConfiguredScoreFeedback.fontSize,
            editorConfiguredScoreFeedbackFontSize));
        Debug.Assert(editorConfiguredScoreFeedback.alignment == editorConfiguredScoreFeedbackAlignment);
        Debug.Assert(editorConfiguredScoreFeedback.color == editorConfiguredScoreFeedbackColor);
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

        m_hitMarkerBaseSize = m_hitMarkerImage.rectTransform.sizeDelta;
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

        TextMeshProUGUI[] popups = new TextMeshProUGUI[k_PickupPopupPoolSize];
        for (int index = 0; index < popups.Length; index++)
        {
            Transform existing = layer.Find($"PickupPopup{index}");
            TextMeshProUGUI popup = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (popup == null)
            {
                Debug.Assert(false, $"Missing HUD Text: Layer_PickupFeedback/PickupPopup{index}");
                return;
            }

            popups[index] = popup;
        }

        for (int index = 0; index < popups.Length; index++)
        {
            TextMeshProUGUI popup = popups[index];
            m_pickupPopupSlotPositions[index] = popup.rectTransform.anchoredPosition;
            m_pickupPopupBaseColors[index] = popup.color;
            popup.font = m_font;
            popup.fontSize = 17f;
            popup.fontStyle = FontStyles.Normal;
            popup.alignment = TextAlignmentOptions.Center;
            popup.enableAutoSizing = false;
            popup.textWrappingMode = TextWrappingModes.NoWrap;
            popup.overflowMode = TextOverflowModes.Overflow;
            popup.text = string.Empty;
            popup.gameObject.SetActive(false);
            m_pickupPopups[index] = popup;
        }
    }

    private void InitializeScoreHud()
    {
        Transform layer = transform.Find("Layer_ScoreCombo");
        Debug.Assert(layer != null);
        if (layer == null)
        {
            return;
        }

        m_scoreLabel = layer.Find("ScoreLabel")?.GetComponent<TextMeshProUGUI>();
        m_scoreText = layer.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_scoreLabel != null, "Missing HUD Text: Layer_ScoreCombo/ScoreLabel");
        Debug.Assert(m_scoreText != null, "Missing HUD Text: Layer_ScoreCombo/ScoreText");

        if (m_scoreLabel != null)
        {
            m_scoreLabel.text = "SCORE";
            m_scoreLabel.font = m_font;
            m_scoreLabel.fontSize = 18f;
            m_scoreLabel.alignment = TextAlignmentOptions.MidlineRight;
            m_scoreLabel.raycastTarget = false;
        }
        if (m_scoreText != null)
        {
            m_scoreText.font = m_font;
            m_scoreText.fontSize = 40f;
            m_scoreText.alignment = TextAlignmentOptions.MidlineRight;
            m_scoreText.raycastTarget = false;
        }

        m_comboText = transform.Find("Layer_ScoreCombo/ComboPanel/ComboText")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_comboText != null, "Missing HUD Text: Layer_ScoreCombo/ComboPanel/ComboText");
        m_comboTextRect = m_comboText != null ? m_comboText.rectTransform : null;
        m_comboLabel = transform.Find("Layer_ScoreCombo/ComboPanel/ComboLabel")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_comboLabel != null, "Missing HUD Text: Layer_ScoreCombo/ComboPanel/ComboLabel");
        m_comboDecayText = transform.Find("Layer_ScoreCombo/ComboPanel/ComboDecayText")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_comboDecayText != null, "Missing HUD Text: Layer_ScoreCombo/ComboPanel/ComboDecayText");
        InitializeComboPanel(layer);

        RefreshScore(0);
        m_survivalTimeText = layer.Find("TimeText")?.GetComponent<TextMeshProUGUI>();
        Debug.Assert(m_survivalTimeText != null, "Missing HUD Text: Layer_ScoreCombo/TimeText");
        RefreshCombo(0, 0f);
    }

    private void InitializeComboPanel(Transform layer)
    {
        Transform existingPanel = layer.Find("ComboPanel");
        Debug.Assert(existingPanel != null, "Missing HUD Transform: Layer_ScoreCombo/ComboPanel");
        if (existingPanel == null)
        {
            return;
        }

        m_comboPanel = (RectTransform)existingPanel;
        if (m_comboTextRect != null)
        {
            m_comboTextBaseScale = m_comboTextRect.localScale;
        }
        if (m_comboLabel != null)
        {
            m_comboLabel.text = "COMBO";
            m_comboLabel.font = m_font;
            m_comboLabel.fontSize = 20f;
            m_comboLabel.alignment = TextAlignmentOptions.MidlineLeft;
            m_comboLabel.color = new Color32(75, 226, 255, 255);
            m_comboLabel.raycastTarget = false;
        }
        if (m_comboText != null)
        {
            m_comboText.font = m_font;
            m_comboText.fontSize = 36f;
            m_comboText.alignment = TextAlignmentOptions.MidlineLeft;
            m_comboText.raycastTarget = false;
        }
        if (m_comboDecayText != null)
        {
            m_comboDecayText.font = m_font;
            m_comboDecayText.fontSize = 18f;
            m_comboDecayText.alignment = TextAlignmentOptions.MidlineRight;
            m_comboDecayText.raycastTarget = false;
        }

        for (int index = 0; index < k_ComboBulletCount; index++)
        {
            Transform bulletTransform = m_comboPanel.Find($"ComboBullet{index}");
            Image bullet = bulletTransform != null ? bulletTransform.GetComponent<Image>() : null;
            Debug.Assert(bullet != null, $"Missing HUD Image: Layer_ScoreCombo/ComboPanel/ComboBullet{index}");
            if (bullet == null)
            {
                continue;
            }
            bullet.sprite = m_comboBulletSprite;
            bullet.type = Image.Type.Simple;
            bullet.preserveAspect = true;
            bullet.enabled = false;
            bullet.raycastTarget = false;
            m_comboBulletImages[index] = bullet;
        }

        Transform progressTrackTransform = m_comboPanel.Find("ComboProgressTrack");
        m_comboProgressTrack = progressTrackTransform != null
            ? progressTrackTransform.GetComponent<Image>() : null;
        Debug.Assert(m_comboProgressTrack != null,
            "Missing HUD Image: Layer_ScoreCombo/ComboPanel/ComboProgressTrack");
        if (m_comboProgressTrack != null)
        {
            if (m_comboProgressTrack.sprite == null)
            {
                m_comboProgressTrack.sprite = m_comboBulletEmptySprite;
            }
            m_comboProgressTrack.type = Image.Type.Simple;
            m_comboProgressTrack.preserveAspect = false;
            m_comboProgressTrack.color = new Color(0.22f, 0.28f, 0.31f, 0.9f);
            m_comboProgressTrack.raycastTarget = false;
        }

        Transform progressFillTransform = m_comboPanel.Find("ComboProgressFill");
        m_comboProgressFill = progressFillTransform != null
            ? progressFillTransform.GetComponent<Image>() : null;
        Debug.Assert(m_comboProgressFill != null,
            "Missing HUD Image: Layer_ScoreCombo/ComboPanel/ComboProgressFill");
        if (m_comboProgressFill != null)
        {
            if (m_comboProgressFill.sprite == null)
            {
                m_comboProgressFill.sprite = m_comboBulletSprite;
            }
            m_comboProgressFill.type = Image.Type.Filled;
            m_comboProgressFill.fillMethod = Image.FillMethod.Horizontal;
            m_comboProgressFill.fillOrigin = 0;
            m_comboProgressFill.fillAmount = 0f;
            m_comboProgressFill.preserveAspect = false;
            m_comboProgressFill.raycastTarget = false;
        }

        m_comboPanel.gameObject.SetActive(false);
    }

    private void UpdateComboTextScale()
    {
        if (m_comboTextRect == null || m_comboPopStartedAt < 0f)
        {
            return;
        }

        float progress = Mathf.Clamp01((GameplayClock.Now - m_comboPopStartedAt) / k_ComboPopDuration);
        float scale = Mathf.Lerp(2f, 1f, Mathf.SmoothStep(0f, 1f, progress));
        m_comboTextRect.localScale = m_comboTextBaseScale * scale;
        if (progress >= 1f)
        {
            m_comboTextRect.localScale = m_comboTextBaseScale;
            m_comboPopStartedAt = -1f;
        }
    }

    private void SetWeaponBackgroundState(int row, bool isActive, bool isEmpty)
    {
        if (row < 0 || row >= m_weaponBackgroundImages.Length)
        {
            return;
        }

        Image background = m_weaponBackgroundImages[row];
        if (background == null)
        {
            return;
        }

        Sprite stateSprite = isActive && isEmpty ? m_weaponRowEmptySprite
            : isActive ? m_weaponRowActiveSprite
            : m_weaponRowInactiveSprite;
        if (stateSprite != null)
        {
            background.sprite = stateSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
    }

    private void SetSkillBackgroundState(bool highlighted)
    {
        Image background = m_weaponBackgroundImages[k_HudRowCount - 1];
        if (background == null)
        {
            return;
        }

        Sprite stateSprite = highlighted && m_skillRowReadySprite != null
            ? m_skillRowReadySprite
            : m_weaponRowInactiveSprite;
        if (stateSprite != null)
        {
            background.sprite = stateSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
    }

    private void InitializeScoreFeedbackPool()
    {
        Transform crosshair = transform.Find("Crosshair");
        Debug.Assert(crosshair != null, "Missing HUD Transform: Crosshair");
        if (crosshair == null)
        {
            return;
        }

        Transform widget = crosshair.Find("ScoreFeedbackWidget");
        Debug.Assert(widget != null, "Missing HUD Transform: Crosshair/ScoreFeedbackWidget");
        if (widget == null)
        {
            return;
        }

        TextMeshProUGUI[] feedbacks = new TextMeshProUGUI[k_ScoreFeedbackPoolSize];
        for (int index = 0; index < feedbacks.Length; index++)
        {
            Transform existing = widget.Find($"ScoreFeedback{index}");
            TextMeshProUGUI feedback = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (feedback == null)
            {
                Debug.Assert(false, $"Missing HUD Text: Crosshair/ScoreFeedbackWidget/ScoreFeedback{index}");
                return;
            }

            feedbacks[index] = feedback;
        }

        for (int index = 0; index < feedbacks.Length; index++)
        {
            TextMeshProUGUI feedback = feedbacks[index];
            m_scoreFeedbackSlotPositions[index] = feedback.rectTransform.anchoredPosition;
            m_scoreFeedbackBaseColors[index] = feedback.color;
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

        m_skillCooldownFill = skillSilhouette.transform.Find("WeaponSlot3CooldownFill")?.GetComponent<Image>();
        Debug.Assert(m_skillCooldownFill != null, "Missing HUD Image: WeaponSlot3Silhouette/WeaponSlot3CooldownFill");
        if (m_skillCooldownFill == null)
        {
            return;
        }

        m_skillCooldownFill.type = Image.Type.Filled;
        m_skillCooldownFill.fillMethod = Image.FillMethod.Horizontal;
        m_skillCooldownFill.fillOrigin = 0;
        m_skillCooldownFill.preserveAspect = true;
        m_skillCooldownFill.gameObject.SetActive(false);
    }

    private void UpdateSkillCooldownFill(string skillName, PlayerSkillState state, float cooldownNormalized,
        Sprite skillSprite)
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
        float normalized = Mathf.Clamp01(cooldownNormalized);
        m_skillCooldownFill.fillAmount = normalized >= 1f
            ? 1f : Mathf.Floor(normalized * 10f) * 0.1f;
        m_skillCooldownFill.color = skillName == "GRENADE" ? new Color32(234, 64, 71, 255)
            : skillName == "ROCKET" ? new Color32(53, 199, 89, 255)
            : new Color32(44, 135, 232, 255);
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
            Color color = m_scoreFeedbackBaseColors[index];
            color.a *= GetScoreFeedbackAlpha(m_scoreFeedbackExpiry[index], GameplayClock.Now);
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
            Vector2 target = m_pickupPopupSlotPositions[m_pickupPopupCount - 1 - index];
            popup.rectTransform.anchoredPosition = Vector2.MoveTowards(
                popup.rectTransform.anchoredPosition,
                target,
                k_PickupPopupMoveSpeed * GameplayClock.DeltaTime);

            Color color = m_pickupPopupBaseColors[index];
            float alpha = Mathf.Clamp01((m_pickupPopupExpiry[index] - GameplayClock.Now) / k_PickupPopupFadeDuration);
            color.a *= alpha;
            popup.color = color;
        }
    }

    private void RemoveOldestPickupPopup()
    {
        TextMeshProUGUI recycled = m_pickupPopups[0];
        Color recycledBaseColor = m_pickupPopupBaseColors[0];
        recycled.gameObject.SetActive(false);
        for (int index = 1; index < m_pickupPopupCount; index++)
        {
            m_pickupPopups[index - 1] = m_pickupPopups[index];
            m_pickupPopupExpiry[index - 1] = m_pickupPopupExpiry[index];
            m_pickupPopupBaseColors[index - 1] = m_pickupPopupBaseColors[index];
        }

        m_pickupPopupCount--;
        m_pickupPopups[m_pickupPopupCount] = recycled;
        m_pickupPopupExpiry[m_pickupPopupCount] = 0f;
        m_pickupPopupBaseColors[m_pickupPopupCount] = recycledBaseColor;
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

    private static float GetComboBulletDecaySeconds(float remainingSeconds)
    {
        float clampedSeconds = Mathf.Clamp(remainingSeconds, 0f, k_ComboDuration);
        if (clampedSeconds <= 0f)
        {
            return 0f;
        }

        float fractionalSeconds = clampedSeconds - Mathf.Floor(clampedSeconds);
        return fractionalSeconds > 0.001f ? fractionalSeconds : 1f;
    }

    private static Color GetComboDecayColor(int visibleBullets)
    {
        int clampedBullets = Mathf.Clamp(visibleBullets, 1, k_ComboBulletCount);
        Color orange = new Color32(255, 164, 63, 255);
        return clampedBullets >= 3
            ? Color.Lerp(Color.white, orange, Mathf.InverseLerp(5f, 3f, clampedBullets))
            : Color.Lerp(orange, k_MaxComboColor, Mathf.InverseLerp(3f, 1f, clampedBullets));
    }

    private Vector2 GetScoreFeedbackTargetPosition(int index)
    {
        return m_scoreFeedbackSlotPositions[index];
    }

    private static float GetScoreFeedbackAlpha(float expiryTime, float now)
    {
        return Mathf.Clamp01((expiryTime - now) / k_ScoreFeedbackFadeDuration);
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

    private Color GetAmmoPickupColor(WeaponId weapon)
    {
        return weapon switch
        {
            WeaponId.Shotgun => m_shotgunAmmoPickupColor,
            WeaponId.Rifle => m_rifleAmmoPickupColor,
            WeaponId.DMR => m_dmrAmmoPickupColor,
            _ => m_pistolAmmoPickupColor
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
        bool created = text == null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        if (created)
        {
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
        }
        return text;
    }

    private static string FormatSurvivalTime(int elapsedSeconds)
    {
        int safeSeconds = Mathf.Max(0, elapsedSeconds);
        return $"{safeSeconds / 60:00}:{safeSeconds % 60:00}";
    }
}
