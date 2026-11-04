#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// 메뉴 한 번으로 로비 씬(방, 게이트 6개, 기억의 상자, 안내판, 조명, 플레이어, 아트 텍스처)을 생성한다.
/// 메뉴: Tools > 기억 로비 > 로비 씬 한번에 생성
public static class LobbyBuilder
{
    private const string ArtDir = "Assets/Art/Lobby";
    private const string DataDir = "Assets/Data";
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/Lobby.unity";

    private const float Half = 18f;        // 36 x 36 방의 절반 크기
    private const float WallHeight = 6f;
    private const float WorldTextScale = 0.01f;

    /// 생성에 쓰는 머티리얼 묶음
    private class LobbyArt
    {
        public Material Floor;
        public Material Wall;
        public Material Ceiling;
        public Material Gate;
        public Material Pedestal;
        public Material Slot;
        public Material EndingDoor;
    }

    [MenuItem("Tools/기억 로비/로비 씬 한번에 생성")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("로비 씬 생성",
                "새 Lobby 씬을 만듭니다. 현재 열린 씬은 닫힙니다. 계속할까요?", "생성", "취소"))
        {
            return;
        }

        EnsureFolder(ScenesDir);
        EnsureFolder(ArtDir);
        EnsureFolder(DataDir);

        LobbyArt art = CreateArt();
        LobbyProgress progress = CreateProgressAsset();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.6f, 0.62f, 0.66f);

        CreateLights();

        Transform root = NewChild(null, "=== Lobby ===", Vector3.zero);
        GameObject endingDoor = BuildRoom(root, art);
        BuildGates(root, art, progress);
        BuildMemoryBox(root, art, progress, endingDoor);
        BuildBoards(root, progress);
        BuildPlayer(root);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[LobbyBuilder] 로비 씬 생성 완료: " + ScenePath);
    }

    // ───────────────────────── 아트 (텍스처 / 머티리얼) ─────────────────────────

    private static LobbyArt CreateArt()
    {
        // 바닥: 1m 격자가 있는 병실 타일 느낌
        Texture2D floorTex = MakeTexture("FloorTile", (x, y) =>
        {
            bool line = x % 64 < 3 || y % 64 < 3;
            return line ? new Color(0.62f, 0.66f, 0.70f) : new Color(0.93f, 0.95f, 0.96f);
        });

        // 벽: 연한 민트 세로 줄무늬
        Texture2D wallTex = MakeTexture("WallStripe", (x, y) =>
        {
            bool stripe = x % 32 < 2;
            return stripe ? new Color(0.80f, 0.84f, 0.86f) : new Color(0.90f, 0.94f, 0.92f);
        });

        return new LobbyArt
        {
            Floor = MakeMaterial("M_Floor", Color.white, floorTex, new Vector2(9f, 9f)),
            Wall = MakeMaterial("M_Wall", Color.white, wallTex, new Vector2(6f, 1f)),
            Ceiling = MakeMaterial("M_Ceiling", new Color(0.96f, 0.96f, 0.96f)),
            Gate = MakeMaterial("M_Gate", new Color(0.55f, 0.85f, 1f)),
            Pedestal = MakeMaterial("M_Pedestal", new Color(0.85f, 0.82f, 0.78f)),
            Slot = MakeMaterial("M_Slot", new Color(0.25f, 0.25f, 0.3f)),
            EndingDoor = MakeMaterial("M_EndingDoor", new Color(1f, 0.85f, 0.3f)),
        };
    }

    /// 절차적으로 PNG를 만들어 Assets/Art/Lobby에 저장하고 임포트한다
    private static Texture2D MakeTexture(string name, System.Func<int, int, Color> pixel)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, pixel(x, y));
            }
        }
        tex.Apply();

        string assetPath = $"{ArtDir}/{name}.png";
        string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    /// URP 프로젝트면 Lit, 빌트인이면 Standard를 사용한다
    private static Material MakeMaterial(string name, Color color, Texture2D tex = null, Vector2 tiling = default)
    {
        string path = $"{ArtDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path); // 재생성 시 덮어쓰기

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader) { color = color };
        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling == Vector2.zero ? Vector2.one : tiling;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static LobbyProgress CreateProgressAsset()
    {
        string path = $"{DataDir}/LobbyProgress.asset";
        var asset = AssetDatabase.LoadAssetAtPath<LobbyProgress>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<LobbyProgress>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    // ───────────────────────── 방 / 조명 / 플레이어 ─────────────────────────

    private static void CreateLights()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.8f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 천장 조명 4개 (방의 네 모서리 쪽)
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                var lamp = new GameObject($"CeilingLight_{x}_{z}").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 22f;
                lamp.intensity = 1.2f;
                lamp.color = new Color(1f, 0.97f, 0.9f);
                lamp.transform.position = new Vector3(x * 9f, 5.4f, z * 9f);
            }
        }
    }

    /// 바닥, 천장, 벽 4개, 뒤쪽 엔딩 문을 만든다. 엔딩 문을 반환한다.
    private static GameObject BuildRoom(Transform root, LobbyArt art)
    {
        Transform room = NewChild(root, "Room", Vector3.zero);
        float wallCenterY = WallHeight * 0.5f;
        float wallOuter = Half + 0.5f; // 벽 두께 1의 중심

        Cube(room, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(36f, 0.2f, 36f), art.Floor);
        Cube(room, "Ceiling", new Vector3(0f, WallHeight + 0.1f, 0f), new Vector3(36f, 0.2f, 36f), art.Ceiling);
        Cube(room, "Wall_Front", new Vector3(0f, wallCenterY, wallOuter), new Vector3(37f, WallHeight, 1f), art.Wall);
        Cube(room, "Wall_Back", new Vector3(0f, wallCenterY, -wallOuter), new Vector3(37f, WallHeight, 1f), art.Wall);
        Cube(room, "Wall_Left", new Vector3(-wallOuter, wallCenterY, 0f), new Vector3(1f, WallHeight, 37f), art.Wall);
        Cube(room, "Wall_Right", new Vector3(wallOuter, wallCenterY, 0f), new Vector3(1f, WallHeight, 37f), art.Wall);

        // 엔딩 문: 뒤쪽 벽 중앙. 기억의 조각 6개 투입 시 사라진다.
        return Cube(room, "EndingDoor", new Vector3(0f, 2f, -Half + 0.4f), new Vector3(4f, 4f, 0.3f), art.EndingDoor);
    }

    private static void BuildPlayer(Transform root)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.SetParent(root, false);
        player.transform.position = new Vector3(0f, 1f, 0f); // 로비 중앙 시작

        // CharacterController로 교체 (트리거 감지와 이동 모두 사용)
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;
        player.GetComponent<MeshRenderer>().enabled = false; // 1인칭이므로 캡슐 숨김

        // 카메라를 눈 높이에 붙인다
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            cam = camGo.GetComponent<Camera>();
        }
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        cam.transform.localRotation = Quaternion.identity;
    }

    // ───────────────────────── 던전 게이트 ─────────────────────────

    /// 왼쪽 벽 1~3던전, 오른쪽 벽 4~6던전. 각 게이트는 문 + 입장 트리거 + 이름표로 구성된다.
    private static void BuildGates(Transform root, LobbyArt art, LobbyProgress progress)
    {
        Transform parent = NewChild(root, "Gates", Vector3.zero);
        float[] zPositions = { -9f, 0f, 9f };

        for (int side = 0; side < 2; side++)
        {
            float wallX = side == 0 ? -Half + 0.4f : Half - 0.4f;
            float inward = side == 0 ? 1f : -1f;          // 방 안쪽 방향 (X)
            float labelYaw = side == 0 ? -90f : 90f;      // 방 안쪽에서 보이도록 회전

            for (int i = 0; i < 3; i++)
            {
                int dungeon = side * 3 + i + 1;
                Transform gate = NewChild(parent, $"Gate_{dungeon}", new Vector3(wallX, 0f, zPositions[i]));

                // 1) 문 (실제로 막는 벽)
                GameObject door = Cube(gate, "Door", new Vector3(0f, 2f, 0f), new Vector3(0.3f, 4f, 4f), art.Gate);

                // 2) 입장 트리거 + LobbyGate (트리거와 같은 오브젝트에 있어야 OnTriggerEnter가 호출된다)
                Transform zone = NewChild(gate, "TriggerZone", new Vector3(inward * 1.5f, 2f, 0f));
                var zoneCollider = zone.gameObject.AddComponent<BoxCollider>();
                zoneCollider.isTrigger = true;
                zoneCollider.size = new Vector3(3f, 4f, 5f);

                var lobbyGate = zone.gameObject.AddComponent<LobbyGate>();
                TMP_Text label = WorldText(gate, $"Label_{dungeon}", new Vector3(inward * 0.6f, 4.8f, 0f),
                    labelYaw, new Vector2(300f, 110f), $"{dungeon}던전", 40f);

                var so = new SerializedObject(lobbyGate);
                so.FindProperty("progress").objectReferenceValue = progress;
                so.FindProperty("doorRenderer").objectReferenceValue = door.GetComponent<Renderer>();
                so.FindProperty("nameLabel").objectReferenceValue = label;
                so.FindProperty("dungeonIndex").intValue = dungeon;
                so.FindProperty("dungeonSceneName").stringValue = $"Dungeon{dungeon:00}";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ───────────────────────── 기억의 상자 ─────────────────────────

    /// 정면 중앙 받침대 + 슬롯 6개 + 상호작용 트리거
    private static void BuildMemoryBox(Transform root, LobbyArt art, LobbyProgress progress, GameObject endingDoor)
    {
        Transform box = NewChild(root, "MemoryBox", new Vector3(0f, 0f, 14f));
        Cube(box, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(6f, 1f, 2f), art.Pedestal);

        var slotRenderers = new Renderer[LobbyProgress.DungeonCount];
        for (int i = 0; i < slotRenderers.Length; i++)
        {
            float x = -2.5f + i; // -2.5 ~ 2.5
            GameObject slot = Cube(box, $"Slot_{i + 1}", new Vector3(x, 1.15f, 0f), new Vector3(0.6f, 0.3f, 0.6f), art.Slot);
            slotRenderers[i] = slot.GetComponent<Renderer>();
        }

        Transform zone = NewChild(box, "InteractZone", new Vector3(0f, 1f, -2f));
        var zoneCollider = zone.gameObject.AddComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector3(4f, 2f, 3f);

        var memoryBox = zone.gameObject.AddComponent<MemoryBox>();
        var so = new SerializedObject(memoryBox);
        so.FindProperty("progress").objectReferenceValue = progress;
        so.FindProperty("endingDoor").objectReferenceValue = endingDoor;

        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slotRenderers.Length;
        for (int i = 0; i < slotRenderers.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotRenderers[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 안내판 ─────────────────────────

    /// 전체 현황판(오른쪽, 항상 표시) + 조건판(왼쪽, F키로 패널 열기)
    private static void BuildBoards(Transform root, LobbyProgress progress)
    {
        Transform boards = NewChild(root, "Boards", Vector3.zero);
        const float boardZ = 17.4f; // 앞쪽 벽 안쪽

        // 전체 현황판
        Transform status = NewChild(boards, "StatusBoard", new Vector3(7f, 0f, boardZ));
        TMP_Text statusText = WorldText(status, "StatusCanvas", new Vector3(0f, 3f, 0f), 0f,
            new Vector2(400f, 220f), "전체 현황", 36f);
        var statusBoard = status.gameObject.AddComponent<LobbyBoard>();
        SetBoard(statusBoard, progress, statusText, null, true);

        // 조건판 (범위 안에서 F키)
        Transform cond = NewChild(boards, "ConditionBoard", new Vector3(-7f, 0f, boardZ));
        var condCollider = cond.gameObject.AddComponent<BoxCollider>();
        condCollider.isTrigger = true;
        condCollider.center = new Vector3(0f, 1.5f, -2.5f);
        condCollider.size = new Vector3(5f, 3f, 5f);

        TMP_Text condText = WorldText(cond, "ConditionCanvas", new Vector3(0f, 3f, 0f), 0f,
            new Vector2(460f, 300f), "", 30f);
        var condBoard = cond.gameObject.AddComponent<LobbyBoard>();
        SetBoard(condBoard, progress, condText, condText.transform.parent.gameObject, false);
    }

    private static void SetBoard(LobbyBoard board, LobbyProgress progress, TMP_Text text, GameObject panel, bool alwaysShow)
    {
        var so = new SerializedObject(board);
        so.FindProperty("progress").objectReferenceValue = progress;
        so.FindProperty("bodyText").objectReferenceValue = text;
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("alwaysShowStatus").boolValue = alwaysShow;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 공통 헬퍼 ─────────────────────────

    private static Transform NewChild(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// 월드 스페이스 캔버스 + 배경 + TMP 텍스트를 만든다. 텍스트 컴포넌트를 반환한다.
    private static TMP_Text WorldText(Transform parent, string name, Vector3 localPos, float yaw,
        Vector2 size, string text, float fontSize)
    {
        var canvasGo = new GameObject(name, typeof(Canvas));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = localPos;
        canvasGo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        canvasGo.transform.localScale = Vector3.one * WorldTextScale;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvasGo.transform).sizeDelta = size;

        // 배경 패널
        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)bg.transform);
        bg.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.85f);

        // 글자
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)textGo.transform);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal; // 변경: Unity 6 TMP 방식 (줄바꿈 켜기)
        tmp.richText = true;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void AddSceneToBuildSettings(string scenePath)
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