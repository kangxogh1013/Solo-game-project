using UnityEngine;

/// 아이템 종류 구분
public enum ItemCategory { Material, Armor, Weapon }

/// 방어구 장착 부위 (순서 중요: Head=0, Body=1, Legs=2, Feet=3)
public enum ArmorPart { Head, Body, Legs, Feet }

/// 아이템 한 종류의 정보 (ScriptableObject)
/// 데이터와 로직을 분리하기 위해 값은 에셋에 저장한다.
[CreateAssetMenu(menuName = "Solo/Item Data", fileName = "Item_New")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string displayName = "아이템";
    [SerializeField] private ItemCategory category = ItemCategory.Material;

    [Tooltip("임시 아이콘 색 (나중에 Sprite로 교체)")]
    [SerializeField] private Color iconColor = Color.white;

    [Tooltip("재료만 적용. 장비는 항상 1개씩")]
    [SerializeField, Range(1, 99)] private int maxStack = 64;

    [Header("장비 정보")]
    [Tooltip("방어구일 때 장착 부위")]
    [SerializeField] private ArmorPart armorPart = ArmorPart.Head;

    [Tooltip("무기 공격력 보너스")]
    [SerializeField, Min(0)] private int power = 0;

    [Tooltip("방어구 방어력")]
    [SerializeField, Min(0)] private int defense = 0;

    public string DisplayName => displayName;
    public ItemCategory Category => category;
    public Color IconColor => iconColor;
    public int MaxStack => category == ItemCategory.Material ? maxStack : 1;
    public ArmorPart Part => armorPart;
    public int Power => power;
    public int Defense => defense;

    /// 에디터 툴이 아이템 값을 채울 때 사용한다.
    public void Setup(string name, ItemCategory cat, ArmorPart part, int atk, int def, Color color)
    {
        displayName = name;
        category = cat;
        armorPart = part;
        power = atk;
        defense = def;
        iconColor = color;
    }
}
