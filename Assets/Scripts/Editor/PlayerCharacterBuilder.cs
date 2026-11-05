#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 플레이어를 블록형 캐릭터(머리·몸·팔·다리)로 바꾼다. 기존 캡슐 외형은 제거한다.
/// 몸 조각들은 PlayerController의 bodyRenderers에 등록되어 3인칭에서만 보인다.
/// 메뉴: Tools > 게임 개발 > 플레이어 캐릭터 만들기
public static class PlayerCharacterBuilder
{
    private const string ScenePath = "Assets/Scenes/Step1_PlayerTest.unity";
    private const string MatFolder = "Assets/Art/Player";
    private const string VisualName = "Visual";

    [MenuItem("Tools/게임 개발/플레이어 캐릭터 만들기")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            EditorUtility.DisplayDialog("오류",
                "1단계 씬이 없습니다. 먼저 'Tools > 게임 개발 > 1단계'를 실행하세요.", "확인");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[PlayerCharacterBuilder] Player 태그 오브젝트를 찾지 못했습니다.");
            return;
        }

        // 기존 캡슐 외형과 이전 Visual 제거
        var capsuleRenderer = player.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) Object.DestroyImmediate(capsuleRenderer);
        var capsuleMesh = player.GetComponent<MeshFilter>();
        if (capsuleMesh != null) Object.DestroyImmediate(capsuleMesh);

        Transform oldVisual = player.transform.Find(VisualName);
        if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

        // 색 재질 준비 (Assets/Art/Player 에 저장)
        EnsureFolder(MatFolder);
        Material skin = MakeMat("Mat_Skin", new Color(0.95f, 0.80f, 0.65f));
        Material hair = MakeMat("Mat_Hair", new Color(0.25f, 0.15f, 0.08f));
        Material shirt = MakeMat("Mat_Shirt", new Color(0.20f, 0.50f, 0.90f));
        Material pants = MakeMat("Mat_Pants", new Color(0.25f, 0.25f, 0.30f));
        Material shoes = MakeMat("Mat_Shoes", new Color(0.15f, 0.12f, 0.10f));
        Material eye = MakeMat("Mat_Eye", new Color(0.08f, 0.08f, 0.08f));

        // 조각 생성 (발바닥 y = -1, 머리 윗면 y = 1 → CharacterController 높이 2와 일치)
        var visual = new GameObject(VisualName).transform;
        visual.SetParent(player.transform, false);

        var bodyParts = new List<Renderer>();
        bodyParts.Add(AddPart(visual, "Head",     new Vector3(0f, 0.775f, 0f),    new Vector3(0.45f, 0.45f, 0.45f), skin));
        bodyParts.Add(AddPart(visual, "Hair",     new Vector3(0f, 0.985f, -0.02f), new Vector3(0.47f, 0.08f, 0.47f), hair));
        bodyParts.Add(AddPart(visual, "EyeL",     new Vector3(-0.1f, 0.8f, 0.23f), new Vector3(0.08f, 0.08f, 0.02f), eye));
        bodyParts.Add(AddPart(visual, "EyeR",     new Vector3(0.1f, 0.8f, 0.23f),  new Vector3(0.08f, 0.08f, 0.02f), eye));
        bodyParts.Add(AddPart(visual, "Body",     new Vector3(0f, 0.175f, 0f),     new Vector3(0.50f, 0.75f, 0.30f), shirt));
        bodyParts.Add(AddPart(visual, "ArmL",     new Vector3(-0.35f, 0.175f, 0f), new Vector3(0.20f, 0.75f, 0.20f), shirt));
        bodyParts.Add(AddPart(visual, "ArmR",     new Vector3(0.35f, 0.175f, 0f),  new Vector3(0.20f, 0.75f, 0.20f), shirt));
        bodyParts.Add(AddPart(visual, "LegL",     new Vector3(-0.13f, -0.6f, 0f),  new Vector3(0.22f, 0.80f, 0.25f), pants));
        bodyParts.Add(AddPart(visual, "LegR",     new Vector3(0.13f, -0.6f, 0f),   new Vector3(0.22f, 0.80f, 0.25f), pants));
        bodyParts.Add(AddPart(visual, "ShoeL",    new Vector3(-0.13f, -0.94f, 0.02f), new Vector3(0.24f, 0.12f, 0.30f), shoes));
        bodyParts.Add(AddPart(visual, "ShoeR",    new Vector3(0.13f, -0.94f, 0.02f),  new Vector3(0.24f, 0.12f, 0.30f), shoes));

        // PlayerController가 3인칭에서만 보여 줄 렌더러 목록 연결
        var controller = player.GetComponent<PlayerController>();
        var so = new SerializedObject(controller);
        SerializedProperty list = so.FindProperty("bodyRenderers");
        list.arraySize = bodyParts.Count;
        for (int i = 0; i < bodyParts.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = bodyParts[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[PlayerCharacterBuilder] 플레이어 캐릭터 생성 완료. V 키로 1·3인칭 전환을 확인하세요.");
    }

    /// 블록 조각 하나 생성 (충돌체는 제거: 캐릭터 이동을 막지 않도록)
    private static Renderer AddPart(Transform parent, string partName, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;

        Object.DestroyImmediate(go.GetComponent<BoxCollider>());

        Renderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return r;
    }

    /// 재질이 있으면 색만 갱신, 없으면 새로 만든다 (URP Lit 기준)
    private static Material MakeMat(string matName, Color color)
    {
        string path = $"{MatFolder}/{matName}.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
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
