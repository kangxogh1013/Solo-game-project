#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 1단계 테스트 씬(바닥, 계단, 플레이어, HUD, 카메라)을 메뉴 한 번으로 만든다.
/// 메뉴: Tools > 게임 개발 > 1단계: 플레이어 테스트 씬 생성
public static class Step1SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Step1_PlayerTest.unity";

    [MenuItem("Tools/게임 개발/1단계: 플레이어 테스트 씬 생성")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("테스트 씬 생성",
                "현재 씬을 닫고 Step1_PlayerTest 씬을 새로 만듭니다. 계속할까요?", "생성", "취소"))
        {
            return;
        }

        EnsureFolder("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

        CreateLight();
        CreateGround();
        CreateSteps();
        CreatePlayer();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[Step1SceneBuilder] 테스트 씬 생성 완료: " + ScenePath);
    }

    private static void CreateLight()
    {
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    /// 바닥: 윗면이 y = 0이 되도록 배치
    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
    }

    /// 점프 테스트용 계단 (0.4 / 0.8 / 1.2 / 1.6 m)
    private static void CreateSteps()
    {
        Transform parent = new GameObject("TestSteps").transform;

        for (int i = 0; i < 4; i++)
        {
            float height = 0.4f * (i + 1);

            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{height:0.0}m";
            step.transform.SetParent(parent, false);
            step.transform.localScale = new Vector3(4f, height, 2f);
            step.transform.position = new Vector3(0f, height * 0.5f, 6f + i * 2.5f);
        }
    }

    /// 플레이어: 캡슐 + CharacterController + PlayerController + PlayerStats + PlayerHud
    /// 자식으로 HeadPivot과 Main Camera를 만들고 참조까지 연결한다.
    private static void CreatePlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1f, -5f);

        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        var controller = player.AddComponent<PlayerController>(); // PlayerStats는 자동 추가
        player.AddComponent<PlayerHud>();

        // 눈 높이
        var head = new GameObject("HeadPivot").transform;
        head.SetParent(player.transform, false);
        head.localPosition = new Vector3(0f, 0.6f, 0f);

        // 카메라 (눈 높이에 붙음)
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();
        camGo.transform.SetParent(head, false);
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.localRotation = Quaternion.identity;

        // PlayerController 참조 연결
        var so = new SerializedObject(controller);
        so.FindProperty("headPivot").objectReferenceValue = head;
        so.FindProperty("cameraTransform").objectReferenceValue = camGo.transform;

        SerializedProperty bodies = so.FindProperty("bodyRenderers");
        bodies.arraySize = 1;
        bodies.GetArrayElementAtIndex(0).objectReferenceValue = player.GetComponent<MeshRenderer>();

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 공통 ─────────────────────────

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void AddToBuildSettings(string scenePath)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in list)
        {
            if (s.path == scenePath) return;
        }

        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
