using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed class GameplayHUD : MonoBehaviour
{
    private const int k_WeaponSlotCount = 3;
    private const int k_PickupPopupPoolSize = 4;
    private const float k_EmptyAmmoFeedbackDuration = 0.2f;
    private const float k_InactiveWeaponAlpha = 0.4f;
    private const float k_ActiveWeaponFontSize = 22f;
    private const float k_InactiveWeaponFontSize = 17f;
    private const float k_PickupPopupDuration = 2.25f;
    private const float k_PickupPopupFadeDuration = 0.3f;
    private const float k_PickupPopupSpacing = 38f;
    private const float k_PickupPopupMoveSpeed = 260f;
    private const float k_ScoreFeedbackDuration = 1.1f;
    private const float k_ScoreFeedbackFadeDuration = 0.25f;
    private static readonly Vector2 k_PickupPopupBasePosition = new(-170f, 48f);
    private static readonly Vector2 k_WeaponBorderSize = new(500f, 54f);
    private static readonly Vector2 k_WeaponBackgroundSize = new(496f, 50f);
    private static readonly Color k_ActiveBorderColor = new(1f, 1f, 1f, 0.65f);
    private static readonly Color k_InactiveBorderColor = new(0.28f, 0.34f, 0.37f, k_InactiveWeaponAlpha);
    private static readonly Color k_WeaponBackgroundColor = new(0.025f, 0.035f, 0.045f, 1f);

    [SerializeField] private TMP_FontAsset m_font;

    private readonly TextMeshProUGUI[] m_weaponNumberTexts = new TextMeshProUGUI[k_WeaponSlotCount];
    private readonly TextMeshProUGUI[] m_weaponNameTexts = new TextMeshProUGUI[k_WeaponSlotCount];
    private readonly TextMeshProUGUI[] m_weaponAmmoTexts = new TextMeshProUGUI[k_WeaponSlotCount];
    private readonly Image[] m_weaponSilhouetteImages = new Image[k_WeaponSlotCount];
    private readonly Image[] m_weaponBorderImages = new Image[k_WeaponSlotCount];
    private readonly Image[] m_weaponBackgroundImages = new Image[k_WeaponSlotCount];
    private readonly TextMeshProUGUI[] m_pickupPopups = new TextMeshProUGUI[k_PickupPopupPoolSize];
    private readonly float[] m_pickupPopupExpiry = new float[k_PickupPopupPoolSize];
    private TextMeshProUGUI m_activeWeaponText;
    private TextMeshProUGUI m_scoreText;
    private TextMeshProUGUI m_comboText;
    private TextMeshProUGUI m_scoreFeedbackText;
    private Image m_comboGaugeFill;
    private Color m_activeWeaponBaseColor = Color.white;
    private float m_emptyAmmoFeedbackUntil;
    private float m_scoreFeedbackUntil;
    private int m_pickupPopupCount;

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

        m_weaponNumberTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-345f, 164f), new Vector2(50f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNumberTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-345f, 98f), new Vector2(50f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNumberTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Number", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-345f, 38f), new Vector2(50f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNameTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-125f, 164f), new Vector2(90f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNameTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-125f, 98f), new Vector2(90f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponNameTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Name", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-125f, 38f), new Vector2(90f, 34f), 17, TextAlignmentOptions.Center);
        m_weaponAmmoTexts[0] = GetOrCreateText("Layer_WeaponText/WeaponSlot1Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 164f), new Vector2(58f, 34f), 17, TextAlignmentOptions.MidlineRight);
        m_weaponAmmoTexts[1] = GetOrCreateText("Layer_WeaponText/WeaponSlot2Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 98f), new Vector2(58f, 34f), 17, TextAlignmentOptions.MidlineRight);
        m_weaponAmmoTexts[2] = GetOrCreateText("Layer_WeaponText/WeaponSlot3Ammo", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 38f), new Vector2(58f, 34f), 17, TextAlignmentOptions.MidlineRight);
        for (int index = 0; index < k_WeaponSlotCount; index++)
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
        }

        InitializePickupPopupPool();
        InitializeScoreHud();

        RefreshWeapon(1, "PISTOL", 0, false);
        RefreshWeapon(2, "SHOTGUN", 0, false);
        RefreshWeapon(3, "RIFLE", 0, false);
    }

    private void Update()
    {
        if (m_activeWeaponText != null)
        {
            m_activeWeaponText.color = Time.unscaledTime < m_emptyAmmoFeedbackUntil ? Color.red : m_activeWeaponBaseColor;
        }

        UpdatePickupPopups();
        UpdateScoreFeedback();
    }

    public void RefreshWeapon(int slot, string weaponName, int ammo, bool isActive)
    {
        Debug.Assert(slot >= 1 && slot <= k_WeaponSlotCount);
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        TextMeshProUGUI numberText = m_weaponNumberTexts[slot - 1];
        TextMeshProUGUI nameText = m_weaponNameTexts[slot - 1];
        TextMeshProUGUI ammoText = m_weaponAmmoTexts[slot - 1];
        Color weaponColor = GetWeaponColor(slot);
        weaponColor.a = isActive ? 1f : k_InactiveWeaponAlpha;
        numberText.text = slot.ToString();
        nameText.text = weaponName;
        ammoText.text = ammo.ToString();
        float fontSize = isActive ? k_ActiveWeaponFontSize : k_InactiveWeaponFontSize;
        numberText.fontSize = fontSize;
        nameText.fontSize = fontSize;
        ammoText.fontSize = fontSize;
        numberText.color = weaponColor;
        nameText.color = weaponColor;
        ammoText.color = weaponColor;
        SetImageAlpha(m_weaponSilhouetteImages[slot - 1], weaponColor.a);
        SetBorderState(m_weaponBorderImages[slot - 1], isActive);
        if (isActive)
        {
            m_activeWeaponText = ammoText;
            m_activeWeaponBaseColor = weaponColor;
        }
    }

    public void ShowEmptyAmmoFeedback()
    {
        m_emptyAmmoFeedbackUntil = Time.unscaledTime + k_EmptyAmmoFeedbackDuration;
    }

    public void RefreshScore(int score)
    {
        if (m_scoreText != null)
        {
            m_scoreText.text = $"SCORE  {Mathf.Max(0, score):000000}";
        }
    }

    public void RefreshCombo(int level, float multiplier, float remainingNormalized)
    {
        if (m_comboText != null)
        {
            m_comboText.text = $"COMBO  {Mathf.Clamp(level, 0, 10)}  x{Mathf.Max(1f, multiplier):0.0}";
        }
        if (m_comboGaugeFill != null)
        {
            m_comboGaugeFill.fillAmount = Mathf.Clamp01(remainingNormalized);
        }
    }

    public void ShowScoreFeedback(int points, string reason)
    {
        if (m_scoreFeedbackText == null || points <= 0)
        {
            return;
        }

        m_scoreFeedbackText.text = $"+{points}  {reason}";
        m_scoreFeedbackText.color = reason.StartsWith("PERFECT")
            ? new Color32(255, 210, 72, 255)
            : reason.StartsWith("CHAIN") ? new Color32(255, 112, 72, 255) : Color.white;
        m_scoreFeedbackText.gameObject.SetActive(true);
        m_scoreFeedbackUntil = Time.unscaledTime + k_ScoreFeedbackDuration;
    }

    public void ShowAmmoPickup(int slot, int amount)
    {
        if (slot < 1 || slot > k_WeaponSlotCount || amount <= 0)
        {
            return;
        }

        string message = slot switch
        {
            1 => $"+{amount}  PISTOL AMMO",
            2 => $"+{amount}  SHOTGUN AMMO",
            _ => $"+{amount}  RIFLE AMMO"
        };
        ShowAmmoPopup(slot, message);
    }

    public void ShowEmptyAmmoPopup(int slot)
    {
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        string message = slot switch
        {
            1 => "EMPTY PISTOL AMMO",
            2 => "EMPTY SHOTGUN AMMO",
            _ => "EMPTY RIFLE AMMO"
        };
        ShowAmmoPopup(slot, message);
    }

    private void ShowAmmoPopup(int slot, string message)
    {
        if (m_pickupPopupCount == k_PickupPopupPoolSize)
        {
            RemoveOldestPickupPopup();
        }

        TextMeshProUGUI popup = m_pickupPopups[m_pickupPopupCount];
        popup.text = message;
        popup.color = GetWeaponColor(slot);
        popup.rectTransform.anchoredPosition = k_PickupPopupBasePosition;
        popup.gameObject.SetActive(true);
        m_pickupPopupExpiry[m_pickupPopupCount] = Time.unscaledTime + k_PickupPopupDuration;
        m_pickupPopupCount++;
    }

    [ContextMenu("Run HUD Self Check")]
    private void RunHudSelfCheck()
    {
        Debug.Assert(k_WeaponSlotCount == 3);
        RefreshWeapon(1, "PISTOL", 60, true);
        Debug.Assert(m_activeWeaponText.text == "60");
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[0].color.a, 1f));
        Debug.Assert(Mathf.Approximately(m_weaponSilhouetteImages[0].color.a, 1f));
        Debug.Assert(m_weaponBorderImages[0].color == k_ActiveBorderColor);
        Debug.Assert(m_weaponBorderImages[0].rectTransform.sizeDelta == k_WeaponBorderSize);
        Debug.Assert(m_weaponBackgroundImages[0].color == k_WeaponBackgroundColor);
        Debug.Assert(m_weaponBackgroundImages[0].rectTransform.sizeDelta == k_WeaponBackgroundSize);
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[0].fontSize, k_ActiveWeaponFontSize));
        RefreshWeapon(2, "SHOTGUN", 24, false);
        Debug.Assert(Mathf.Approximately(m_weaponAmmoTexts[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(Mathf.Approximately(m_weaponSilhouetteImages[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(Mathf.Approximately(m_weaponBorderImages[1].color.a, k_InactiveWeaponAlpha));
        Debug.Assert(m_weaponBorderImages[1].color == k_InactiveBorderColor);
        Debug.Assert(m_weaponBorderImages[1].rectTransform.sizeDelta == k_WeaponBorderSize);
        Debug.Assert(Mathf.Approximately(m_weaponNumberTexts[1].fontSize, k_InactiveWeaponFontSize));
        RefreshWeapon(0, "INVALID", 0, false);
        ShowEmptyAmmoFeedback();
        Debug.Assert(m_emptyAmmoFeedbackUntil > Time.unscaledTime);
        int popupCount = m_pickupPopupCount;
        ShowAmmoPickup(1, 0);
        Debug.Assert(m_pickupPopupCount == popupCount);
        ShowEmptyAmmoPopup(1);
        Debug.Assert(m_pickupPopups[m_pickupPopupCount - 1].text == "EMPTY PISTOL AMMO");
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

        GetOrCreateImage(layer, "ComboGaugeBackground", new Vector2(-48f, -108f), new Vector2(360f, 8f), new Color32(35, 45, 52, 220));
        m_comboGaugeFill = GetOrCreateImage(layer, "ComboGaugeFill", new Vector2(-48f, -108f), new Vector2(360f, 8f), new Color32(92, 220, 255, 255));
        m_comboGaugeFill.type = Image.Type.Filled;
        m_comboGaugeFill.fillMethod = Image.FillMethod.Horizontal;
        m_comboGaugeFill.fillOrigin = 0;

        Transform existingFeedback = layer.Find("ScoreFeedbackText");
        if (existingFeedback == null)
        {
            GameObject feedbackObject = new("ScoreFeedbackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            feedbackObject.transform.SetParent(layer, false);
            existingFeedback = feedbackObject.transform;
        }
        m_scoreFeedbackText = existingFeedback.GetComponent<TextMeshProUGUI>();
        RectTransform feedbackRect = m_scoreFeedbackText.rectTransform;
        feedbackRect.anchorMin = Vector2.one;
        feedbackRect.anchorMax = Vector2.one;
        feedbackRect.pivot = Vector2.one;
        feedbackRect.anchoredPosition = new Vector2(-48f, -122f);
        feedbackRect.sizeDelta = new Vector2(360f, 30f);
        m_scoreFeedbackText.font = m_font;
        m_scoreFeedbackText.fontSize = 20f;
        m_scoreFeedbackText.alignment = TextAlignmentOptions.MidlineRight;
        m_scoreFeedbackText.raycastTarget = false;
        m_scoreFeedbackText.gameObject.SetActive(false);

        RefreshScore(0);
        RefreshCombo(0, 1f, 0f);
    }

    private void UpdateScoreFeedback()
    {
        if (m_scoreFeedbackText == null || !m_scoreFeedbackText.gameObject.activeSelf)
        {
            return;
        }

        if (Time.unscaledTime >= m_scoreFeedbackUntil)
        {
            m_scoreFeedbackText.gameObject.SetActive(false);
            return;
        }

        Color color = m_scoreFeedbackText.color;
        color.a = Mathf.Clamp01((m_scoreFeedbackUntil - Time.unscaledTime) / k_ScoreFeedbackFadeDuration);
        m_scoreFeedbackText.color = color;
    }

    private void UpdatePickupPopups()
    {
        while (m_pickupPopupCount > 0 && m_pickupPopupExpiry[0] <= Time.unscaledTime)
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
                k_PickupPopupMoveSpeed * Time.unscaledDeltaTime);

            Color color = popup.color;
            color.a = Mathf.Clamp01((m_pickupPopupExpiry[index] - Time.unscaledTime) / k_PickupPopupFadeDuration);
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

    private static Color GetWeaponColor(int slot)
    {
        return slot switch
        {
            2 => new Color32(53, 199, 89, 255),
            3 => new Color32(44, 135, 232, 255),
            _ => new Color32(234, 64, 71, 255)
        };
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

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
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
}
