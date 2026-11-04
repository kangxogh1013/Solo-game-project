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
}

/// 플레이어 인벤토리 27칸 + 장비 (방어구 4부위, 무기 1칸)
/// - 선택 칸 이동: 인벤토리를 연 상태에서 마우스 휠 (InventoryUI가 호출)
/// - 장착: U 키 (선택된 칸의 아이템)
public class PlayerInventory : MonoBehaviour
{
    public const int SlotCount = 27;
    private const int ArmorCount = 4;
    private const int BaseAttack = 1; // 맨손 공격력

    [Header("테스트")]
    [Tooltip("Context Menu '테스트 아이템 지급'으로 한 개씩 넣을 아이템들")]
    [SerializeField] private ItemData[] testItems;

    private readonly ItemStack[] slots = new ItemStack[SlotCount];
    private readonly ItemData[] armor = new ItemData[ArmorCount]; // ArmorPart 순서
    private ItemData weapon;

    /// 인벤토리/장비가 바뀔 때 호출 (구독자: InventoryUI)
    public event Action OnChanged;

    public int SelectedIndex { get; private set; }
    public ItemData Weapon => weapon;

    /// 공격력 = 맨손 공격력 + 무기 공격력
    public int AttackPower => BaseAttack + (weapon != null ? weapon.Power : 0);

    /// 방어력 = 장착한 방어구 방어력 합
    public int DefensePower
    {
        get
        {
            int sum = 0;
            foreach (ItemData a in armor)
            {
                if (a != null) sum += a.Power;
            }
            return sum;
        }
    }

    public ItemStack GetSlot(int index) => slots[index];
    public ItemData GetArmor(ArmorPart part) => armor[(int)part];

    private void Awake()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i] = new ItemStack();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.uKey.wasPressedThisFrame) EquipSelected();
    }

    /// 아이템을 넣는다. 같은 아이템 칸을 먼저 채우고 남으면 빈 칸에 넣는다. 못 넣은 개수를 반환.
    public int Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0) return count;
        int remaining = count;

        // 1) 같은 아이템 칸 채우기
        foreach (ItemStack s in slots)
        {
            if (remaining <= 0) break;
            if (s.IsEmpty || s.item != item) continue;

            int add = Mathf.Min(item.MaxStack - s.count, remaining);
            s.count += add;
            remaining -= add;
        }

        // 2) 빈 칸에 넣기
        foreach (ItemStack s in slots)
        {
            if (remaining <= 0) break;
            if (!s.IsEmpty) continue;

            int add = Mathf.Min(item.MaxStack, remaining);
            s.item = item;
            s.count = add;
            remaining -= add;
        }

        NotifyChanged();
        return remaining;
    }

    /// 선택 칸 이동 (끝에서 반대쪽으로 순환)
    public void MoveSelection(int delta)
    {
        SelectedIndex = (SelectedIndex + delta + SlotCount) % SlotCount;
        NotifyChanged();
    }

    /// U 키: 선택 칸의 아이템을 장착한다. 기존 장비는 선택 칸으로 돌아온다.
    public void EquipSelected()
    {
        ItemStack stack = slots[SelectedIndex];
        if (stack.IsEmpty) return;

        ItemData item = stack.item;
        ItemData previous;

        if (item.Category == ItemCategory.Armor)
        {
            int idx = (int)item.Part;
            previous = armor[idx];
            armor[idx] = item;
        }
        else if (item.Category == ItemCategory.Weapon)
        {
            previous = weapon;
            weapon = item;
        }
        else
        {
            Debug.Log($"[PlayerInventory] '{item.DisplayName}'은(는) 장착할 수 없습니다.");
            return;
        }

        // 장착한 1개를 칸에서 뺀다
        stack.count--;
        if (stack.count <= 0)
        {
            stack.item = null;
            stack.count = 0;
        }

        // 기존 장비를 선택 칸에 돌려놓는다 (칸이 비었으면 그 자리, 아니면 빈 칸)
        if (previous != null)
        {
            if (stack.IsEmpty)
            {
                stack.item = previous;
                stack.count = 1;
            }
            else
            {
                Add(previous, 1);
            }
        }

        Debug.Log($"[PlayerInventory] '{item.DisplayName}' 장착 (공격 {AttackPower} / 방어 {DefensePower})");
        NotifyChanged();
    }

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
