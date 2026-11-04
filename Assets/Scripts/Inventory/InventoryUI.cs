using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// 마크 스타일 인벤토리 UI (런타임에 자동 생성, Inspector 설정 불필요)
/// - 핫바: 화면 하단에 항상 표시, 선택 칸은 노란 테두리
/// - 인벤토리(I): 가방 27칸 + 핫바 + 방어구 4칸 + 무기 1칸
/// 조작: 좌클릭 집기/놓기/교환 · 우클릭 절반 집기/1개 놓기 · U 장착 · 1~9 또는 휠 핫바 선택 · I 닫기
[RequireComponent(typeof(PlayerInventory))]
public class InventoryUI : MonoBehaviour
{
    /// 인벤토리 창이 열려 있는지 (PlayerController가 슬롯 클릭과 충돌하지 않도록 확인)
    public static bool IsOpen { get; private set; }

    private const float SlotSize = 56f;
    private const float Step = 62f; // 칸 크기 + 간격

    private PlayerInventory inventory;
    private GameObject canvasGo;
    private RectTransform canvasRect;
    private GameObject invPanel;
    private TMP_Text statsText;
    private RectTransform carriedRoot;
    private Image carriedBg;
    private TMP_Text carriedLabel;

    private readonly SlotView[] views = new SlotView[PlayerInventory.TotalSlots];
    private readonly SlotView[] hudViews = new SlotView[PlayerInventory.HotbarSize];
    private readonly StringBuilder sb = new StringBuilder(128); // 재사용 (GC 방지)

    /// 칸 하나의 UI 묶음
    private class SlotView
    {
        public RectTransform rect;
        public Image frame;      // 선택 테두리
        public Image bg;         // 아이템 색 (블록 아이콘 대용)
        public TMP_Text label;   // 아이템 이름 2글자
        public TMP_Text count;   // 개수
        public string emptyHint; // 빈 장비 칸 안내 (머리, 몸 등)
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        BuildCanvas();
        BuildHud();
        BuildPanel();
        BuildCarried();

        SetOpen(false);
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.OnChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.iKey.wasPressedThisFrame) SetOpen(!IsOpen);
        if (!IsOpen) return;

