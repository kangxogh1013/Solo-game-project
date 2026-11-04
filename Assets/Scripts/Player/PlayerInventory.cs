using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// 슬롯 한 칸 (아이템 + 개수)
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

/// 마크 스타일 인벤토리 데이터
/// 슬롯 구성: 0~8 핫바 · 9~35 가방 · 36~39 방어구(머리/몸/다리/발) · 40 무기
/// - 칸 클릭 규칙(집기/놓기/합치기/교환), 핫바 선택, U 장착을 여기서 처리한다.
public class PlayerInventory : MonoBehaviour
{
    public const int HotbarSize = 9;
    public const int BagStart = 9;
    public const int InventorySize = 36;   // 핫바 + 가방
    public const int ArmorStart = 36;      // 36~39
    public const int WeaponSlot = 40;
    public const int TotalSlots = 41;

    private const int BaseAttack = 1;      // 맨손 공격력

    [Header("테스트")]
    [Tooltip("Context Menu '테스트 아이템 지급'으로 한 개씩 넣을 아이템")]
    [SerializeField] private ItemData[] testItems;

    private readonly ItemStack[] slots = new ItemStack[TotalSlots];
    private readonly ItemStack carried = new ItemStack(); // 마우스에 붙어 다니는 아이템
    private int selectedHotbar;

    /// 내용이 바뀔 때 호출 (구독자: InventoryUI)
    public event Action OnChanged;

    public int SelectedHotbar => selectedHotbar;
    public ItemStack Carried => carried;

    /// 공격력 = 맨손 1 + 무기 공격력
    public int AttackPower
    {
        get
        {
            ItemStack weapon = slots[WeaponSlot];
            return BaseAttack + (weapon.IsEmpty ? 0 : weapon.item.Power);
        }
    }

    /// 방어력 = 방어구 4칸 방어력 합
    public int DefensePower
    {
        get
        {
            int sum = 0;
            for (int k = 0; k < 4; k++)
            {
                ItemStack armor = slots[ArmorStart + k];
                if (!armor.IsEmpty) sum += armor.item.Power;
            }
            return sum;
        }
    }

    public ItemStack GetSlot(int index) => slots[index];

