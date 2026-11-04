using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// 던전 입구 게이트.
/// - 던전 1은 항상 열려 있고, N던전은 (N-1)던전 클리어 시 열린다.
/// - 플레이어가 입장 범위 안에서 F키를 누르면 던전 씬을 로드한다.
/// 이 컴포넌트는 BoxCollider(Is Trigger)가 붙은 "TriggerZone" 오브젝트에 달려 있어야 한다.
[RequireComponent(typeof(BoxCollider))]
public class LobbyGate : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("로비 진행 데이터 (LobbyProgress.asset)")]
    [SerializeField] private LobbyProgress progress;

    [Tooltip("잠금 상태에 따라 색이 바뀌는 문 렌더러")]
    [SerializeField] private Renderer doorRenderer;

    [Tooltip("문 위 이름표 (TMP)")]
    [SerializeField] private TMP_Text nameLabel;

    [Header("던전 설정")]
    [Tooltip("던전 번호 (1~6)")]
    [SerializeField, Range(1, LobbyProgress.DungeonCount)] private int dungeonIndex = 1;

    [Tooltip("입장 시 로드할 씬 이름 (File > Build Settings 등록 필요)")]
    [SerializeField] private string dungeonSceneName = "Dungeon01";

    [Header("색상")]
    [SerializeField] private Color unlockedColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.4f);

    private Material doorMat;
    private bool playerInRange;

    /// 현재 입장 가능 여부
    public bool IsUnlocked => dungeonIndex == 1 || (progress != null && progress.IsCleared(dungeonIndex - 1));

    private void Awake()
    {
        // 머티리얼 인스턴스는 한 번만 만든다 (Update에서 new/GetComponent 금지)
        if (doorRenderer != null) doorMat = doorRenderer.material;
    }

    private void OnEnable()
    {
        if (progress != null) progress.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (progress != null) progress.OnChanged -= Refresh;
    }

    /// 잠금 상태에 맞게 문 색과 이름표를 갱신
    private void Refresh()
    {
        bool open = IsUnlocked;

        if (doorMat != null) doorMat.color = open ? unlockedColor : lockedColor;
        if (nameLabel != null) nameLabel.text = open ? $"{dungeonIndex}던전" : $"{dungeonIndex}던전 (잠김)";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }

    private void Update()
    {
        if (!playerInRange) return;

        // New Input System 기준
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

        TryEnter();
    }

    private void TryEnter()
    {
        if (!IsUnlocked)
        {
            Debug.Log($"[LobbyGate] {dungeonIndex}던전 잠김 → {dungeonIndex - 1}던전을 먼저 클리어하세요.");
            return;
        }

        Debug.Log($"[LobbyGate] {dungeonIndex}던전 입장 → 씬 '{dungeonSceneName}' 로드");
        SceneManager.LoadScene(dungeonSceneName);
    }

    /// 씬 뷰에서 입장 범위를 표시 (파랑: 열림, 회색: 잠김)
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = IsUnlocked ? Color.cyan : Color.gray;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
