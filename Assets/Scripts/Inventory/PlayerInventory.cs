using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// 슬롯 하나에 들어가는 아이템 묶음
[Serializable]
public class ItemStack
{
    public ItemData item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public void Clear()
    {
        item = null;
        count = 0;
    }
}

/// 시작 시 지급할 아이템 (Inspector에서 설정)
[Serializable]
public struct StartItem
{
    public ItemData item;
    [Min(1)] public int count;
}

/// 플레이어 인벤토리 데이터 (마인크래프트 구조)
/// 슬롯: 0~8 핫바 · 9~35 가방 · 36~39 방어구(머리/몸/다리/발) · 40 무기
/// 입력: 1~9 핫바 선택 · 마우스 휠 핫바 이동 · U 선택한 장비 장착
/// 화면 조작(클릭)은 InventoryUI가 ClickSlot을 호출해서 처리한다.
public class PlayerInventory : MonoBehaviour
{
    public const int HotbarSize = 9;
    public const int BagStart = 9;
    public const int BagSize = 27;
    public const int ArmorStart = 36;
    public const int ArmorCount = 4;
    public const int WeaponSlot = 40;
    public const int TotalSlots = 41;

    private static readonly Key[] HotbarKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    [Header("시작 아이템")]
    [SerializeField] private StartItem[] startItems;

    [Header("전투력")]
    [Tooltip("맨손 공격력")]
    [SerializeField, Min(0)] private int baseAttack = 1;

    /// 슬롯 내용이 바뀔 때 호출 (구독자: InventoryUI)
    public event Action OnChanged;

    /// 선택 핫바가 바뀔 때 호출
    public event Action OnHotbarChanged;

    private ItemStack[] slots;
    private readonly ItemStack carried = new ItemStack(); // 마우스에 들고 있는 아이템
    private int selectedHotbar;

    public ItemStack Carried => carried;
    public int SelectedHotbar => selectedHotbar;
    public ItemStack GetSlot(int index) => slots[index];

    /// 공격력 = 맨손 공격력 + 무기 공격력
    public int AttackPower
    {
        get
        {
            ItemStack weapon = slots[WeaponSlot];
            return baseAttack + (weapon.IsEmpty ? 0 : weapon.item.Power);
        }
    }

    /// 방어력 = 방어구 4칸의 방어력 합
    public int DefensePower
    {
        get
        {
            int sum = 0;
            for (int i = ArmorStart; i < ArmorStart + ArmorCount; i++)
            {
                if (!slots[i].IsEmpty) sum += slots[i].item.Defense;
            }
            return sum;
        }
    }

    private void Awake()
    {
        slots = new ItemStack[TotalSlots];
        for (int i = 0; i < TotalSlots; i++) slots[i] = new ItemStack();

        if (startItems != null)
        {
            foreach (StartItem s in startItems) Add(s.item, s.count);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // 1~9 키로 핫바 선택
        for (int i = 0; i < HotbarSize; i++)
        {
            if (keyboard[HotbarKeys[i]].wasPressedThisFrame) SelectHotbar(i);
        }

        // 마우스 휠로 핫바 이동
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0.1f) SelectHotbar(selectedHotbar - 1);
        else if (scroll < -0.1f) SelectHotbar(selectedHotbar + 1);

        // U: 선택한 핫바 아이템이 장비면 장착
        if (keyboard.uKey.wasPressedThisFrame) EquipSelected();
    }

    // ───────────────────────── 핫바 ─────────────────────────

    /// 핫바 선택 (범위를 벗어나면 반대쪽으로 돈다)
    public void SelectHotbar(int index)
    {
        int wrapped = ((index % HotbarSize) + HotbarSize) % HotbarSize;
        if (wrapped == selectedHotbar) return;

        selectedHotbar = wrapped;
        OnHotbarChanged?.Invoke();
    }

    // ───────────────────────── 추가 / 제거 ─────────────────────────

    /// 아이템을 넣는다. 같은 아이템 칸을 먼저 채우고, 남으면 빈 칸에 넣는다.
    /// 반환값은 넣지 못한 개수다.
    public int Add(ItemData item, int count)
    {
        if (item == null || count <= 0) return 0;

        int left = count;
        int lastSlot = BagStart + BagSize; // 핫바 + 가방 (0~35)

        // 1) 같은 아이템 칸 채우기
        for (int i = 0; i < lastSlot && left > 0; i++)
        {
            ItemStack s = slots[i];
            if (s.IsEmpty || s.item != item) continue;

            int put = Mathf.Min(item.MaxStack - s.count, left);
            s.count += put;
            left -= put;
        }

        // 2) 빈 칸에 넣기
        for (int i = 0; i < lastSlot && left > 0; i++)
        {
            ItemStack s = slots[i];
            if (!s.IsEmpty) continue;

            int put = Mathf.Min(item.MaxStack, left);
            s.item = item;
            s.count = put;
            left -= put;
        }

        NotifyChanged();
        return left;
    }

