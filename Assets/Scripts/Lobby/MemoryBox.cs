using UnityEngine;
using UnityEngine.InputSystem;

/// 정면 기억의 조각 상자.
/// 범위 안에서 F키로 보유 중인 조각을 투입하고, 6개가 모이면 엔딩 문을 연다.
/// 이 컴포넌트는 BoxCollider(Is Trigger)가 붙은 "InteractZone" 오브젝트에 달려 있어야 한다.
public class MemoryBox : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("로비 진행 데이터 (LobbyProgress.asset)")]
    [SerializeField] private LobbyProgress progress;

    [Tooltip("상단 슬롯 렌더러 6개 (왼쪽 → 오른쪽 순서)")]
    [SerializeField] private Renderer[] slots;

    [Tooltip("6개 투입 시 비활성화될 엔딩 문")]
    [SerializeField] private GameObject endingDoor;

    [Header("슬롯 색상")]
    [SerializeField] private Color emptySlotColor = new Color(0.25f, 0.25f, 0.3f);
    [SerializeField] private Color filledSlotColor = new Color(1f, 0.85f, 0.3f);

    [Header("기즈모")]
    [SerializeField, Min(0.5f)] private float gizmoRadius = 2f;

    private Material[] slotMats;
    private bool playerInRange;

    private void Awake()
    {
        // 슬롯 머티리얼 인스턴스를 한 번만 만든다
        slotMats = new Material[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) slotMats[i] = slots[i].material;
        }
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

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

        InsertFragment();
    }

    /// 조각 1개 투입 시도
    private void InsertFragment()
    {
        if (progress == null) return;

        if (!progress.TryInsertFragment())
        {
            Debug.Log("[MemoryBox] 투입할 기억의 조각이 없습니다. 던전을 먼저 클리어하세요.");
            return;
        }

        Debug.Log($"[MemoryBox] 기억의 조각 투입 {progress.FragmentsInserted}/{LobbyProgress.DungeonCount}");

        if (progress.AllInserted) Debug.Log("[MemoryBox] 6개 완료 → 엔딩 문 개방");
    }

    /// 투입 수에 맞게 슬롯 색을 바꾸고, 6개가 모였으면 엔딩 문을 연다
    private void Refresh()
    {
        if (slotMats == null) return;

        int inserted = progress != null ? progress.FragmentsInserted : 0;
        for (int i = 0; i < slotMats.Length; i++)
        {
            if (slotMats[i] != null) slotMats[i].color = i < inserted ? filledSlotColor : emptySlotColor;
        }

        if (endingDoor != null && progress != null && progress.AllInserted)
        {
            endingDoor.SetActive(false);
        }
    }

    /// 씬 뷰에서 상호작용 범위를 표시
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, gizmoRadius);
    }
}
