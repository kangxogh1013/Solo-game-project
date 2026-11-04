using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// 로비 안내판.
/// - alwaysShowStatus = true  → 전체 현황판 (항상 진행도 표시)
/// - alwaysShowStatus = false → 조건판 (범위 안에서 F키로 패널 열기/닫기)
/// 조건판은 BoxCollider(Is Trigger)가 붙은 오브젝트에 달려 있어야 한다.
public class LobbyBoard : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("로비 진행 데이터 (LobbyProgress.asset)")]
    [SerializeField] private LobbyProgress progress;

    [Tooltip("표시할 TMP 텍스트")]
    [SerializeField] private TMP_Text bodyText;

    [Tooltip("F키로 열고 닫을 패널 (조건판용, 현황판이면 비워둠)")]
    [SerializeField] private GameObject panelRoot;

    [Header("모드")]
    [Tooltip("true: 항상 진행도 표시 / false: F키로 조건 패널 토글")]
    [SerializeField] private bool alwaysShowStatus;

    [Header("문구")]
    [TextArea(2, 4)]
    [SerializeField] private string conditionMessage =
        "<b>클리어 조건</b>\n1~6 던전을 순서대로 클리어하고\n기억의 조각 6개를 정면 상자에 넣으세요.";

    private bool playerInRange;
    private bool panelOpen;

    private void OnEnable()
    {
        if (progress != null) progress.OnChanged += Refresh;
        ApplyPanel();
        Refresh();
    }

    private void OnDisable()
    {
        if (progress != null) progress.OnChanged -= Refresh;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        panelOpen = false;
        ApplyPanel();
    }

    private void Update()
    {
        if (alwaysShowStatus || !playerInRange) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

        panelOpen = !panelOpen;
        ApplyPanel();
    }

    private void ApplyPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(!alwaysShowStatus && panelOpen);
    }

    /// 모드에 맞게 텍스트 갱신
    private void Refresh()
    {
        if (bodyText == null) return;

        int cleared = progress != null ? progress.ClearedCount : 0;
        int inserted = progress != null ? progress.FragmentsInserted : 0;
        int total = LobbyProgress.DungeonCount;

        if (alwaysShowStatus)
        {
            bodyText.text = $"<b>전체 현황</b>\n던전 클리어  {cleared} / {total}\n조각 투입  {inserted} / {total}";
            return;
        }

        int next = NextDungeon();
        string target = next > 0
            ? $"{next}던전 클리어"
            : "모든 던전 클리어 → 조각을 상자에 넣으세요";

        bodyText.text = $"{conditionMessage}\n\n<b>진행량</b>  {cleared} / {total}\n<b>남은 목표</b>  {target}";
    }

    /// 아직 클리어하지 않은 가장 앞 던전 번호 (모두 클리어했으면 -1)
    private int NextDungeon()
    {
        if (progress == null) return 1;

        for (int i = 1; i <= LobbyProgress.DungeonCount; i++)
        {
            if (!progress.IsCleared(i)) return i;
        }
        return -1;
    }
}
