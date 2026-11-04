using UnityEngine;

/// 아이템 분류
public enum ItemCategory { Material, Armor, Weapon }

/// 방어구 장착 부위 (머리·몸·바지·신발)
public enum ArmorPart { Head, Body, Legs, Feet }

/// 아이템 데이터 (ScriptableObject). Project 창 우클릭 > Create > 기억 로비 > Item Data로 만든다.
[CreateAssetMenu(fileName = "Item_", menuName = "기억 로비/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string displayName = "아이템";
    [SerializeField] private ItemCategory category = ItemCategory.Material;
    [Tooltip("칸에 표시될 색 (마크 스타일 블록 아이콘 대용)")]
    [SerializeField] private Color iconColor = new Color(0.6f, 0.45f, 0.3f);
    [Tooltip("한 칸에 쌓을 수 있는 최대 개수")]
    [SerializeField, Min(1)] private int maxStack = 99;

    [Header("장비 전용")]
    [Tooltip("방어구일 때 장착할 부위")]
    [SerializeField] private ArmorPart part = ArmorPart.Head;
    [Tooltip("무기는 공격력, 방어구는 방어력으로 쓰인다")]
    [SerializeField, Min(0)] private int power = 1;

    public string DisplayName => displayName;
    public ItemCategory Category => category;
    public Color IconColor => iconColor;
    public int MaxStack => maxStack;
    public ArmorPart Part => part;
    public int Power => power;
}
