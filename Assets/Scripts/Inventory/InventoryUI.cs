using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// 인벤토리 UI (런타임에 자동 생성, Inspector 설정 불필요)
/// - 하단 핫바: 항상 표시
/// - I 키: 인벤토리 창 열기/닫기 (마인크래프트 배치)
/// - 창이 열리면 커서가 풀리고 시점 회전이 멈춘다
/// - 좌클릭: 전체 집기/놓기 · 우클릭: 절반 집기/1개 놓기
[RequireComponent(typeof(PlayerInventory))]
public class InventoryUI : MonoBehaviour
{
    /// 인벤토리 창이 열려 있는지
    public static bool IsOpen { get; private set; }

    private const float SlotSize = 56f;
    private const float Step = 62f;          // 칸 간격 (칸 56 + 여백 6)
    private const float HotbarHudY = 56f;    // 화면 아래에서 HUD 핫바 중심 높이
    private const float PanelHotbarY = -124f; // 창 안 핫바 줄 높이

    private static readonly Color NormalColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
    private static readonly Color SelectedColor = new Color(0.95f, 0.75f, 0.2f, 0.95f);

    /// 칸 하나의 화면 요소 묶음
    private sealed class SlotView
    {
        public RectTransform rt;
        public Image bg;
        public Image icon;
        public TMP_Text count;
    }

    private PlayerInventory inv;

    private GameObject canvasGo;
    private RectTransform canvasRt;
    private RectTransform panel;
    private RectTransform carriedRt;
    private Image carriedIcon;
    private TMP_Text carriedCount;

    private readonly SlotView[] panelViews = new SlotView[PlayerInventory.TotalSlots];
    private readonly SlotView[] hotbarViews = new SlotView[PlayerInventory.HotbarSize];

    private void Awake()
    {
        inv = GetComponent<PlayerInventory>();
        BuildUI();
    }

    private void OnEnable()
    {
        inv.OnChanged += RefreshAll;
        inv.OnHotbarChanged += RefreshAll;
    }

    private void Start()
    {
        RefreshAll();
    }

    private void OnDisable()
    {
        if (inv != null)
        {
            inv.OnChanged -= RefreshAll;
            inv.OnHotbarChanged -= RefreshAll;
        }

        if (IsOpen)
        {
            IsOpen = false;
            PlayerController.IsUiOpen = false;
        }
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

        if (keyboard.iKey.wasPressedThisFrame) Toggle();
        if (!IsOpen) return;

        Vector2 screen = mouse.position.ReadValue();
        MoveCarried(screen);

        if (mouse.leftButton.wasPressedThisFrame) HandleClick(screen, false);
        else if (mouse.rightButton.wasPressedThisFrame) HandleClick(screen, true);
    }

    // ───────────────────────── 열기 / 닫기 ─────────────────────────

    private void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void Open()
    {
        IsOpen = true;
        PlayerController.IsUiOpen = true;   // 시점 회전과 커서 잠금 차단
        panel.gameObject.SetActive(true);
        SetCursorLocked(false);
        RefreshAll();
    }

    private void Close()
    {
        IsOpen = false;
        PlayerController.IsUiOpen = false;
        panel.gameObject.SetActive(false);

        inv.ReturnCarried();                // 들고 있던 아이템은 가방으로
        carriedRt.gameObject.SetActive(false);
        SetCursorLocked(true);
    }

    // ───────────────────────── 클릭 ─────────────────────────

