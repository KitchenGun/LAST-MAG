using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayHUD : MonoBehaviour
{
    private const int k_WeaponSlotCount = 3;
    private const float k_EmptyAmmoFeedbackDuration = 0.2f;

    [SerializeField] private Font m_font;

    private readonly string[] m_weaponSlots = new string[k_WeaponSlotCount];
    private PlayerHealth m_playerHealth;
    private Text m_healthText;
    private Text m_activeWeaponText;
    private Text m_weaponSlotsText;
    private float m_emptyAmmoFeedbackUntil;

    private void Awake()
    {
        Debug.Assert(m_font != null);
        if (m_font == null)
        {
            enabled = false;
            return;
        }

        m_healthText = GetOrCreateText("Health", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), 32, TextAnchor.UpperLeft);
        m_activeWeaponText = GetOrCreateText("ActiveWeapon", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 32f), 32, TextAnchor.LowerRight);
        m_weaponSlotsText = GetOrCreateText("WeaponSlots", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 124f), 20, TextAnchor.LowerRight);

        RefreshWeapon(1, "PISTOL", 0, false);
        RefreshWeapon(2, "SHOTGUN", 0, false);
        RefreshWeapon(3, "RIFLE", 0, false);
        m_activeWeaponText.text = "WEAPON --\n--";
    }

    private void Update()
    {
        if (m_playerHealth == null)
        {
            m_playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (m_playerHealth != null)
        {
            RefreshHealth(m_playerHealth.CurrentHealth, m_playerHealth.MaxHealth);
        }

        m_activeWeaponText.color = Time.unscaledTime < m_emptyAmmoFeedbackUntil ? Color.red : Color.white;
    }

    public void RefreshHealth(float currentHealth, float maxHealth)
    {
        m_healthText.text = $"HP {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
    }

    public void RefreshWeapon(int slot, string weaponName, int ammo, bool isActive)
    {
        Debug.Assert(slot >= 1 && slot <= k_WeaponSlotCount);
        if (slot < 1 || slot > k_WeaponSlotCount)
        {
            return;
        }

        m_weaponSlots[slot - 1] = $"{slot}  {weaponName}";
        m_weaponSlotsText.text = string.Join("\n", m_weaponSlots);
        if (isActive)
        {
            m_activeWeaponText.text = $"{weaponName}\n{ammo}";
        }
    }

    public void ShowEmptyAmmoFeedback()
    {
        m_emptyAmmoFeedbackUntil = Time.unscaledTime + k_EmptyAmmoFeedbackDuration;
    }

    [ContextMenu("Run HUD Self Check")]
    private void RunHudSelfCheck()
    {
        Debug.Assert(k_WeaponSlotCount == 3);
        RefreshWeapon(1, "PISTOL", 60, true);
        Debug.Assert(m_activeWeaponText.text == "PISTOL\n60");
        RefreshWeapon(0, "INVALID", 0, false);
        ShowEmptyAmmoFeedback();
        Debug.Assert(m_emptyAmmoFeedbackUntil > Time.unscaledTime);
    }

    private Text GetOrCreateText(string objectName, Vector2 anchor, Vector2 pivot, Vector2 position, int fontSize, TextAnchor alignment)
    {
        Transform existing = transform.Find(objectName);
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(transform, false);
            text = textObject.GetComponent<Text>();
        }

        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = new Vector2(420f, 88f);
        text.font = m_font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
