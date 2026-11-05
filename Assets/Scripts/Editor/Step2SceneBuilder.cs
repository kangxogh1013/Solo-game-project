#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 2단계: 1단계 테스트 씬의 Player에 인벤토리를 붙이고 샘플 아이템을 만든다.
/// 메뉴: Tools > 게임 개발 > 2단계: 인벤토리 적용
public static class Step2SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Step1_PlayerTest.unity";
    private const string ItemFolder = "Assets/Data/Items";

    [MenuItem("Tools/게임 개발/2단계: 인벤토리 적용")]
    public static void Apply()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            EditorUtility.DisplayDialog("오류",
                "1단계 씬이 없습니다. 먼저 'Tools > 게임 개발 > 1단계: 플레이어 테스트 씬 생성'을 실행하세요.", "확인");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[Step2SceneBuilder] Player 태그 오브젝트를 찾지 못했습니다.");
            return;
        }

        EnsureFolder(ItemFolder);
        ItemData[] items = CreateSampleItems();

        // 샘플 아이템 시작 수량 (CreateSampleItems 순서와 같아야 함)
        // 나무 조각 16 · 돌 8 · 가죽 투구/갑옷/바지/신발 각 1 · 나무 검 1 · 돌 검 1
        int[] counts = { 16, 8, 1, 1, 1, 1, 1, 1 };

        // 컴포넌트 붙이기 (InventoryUI는 PlayerInventory를 자동으로 요구)
        PlayerInventory inv = player.GetComponent<PlayerInventory>();
        if (inv == null) inv = player.AddComponent<PlayerInventory>();
        if (player.GetComponent<InventoryUI>() == null) player.AddComponent<InventoryUI>();

        // 시작 아이템 목록 연결
        var so = new SerializedObject(inv);
        SerializedProperty list = so.FindProperty("startItems");
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("item").objectReferenceValue = items[i];
            element.FindPropertyRelative("count").intValue = counts[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Step2SceneBuilder] 인벤토리 적용 완료. Play 후 I / 1~9 / U 키를 확인하세요.");
    }

    /// 샘플 아이템 8종 생성 (이미 있으면 값만 갱신)
    private static ItemData[] CreateSampleItems()
    {
        return new[]
        {
            Make("Item_WoodChunk", "나무 조각", ItemCategory.Material, ArmorPart.Head, 0, 0, new Color(0.6f, 0.4f, 0.2f)),
            Make("Item_Stone", "돌", ItemCategory.Material, ArmorPart.Head, 0, 0, new Color(0.5f, 0.5f, 0.5f)),
            Make("Item_LeatherHelmet", "가죽 투구", ItemCategory.Armor, ArmorPart.Head, 0, 1, new Color(0.7f, 0.5f, 0.3f)),
            Make("Item_LeatherChest", "가죽 갑옷", ItemCategory.Armor, ArmorPart.Body, 0, 3, new Color(0.7f, 0.5f, 0.3f)),
            Make("Item_LeatherLegs", "가죽 바지", ItemCategory.Armor, ArmorPart.Legs, 0, 2, new Color(0.7f, 0.5f, 0.3f)),
            Make("Item_LeatherBoots", "가죽 신발", ItemCategory.Armor, ArmorPart.Feet, 0, 1, new Color(0.7f, 0.5f, 0.3f)),
            Make("Item_WoodSword", "나무 검", ItemCategory.Weapon, ArmorPart.Head, 2, 0, new Color(0.8f, 0.6f, 0.3f)),
            Make("Item_StoneSword", "돌 검", ItemCategory.Weapon, ArmorPart.Head, 4, 0, new Color(0.6f, 0.6f, 0.6f)),
        };
    }

    private static ItemData Make(string fileName, string displayName, ItemCategory category,
                                 ArmorPart part, int power, int defense, Color color)
    {
        string path = $"{ItemFolder}/{fileName}.asset";

        var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.Setup(displayName, category, part, power, defense, color);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
