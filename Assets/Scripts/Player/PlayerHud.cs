using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 플레이어 HUD: 왼쪽 HP 바(마크의 하트 자리), 오른쪽 스태미나 바(마크의 허기 자리)
/// 런타임에 Canvas를 자동 생성하므로 Inspector 설정이 필요 없다.
/// 바는 Image.fillAmount로 줄이고 늘린다 (앵커를 바꾸면 매 프레임 레이아웃이 다시 계산되어 끊긴다).
[RequireComponent(typeof(PlayerStats))]
public class PlayerHud : MonoBehaviour
{
    private const float BarWidth = 300f;
    private const float BarHeight = 22f;
    private const float BarX = 260f;  // 화면 중앙에서 좌우 거리
    private const float BarY = 110f;  // 화면 아래에서 높이 (핫바 위쪽)

    private PlayerStats stats;
    private GameObject canvasGo;
    private Image hpFill;
    private Image staminaFill;
    private TMP_Text hpText;
    private TMP_Text staminaText;

    // 값이 바뀔 때만 갱신 (매 프레임 문자열·UI 갱신 방지)
    private int shownHp = -1;
    private int shownStamina = -1;
    private float shownHpRatio = -1f;
    private float shownStaminaRatio = -1f;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        BuildUI();
    }

    private void OnEnable()
    {
        if (stats != null) stats.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (stats != null) stats.OnChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void Refresh()
    {
        if (hpFill == null) return;

        float hpRatio = stats.MaxHp > 0f ? stats.Hp / stats.MaxHp : 0f;
        float staminaRatio = stats.MaxStamina > 0f ? stats.Stamina / stats.MaxStamina : 0f;

        SetFill(hpFill, hpRatio, ref shownHpRatio);
        SetFill(staminaFill, staminaRatio, ref shownStaminaRatio);

        // 글자는 숫자가 바뀔 때만 다시 만든다
        int hp = Mathf.CeilToInt(stats.Hp);
        if (hp != shownHp)
        {
            shownHp = hp;
            hpText.text = $"HP {hp} / {Mathf.CeilToInt(stats.MaxHp)}";
        }

        int st = Mathf.CeilToInt(stats.Stamina);
        if (st != shownStamina)
        {
            shownStamina = st;
            staminaText.text = $"스태미나 {st} / {Mathf.CeilToInt(stats.MaxStamina)}";
        }
    }

    /// 채움 양이 실제로 바뀐 경우에만 fillAmount를 바꾼다
    private static void SetFill(Image fill, float ratio, ref float cached)
    {
        ratio = Mathf.Clamp01(ratio);
        if (Mathf.Abs(ratio - cached) < 0.001f) return;

        cached = ratio;
        fill.fillAmount = ratio;
    }

    // ───────────────────────── UI 생성 ─────────────────────────

    private void BuildUI()
    {
        canvasGo = new GameObject("PlayerHudCanvas", typeof(Canvas), typeof(CanvasScaler));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Transform root = canvasGo.transform;
        hpFill = CreateBar(root, -BarX, new Color(0.85f, 0.15f, 0.15f), out hpText);
        staminaFill = CreateBar(root, BarX, new Color(0.3f, 0.8f, 0.35f), out staminaText);
    }

    /// 배경 + 채움 + 글자 3겹으로 된 바를 만든다. 채움 Image를 반환한다.
    private static Image CreateBar(Transform root, float x, Color fillColor, out TMP_Text label)
    {
        // 바 배경
        RectTransform bg = NewRect("Bar", root, new Vector2(0.5f, 0f), new Vector2(x, BarY), BarWidth, BarHeight);
        bg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // 채움: 왼쪽에서부터 fillAmount 비율만큼 보인다
        RectTransform fillRt = NewRect("Fill", bg, new Vector2(0.5f, 0.5f), Vector2.zero, BarWidth, BarHeight);
        Stretch(fillRt);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        // 글자: 가장 위에 그린다
        RectTransform labelRt = NewRect("Label", bg, new Vector2(0.5f, 0.5f), Vector2.zero, BarWidth, BarHeight);
        Stretch(labelRt);
        var tmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        label = tmp;

        return fill;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