        Vector2 pos = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame) ClickAt(pos, false);
        else if (mouse.rightButton.wasPressedThisFrame) ClickAt(pos, true);

        // 커서에 든 아이템은 마우스를 따라다닌다
        if (carriedRoot.gameObject.activeSelf) carriedRoot.position = pos;
    }

    // ───────────────────────── 열기 / 닫기 ─────────────────────────

    private void SetOpen(bool open)
    {
        IsOpen = open;
        invPanel.SetActive(open);

        // 열면 커서를 풀어 클릭할 수 있게, 닫으면 다시 잠근다
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (!open) inventory.ReturnCarried();
        Refresh();
    }

    // ───────────────────────── 클릭 ─────────────────────────

    /// 클릭 위치에 있는 칸을 찾아 PlayerInventory에 전달한다
    private void ClickAt(Vector2 screenPos, bool rightClick)
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(views[i].rect, screenPos, null))
            {
                inventory.ClickSlot(i, rightClick);
                return;
            }
        }
    }

    // ───────────────────────── 화면 갱신 ─────────────────────────

    private void Refresh()
    {
        int selected = inventory.SelectedHotbar;

        for (int i = 0; i < views.Length; i++)
        {
            bool isHotbar = i < PlayerInventory.HotbarSize;
            PaintSlot(views[i], inventory.GetSlot(i), isHotbar && i == selected);
        }

        for (int i = 0; i < hudViews.Length; i++)
        {
            PaintSlot(hudViews[i], inventory.GetSlot(i), i == selected);
        }

        sb.Clear();
        sb.Append("공격력 ").Append(inventory.AttackPower)
          .Append("     방어력 ").Append(inventory.DefensePower);
        statsText.text = sb.ToString();

        ItemStack c = inventory.Carried;
        bool showCarried = IsOpen && !c.IsEmpty;
        carriedRoot.gameObject.SetActive(showCarried);
        if (showCarried)
        {
            carriedBg.color = c.item.IconColor;
            carriedLabel.text = c.count > 1 ? $"{Short(c.item.DisplayName)}\nx{c.count}" : Short(c.item.DisplayName);
        }
    }

    private static void PaintSlot(SlotView v, ItemStack s, bool selected)
    {
        v.frame.enabled = selected;

        if (s.IsEmpty)
        {
            v.bg.color = new Color(0.14f, 0.14f, 0.16f, 0.9f);
            v.label.text = v.emptyHint;
            v.label.color = new Color(1f, 1f, 1f, 0.35f);
            v.count.text = string.Empty;
        }
        else
        {
            v.bg.color = s.item.IconColor;
            v.label.text = Short(s.item.DisplayName);
            v.label.color = Color.white;
            v.count.text = s.count > 1 ? s.count.ToString() : string.Empty;
        }
    }

    private static string Short(string name)
    {
        return name.Length <= 2 ? name : name.Substring(0, 2);
    }

    // ───────────────────────── UI 생성 ─────────────────────────

    private void BuildCanvas()
    {
        canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasRect = (RectTransform)canvasGo.transform;
    }

    /// 화면 하단 중앙의 핫바 (항상 표시)
    private void BuildHud()
    {
        RectTransform hud = NewRect("Hud", canvasRect, Vector2.zero, 0f, 0f);
        hud.anchorMin = new Vector2(0.5f, 0f);
        hud.anchorMax = new Vector2(0.5f, 0f);
        hud.anchoredPosition = new Vector2(0f, 30f);

        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            hudViews[i] = CreateSlot(hud, new Vector2((i - 4) * Step, 0f), string.Empty);
        }
    }

    /// 인벤토리 창 (I 키). 왼쪽: 방어구 4 + 무기, 가운데: 가방 27 + 핫바 9
    private void BuildPanel()
    {
        RectTransform panel = NewRect("InvPanel", canvasRect, Vector2.zero, 1000f, 560f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
        invPanel = panel.gameObject;

        statsText = CreateTmp(Stretch(NewRect("Stats", panel, new Vector2(0f, 230f), 960f, 50f), 0f), 26f, TextAlignmentOptions.Center);

        TMP_Text hint = CreateTmp(Stretch(NewRect("Hint", panel, new Vector2(0f, -225f), 960f, 40f), 0f), 20f, TextAlignmentOptions.Center);
        hint.text = "좌클릭: 집기/놓기   우클릭: 절반/1개   U: 장착   1~9/휠: 핫바   I: 닫기";

        // 방어구 4칸 + 무기 (왼쪽 열)
        string[] armorHints = { "머리", "몸", "다리", "발" };
        float[] armorY = { 130f, 68f, 6f, -56f };
        for (int k = 0; k < 4; k++)
        {
            views[PlayerInventory.ArmorStart + k] = CreateSlot(panel, new Vector2(-400f, armorY[k]), armorHints[k]);
        }
        views[PlayerInventory.WeaponSlot] = CreateSlot(panel, new Vector2(-400f, -120f), "무기");

        // 가방 27칸 (9 x 3) + 핫바 9칸
        const float gridX = -160f;
        float[] rowY = { 110f, 48f, -14f };
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                views[PlayerInventory.BagStart + r * 9 + c] = CreateSlot(panel, new Vector2(gridX + c * Step, rowY[r]), string.Empty);
            }
        }
        for (int c = 0; c < 9; c++)
        {
            views[c] = CreateSlot(panel, new Vector2(gridX + c * Step, -120f), string.Empty);
        }
    }

    /// 커서를 따라다니는 아이템 (가장 위에 그려지도록 마지막에 만든다)
    private void BuildCarried()
    {
        carriedRoot = NewRect("Carried", canvasRect, Vector2.zero, SlotSize, SlotSize);
        carriedBg = carriedRoot.gameObject.AddComponent<Image>();
        carriedBg.raycastTarget = false;
        carriedLabel = CreateTmp(Stretch(NewRect("Label", carriedRoot, Vector2.zero, 0f, 0f), 2f), 14f, TextAlignmentOptions.Center);
        carriedRoot.gameObject.SetActive(false);
    }

    private SlotView CreateSlot(Transform parent, Vector2 pos, string emptyHint)
    {
        var v = new SlotView { emptyHint = emptyHint };

        v.rect = NewRect("Slot", parent, pos, SlotSize, SlotSize);
        v.frame = v.rect.gameObject.AddComponent<Image>();
        v.frame.color = new Color(1f, 0.85f, 0.3f);
        v.frame.enabled = false;

        RectTransform bgRt = NewRect("Bg", v.rect, Vector2.zero, SlotSize - 6f, SlotSize - 6f);
        v.bg = bgRt.gameObject.AddComponent<Image>();

        v.label = CreateTmp(Stretch(NewRect("Label", bgRt, Vector2.zero, 0f, 0f), 2f), 16f, TextAlignmentOptions.Center);
        v.count = CreateTmp(Stretch(NewRect("Count", bgRt, Vector2.zero, 0f, 0f), 3f), 14f, TextAlignmentOptions.BottomRight);

        return v;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    /// 부모 영역을 꽉 채운다 (pad는 안쪽 여백)
    private static RectTransform Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    private static TMP_Text CreateTmp(RectTransform rt, float size, TextAlignmentOptions align)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }
}