    /// 화면 좌표가 어떤 칸 위에 있는지 찾아 인벤토리에 전달한다
    /// (EventSystem 없이 직접 판정하므로 씬에 EventSystem이 없어도 된다)
    private void HandleClick(Vector2 screen, bool rightClick)
    {
        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(panelViews[i].rt, screen, null))
            {
                inv.ClickSlot(i, rightClick);
                return;
            }
        }
    }

    private void MoveCarried(Vector2 screen)
    {
        if (!carriedRt.gameObject.activeSelf) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local);
        carriedRt.anchoredPosition = local;
    }

    // ───────────────────────── 화면 갱신 ─────────────────────────

    private void RefreshAll()
    {
        if (inv == null) return;

        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
        {
            ShowSlot(panelViews[i], inv.GetSlot(i));
        }

        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            ShowSlot(hotbarViews[i], inv.GetSlot(i));

            // 선택된 핫바 칸은 노란색으로 강조 (HUD와 창 안 핫바 둘 다)
            Color c = i == inv.SelectedHotbar ? SelectedColor : NormalColor;
            hotbarViews[i].bg.color = c;
            panelViews[i].bg.color = c;
        }

        ShowCarried();
    }

    private static void ShowSlot(SlotView v, ItemStack s)
    {
        bool has = !s.IsEmpty;
        v.icon.enabled = has;

        if (!has)
        {
            v.count.text = "";
            return;
        }

        v.icon.color = s.item.IconColor;
        v.count.text = s.count > 1 ? s.count.ToString() : "";
    }

    private void ShowCarried()
    {
        ItemStack c = inv.Carried;
        bool show = IsOpen && !c.IsEmpty;
        carriedRt.gameObject.SetActive(show);
        if (!show) return;

        carriedIcon.color = c.item.IconColor;
        carriedCount.text = c.count > 1 ? c.count.ToString() : "";
    }

    // ───────────────────────── UI 생성 ─────────────────────────

    private void BuildUI()
    {
        canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRt = (RectTransform)canvasGo.transform;
        Transform root = canvasGo.transform;

        // 1) 하단 핫바 HUD (항상 보임)
        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            hotbarViews[i] = CreateSlot(root, new Vector2(0.5f, 0f),
                new Vector2((i - 4) * Step, HotbarHudY), SlotSize);
        }

        // 2) 인벤토리 창 (I 키로 열기)
        panel = NewRect("InventoryPanel", root, new Vector2(0.5f, 0.5f), Vector2.zero, 1000f, 560f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 0.94f);
        CreateTitle(panel, "인벤토리", new Vector2(0f, 250f));

        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
        {
            panelViews[i] = CreateSlot(panel, new Vector2(0.5f, 0.5f), PanelPos(i), SlotSize);
        }
        panel.gameObject.SetActive(false);

        // 3) 마우스를 따라다니는 '들고 있는 아이템' (창 뒤에 만들어 가장 위에 그린다)
        carriedRt = NewRect("Carried", root, new Vector2(0.5f, 0.5f), Vector2.zero, SlotSize, SlotSize);
        carriedIcon = carriedRt.gameObject.AddComponent<Image>();
        carriedIcon.raycastTarget = false;

        RectTransform countRt = NewRect("Count", carriedRt, new Vector2(0.5f, 0.5f), Vector2.zero, SlotSize, SlotSize);
        Stretch(countRt);
        carriedCount = AddText(countRt, 14f, TextAlignmentOptions.BottomRight);
        carriedRt.gameObject.SetActive(false);
    }

    /// 칸 번호 → 창 안의 위치 (창 중심 기준)
    private static Vector2 PanelPos(int index)
    {
        // 핫바 줄 (0~8)
        if (index < PlayerInventory.BagStart)
        {
            return new Vector2(-160f + index * Step, PanelHotbarY);
        }

        // 가방 3줄 (9~35)
        if (index < PlayerInventory.ArmorStart)
        {
            int b = index - PlayerInventory.BagStart;
            return new Vector2(-160f + (b % 9) * Step, 140f - (b / 9) * Step);
        }

        // 방어구 4칸 (36~39): 왼쪽 세로 줄
        if (index < PlayerInventory.WeaponSlot)
        {
            return new Vector2(-400f, 140f - (index - PlayerInventory.ArmorStart) * Step);
        }

        // 무기 칸 (40): 왼쪽 아래
        return new Vector2(-400f, PanelHotbarY);
    }

    private static SlotView CreateSlot(Transform parent, Vector2 anchor, Vector2 pos, float size)
    {
        RectTransform rt = NewRect("Slot", parent, anchor, pos, size, size);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = NormalColor;

        RectTransform iconRt = NewRect("Icon", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size - 12f, size - 12f);
        var icon = iconRt.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.enabled = false;

        RectTransform countRt = NewRect("Count", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, size);
        Stretch(countRt);
        TMP_Text count = AddText(countRt, 14f, TextAlignmentOptions.BottomRight);

        return new SlotView { rt = rt, bg = bg, icon = icon, count = count };
    }

    private static void CreateTitle(RectTransform parent, string text, Vector2 pos)
    {
        RectTransform rt = NewRect("Title", parent, new Vector2(0.5f, 0.5f), pos, 600f, 50f);
        TMP_Text tmp = AddText(rt, 28f, TextAlignmentOptions.Center);
        tmp.text = text;
    }

    private static TMP_Text AddText(RectTransform rt, float fontSize, TextAlignmentOptions align)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