    private void Awake()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            slots[i] = new ItemStack();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null) return;

        // 숫자키 1~9 로 핫바 선택
        if (keyboard.digit1Key.wasPressedThisFrame) SelectHotbar(0);
        if (keyboard.digit2Key.wasPressedThisFrame) SelectHotbar(1);
        if (keyboard.digit3Key.wasPressedThisFrame) SelectHotbar(2);
        if (keyboard.digit4Key.wasPressedThisFrame) SelectHotbar(3);
        if (keyboard.digit5Key.wasPressedThisFrame) SelectHotbar(4);
        if (keyboard.digit6Key.wasPressedThisFrame) SelectHotbar(5);
        if (keyboard.digit7Key.wasPressedThisFrame) SelectHotbar(6);
        if (keyboard.digit8Key.wasPressedThisFrame) SelectHotbar(7);
        if (keyboard.digit9Key.wasPressedThisFrame) SelectHotbar(8);

        // 마우스 휠로 핫바 이동
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0f) MoveHotbar(-1);
            else if (scroll < 0f) MoveHotbar(1);
        }

        // U: 선택한 핫바 아이템을 장비 칸에 장착
        if (keyboard.uKey.wasPressedThisFrame) EquipHeld();
    }

    // ───────────────────────── 핫바 ─────────────────────────

    public void SelectHotbar(int index)
    {
        selectedHotbar = (index + HotbarSize) % HotbarSize;
        NotifyChanged();
    }

    public void MoveHotbar(int delta)
    {
        SelectHotbar(selectedHotbar + delta);
    }

    // ───────────────────────── 아이템 추가 ─────────────────────────

    /// 가방/핫바에 넣는다. 같은 아이템 칸을 먼저 채우고, 남으면 빈 칸에 넣는다. 못 넣은 개수를 반환.
    public int Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return count;
        int remaining = count;

        for (int i = 0; i < InventorySize && remaining > 0; i++)
        {
            ItemStack s = slots[i];
            if (s.IsEmpty || s.item != item || s.count >= item.MaxStack) continue;

            int add = Mathf.Min(item.MaxStack - s.count, remaining);
            s.count += add;
            remaining -= add;
        }

        for (int i = 0; i < InventorySize && remaining > 0; i++)
        {
            ItemStack s = slots[i];
            if (!s.IsEmpty) continue;

            int add = Mathf.Min(item.MaxStack, remaining);
            s.item = item;
            s.count = add;
            remaining -= add;
        }

        NotifyChanged();
        return remaining;
    }

    // ───────────────────────── 칸 클릭 (마크 방식) ─────────────────────────

    /// 칸 클릭 처리
    /// 좌클릭: 집기 / 놓기 / 같은 아이템 합치기 / 교환
    /// 우클릭: 절반 집기 / 1개 놓기
    public void ClickSlot(int slot, bool rightClick)
    {
        ItemStack target = slots[slot];

        if (!rightClick)
        {
            if (carried.IsEmpty)
            {
                if (target.IsEmpty) return;
                Move(target, carried, target.count);
            }
            else if (!target.IsEmpty && target.item == carried.item)
            {
                int move = Mathf.Min(carried.item.MaxStack - target.count, carried.count);
                target.count += move;
                carried.count -= move;
                if (carried.count <= 0) carried.Clear();
            }
            else if (!Accepts(slot, carried.item))
            {
                return;
            }
            else if (target.IsEmpty)
            {
                Move(carried, target, carried.count);
            }
            else
            {
                // 교환: 칸의 아이템과 커서의 아이템을 바꾼다
                ItemData tItem = target.item;
                int tCount = target.count;
                target.item = carried.item;
                target.count = carried.count;
                carried.item = tItem;
                carried.count = tCount;
            }
        }
        else
        {
            if (carried.IsEmpty)
            {
                if (target.IsEmpty) return;
                Move(target, carried, (target.count + 1) / 2);
            }
            else if (target.IsEmpty || (target.item == carried.item && target.count < carried.item.MaxStack))
            {
                if (!Accepts(slot, carried.item)) return;

                if (target.IsEmpty) target.item = carried.item;
                target.count++;
                carried.count--;
                if (carried.count <= 0) carried.Clear();
            }
        }

        NotifyChanged();
    }

    /// 칸이 해당 아이템을 받을 수 있는지 (방어구는 맞는 부위, 무기는 무기 칸만)
    public bool Accepts(int slot, ItemData item)
    {
        if (item == null) return true;

        if (slot >= ArmorStart && slot < ArmorStart + 4)
        {
            return item.Category == ItemCategory.Armor && (int)item.Part == slot - ArmorStart;
        }

        if (slot == WeaponSlot)
        {
            return item.Category == ItemCategory.Weapon;
        }

        return true;
    }

    /// U: 선택한 핫바 아이템을 맞는 장비 칸에 넣는다. 기존 장비는 핫바 칸으로 돌아온다.
    public void EquipHeld()
    {
        ItemStack held = slots[selectedHotbar];
        if (held.IsEmpty) return;

        int dest = EquipSlotFor(held.item);
        if (dest < 0)
        {
            Debug.Log($"[PlayerInventory] '{held.item.DisplayName}'은(는) 장착할 수 없습니다.");
            return;
        }

        ItemStack equip = slots[dest];
        ItemData oldItem = equip.item;
        int oldCount = equip.count;

        equip.item = held.item;
        equip.count = 1;
        held.count--;
        if (held.count <= 0) held.Clear();

        if (oldItem != null)
        {
            if (held.IsEmpty)
            {
                held.item = oldItem;
                held.count = oldCount;
            }
            else
            {
                Add(oldItem, oldCount);
            }
        }

        Debug.Log($"[PlayerInventory] '{equip.item.DisplayName}' 장착 (공격 {AttackPower} / 방어 {DefensePower})");
        NotifyChanged();
    }

    /// 인벤토리를 닫을 때 커서에 든 아이템을 가방에 돌려놓는다
    public void ReturnCarried()
    {
        if (carried.IsEmpty) return;

        int left = Add(carried.item, carried.count);
        if (left > 0) carried.count = left;
        else carried.Clear();
    }

    /// 아이템이 갈 장비 칸 번호 (장비가 아니면 -1)
    public static int EquipSlotFor(ItemData item)
    {
        if (item.Category == ItemCategory.Armor) return ArmorStart + (int)item.Part;
        if (item.Category == ItemCategory.Weapon) return WeaponSlot;
        return -1;
    }

    /// from에서 amount개를 to로 옮긴다 (to는 비어 있어야 한다)
    private static void Move(ItemStack from, ItemStack to, int amount)
    {
        to.item = from.item;
        to.count = amount;
        from.count -= amount;
        if (from.count <= 0) from.Clear();
    }

    // ───────────────────────── 테스트 ─────────────────────────

    [ContextMenu("테스트 아이템 지급")]
    private void TestGiveItems()
    {
        if (testItems == null) return;

        foreach (ItemData item in testItems)
        {
            if (item != null) Add(item, 1);
        }
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
