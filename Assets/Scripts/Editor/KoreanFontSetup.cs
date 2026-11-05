#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// 원본 TTF(NotoSansKR-Medium)에서 TMP 폰트 에셋을 새로 만들고 기본 폰트로 지정한다.
/// 기존 SDF 에셋이 깨졌을 때 이 툴을 한 번 실행하면 된다.
/// 메뉴: Tools > 게임 개발 > 한글 폰트 다시 만들기
public static class KoreanFontSetup
{
    private const string SourceFileName = "NotoSansKR-Medium.ttf"; // 원본 폰트 파일 이름
    private const string OutputPath = "Assets/Fonts/Noto_Sans_KR/NotoSansKR-Medium_TMP.asset";

    private const int SamplingPointSize = 90;  // 글자 원본 크기 (클수록 선명)
    private const int AtlasPadding = 9;
    private const int AtlasSize = 2048;        // 글자 그림판 크기

    [MenuItem("Tools/게임 개발/한글 폰트 다시 만들기")]
    public static void Apply()
    {
        // 1) 원본 TTF 찾기
        Font sourceFont = FindSourceFont();
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("오류",
                $"{SourceFileName} 파일을 프로젝트에서 찾지 못했습니다.\nAssets/Fonts/Noto_Sans_KR/static 폴더를 확인하세요.", "확인");
            return;
        }

        // 2) 깨진 기존 에셋 지우기 (같은 경로에 새로 만든다)
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
        {
            AssetDatabase.DeleteAsset(OutputPath);
        }

        // 3) Dynamic 모드로 TMP 폰트 에셋 생성 (실행 중 필요한 한글 글자를 자동 추가)
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            SamplingPointSize,
            AtlasPadding,
            GlyphRenderMode.SDFAA,
            AtlasSize,
            AtlasSize,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            Debug.LogError("[KoreanFontSetup] TMP 폰트 에셋 생성에 실패했습니다.");
            return;
        }

        // 4) 에셋 파일로 저장 (재질·글자 그림판 텍스처를 함께 넣어야 깨지지 않는다)
        AssetDatabase.CreateAsset(fontAsset, OutputPath);
        if (fontAsset.material != null)
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        foreach (Texture2D tex in fontAsset.atlasTextures)
        {
            if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }
        EditorUtility.SetDirty(fontAsset);

        // 5) TMP 기본 폰트로 지정
        TMP_Settings settings = FindTmpSettings();
        if (settings == null)
        {
            Debug.LogError("[KoreanFontSetup] TMP Settings를 찾지 못했습니다. Window > TextMeshPro > Import TMP Essential Resources 를 먼저 실행하세요.");
            return;
        }

        var so = new SerializedObject(settings);
        SerializedProperty defaultFont = so.FindProperty("m_defaultFontAsset");
        if (defaultFont == null)
        {
            Debug.LogError("[KoreanFontSetup] TMP Settings에서 기본 폰트 항목을 찾지 못했습니다. 에셋은 만들어졌으니 Default Font Asset에 직접 지정하세요: " + OutputPath);
            return;
        }

        defaultFont.objectReferenceValue = fontAsset;
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[KoreanFontSetup] 새 한글 폰트 에셋을 만들고 기본 폰트로 지정했습니다: {OutputPath}. Play를 다시 실행해서 확인하세요.");
    }

    /// 프로젝트에서 NotoSansKR-Medium.ttf 원본 Font 찾기
    private static Font FindSourceFont()
    {
        string[] guids = AssetDatabase.FindAssets("NotoSansKR-Medium t:Font");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(SourceFileName)) continue;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null) return font;
        }
        return null;
    }

    /// TMP 기본 설정 에셋 찾기
    private static TMP_Settings FindTmpSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_Settings");
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