    // ───────────────────────── 클릭 (InventoryUI가 호출) ─────────────────────────

    /// 좌클릭: 전체 집기 / 놓기 / 교체
    /// 우클릭: 절반 집기 / 1개 놓기
    public void ClickSlot(int index, bool rightClick)
    {
        if (index < 0 || index >= TotalSlots) return;
        ItemStack slot = slots[index];

        if (carried.IsEmpty)
        {
            if (slot.IsEmpty) return;

            if (rightClick)
            {
                // 절반 집기 (홀수면 올림)
                int half = (slot.count + 1) / 2;
                carried.item = slot.item;
                carried.count = half;
                slot.count -= half;
                if (slot.count <= 0) slot.Clear();
            }
            else
            {
                // 전부 집기
                carried.item = slot.item;
                carried.count = slot.count;
                slot.Clear();
            }
        }
        else
        {
            if (rightClick) PlaceOne(index);
            else PlaceAll(index);
        }

        NotifyChanged();
    }

    /// 들고 있는 아이템 전부를 칸에 놓는다 (같은 재료면 합치고, 다르면 교체)
    private void PlaceAll(int index)
    {
        ItemStack slot = slots[index];
        if (!Accepts(index, carried.item)) return;

        // 빈 칸: 통째로 놓기
        if (slot.IsEmpty)
        {
            slot.item = carried.item;
            slot.count = carried.count;
            carried.Clear();
            return;
        }

        // 같은 재료: 최대 스택까지 합치기
        if (slot.item == carried.item && slot.item.MaxStack > 1)
        {
            int move = Mathf.Min(slot.item.MaxStack - slot.count, carried.count);
            slot.count += move;
            carried.count -= move;
            if (carried.count <= 0) carried.Clear();
            return;
        }

        // 다른 아이템: 서로 교체
        ItemData slotItem = slot.item;
        int slotCount = slot.count;
        slot.item = carried.item;
        slot.count = carried.count;
        carried.item = slotItem;
        carried.count = slotCount;
    }

    /// 들고 있는 아이템을 1개만 놓는다
    private void PlaceOne(int index)
    {
        ItemStack slot = slots[index];
        if (!Accepts(index, carried.item)) return;

        if (slot.IsEmpty)
        {
            slot.item = carried.item;
            slot.count = 1;
        }
        else if (slot.item == carried.item && slot.count < slot.item.MaxStack)
        {
            slot.count++;
        }
        else
        {
            return;
        }

        carried.count--;
        if (carried.count <= 0) carried.Clear();
    }

    /// 인벤토리를 닫을 때 들고 있던 아이템을 가방으로 돌려보낸다
    public void ReturnCarried()
    {
        if (carried.IsEmpty) return;

        ItemData item = carried.item;
        int count = carried.count;
        carried.Clear();

        int left = Add(item, count);
        if (left > 0)
        {
            Debug.LogWarning($"[PlayerInventory] 인벤토리가 가득 차서 {item.DisplayName} {left}개를 되돌리지 못했습니다.");
        }
    }

    // ───────────────────────── 장비 ─────────────────────────

    /// 선택한 핫바 아이템이 장비면 맞는 칸과 교체한다
    public void EquipSelected()
    {
        ItemStack hand = slots[selectedHotbar];
        if (hand.IsEmpty) return;

        int target = GetEquipSlot(hand.item);
        if (target < 0)
        {
            Debug.Log($"[PlayerInventory] {hand.item.DisplayName} 은(는) 장비가 아닙니다.");
            return;
        }

        // 두 칸의 내용을 서로 바꾼다 (장비는 1개씩)
        ItemStack equipped = slots[target];
        (hand.item, equipped.item) = (equipped.item, hand.item);
        (hand.count, equipped.count) = (equipped.count, hand.count);

        NotifyChanged();
    }

    /// 이 아이템이 들어갈 장비 칸 번호 (장비가 아니면 -1)
    private static int GetEquipSlot(ItemData item)
    {
        switch (item.Category)
        {
            case ItemCategory.Armor: return ArmorStart + (int)item.Part;
            case ItemCategory.Weapon: return WeaponSlot;
            default: return -1;
        }
    }

    /// 해당 칸에 이 아이템을 넣어도 되는지 (장비 칸은 맞는 종류만 허용)
    public bool Accepts(int index, ItemData item)
    {
        if (item == null) return true;

        if (index == WeaponSlot) return item.Category == ItemCategory.Weapon;

        if (index >= ArmorStart && index < ArmorStart + ArmorCount)
        {
            return item.Category == ItemCategory.Armor && GetEquipSlot(item) == index;
        }

        return true;
    }

    // ───────────────────────── 공통 ─────────────────────────

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    [ContextMenu("공격력 / 방어력 로그 출력")]
    private void LogPower()
    {
        Debug.Log($"[PlayerInventory] 공격력 {AttackPower} / 방어력 {DefensePower}");
    }
}
