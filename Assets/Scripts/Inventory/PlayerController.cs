using UnityEngine;
using UnityEngine.InputSystem;

/// 플레이어 이동·점프·앉기·시점 전환 컨트롤러 (New Input System 기준)
/// 필요 컴포넌트: CharacterController (같은 오브젝트)
/// 구조: Player(이 스크립트) → HeadPivot(눈 높이) → Main Camera
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum ViewMode { FirstPerson, ThirdPerson }

    [Header("이동 속도")]
    [Tooltip("걷기 속도 (m/s)")]
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [Tooltip("달리기 속도 (앞으로 달릴 때)")]
    [SerializeField, Min(0f)] private float runSpeed = 6f;
    [Tooltip("앉은 상태 이동 속도")]
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;

    [Header("점프 / 중력")]
    [Tooltip("기본 점프 높이 (m)")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("앉기")]
    [Tooltip("서 있을 때 캡슐 높이")]
    [SerializeField, Min(0.5f)] private float standHeight = 2f;
    [Tooltip("앉았을 때 캡슐 높이")]
    [SerializeField, Min(0.5f)] private float crouchHeight = 1f;
    [Tooltip("앉기/일어서기 전환 속도")]
    [SerializeField, Min(1f)] private float crouchSmooth = 10f;

    [Header("시점")]
    [Tooltip("눈 위치를 잡는 자식 오브젝트 (HeadPivot)")]
    [SerializeField] private Transform headPivot;
    [Tooltip("1인칭/3인칭에서 움직이는 카메라 (Main Camera)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.1f;
    [Tooltip("위를 볼 때 최대 각도 (음수)")]
    [SerializeField, Range(-89f, 0f)] private float minPitch = -80f;
    [Tooltip("아래를 볼 때 최대 각도")]
    [SerializeField, Range(0f, 89f)] private float maxPitch = 80f;
    [Tooltip("3인칭 카메라 거리 (m)")]
    [SerializeField, Min(1f)] private float thirdPersonDistance = 4f;

    [Header("시각 요소")]
    [Tooltip("3인칭에서만 보일 몸 렌더러 (Player의 Mesh Renderer)")]
    [SerializeField] private Renderer[] bodyRenderers;

    // ── 캐시 (Update에서 GetComponent 호출 금지) ──
    private CharacterController controller;

    // ── 상태 ──
    private Vector3 velocity;
    private float yaw;
    private float pitch;
    private bool isRunning;
    private bool isCrouching;
    private ViewMode viewMode = ViewMode.FirstPerson;

    private static bool warnedNoInput; // 입력 장치 경고는 한 번만 출력

    // 상점 강화 배율 (이동속도 강화, 점프 강화 연동용)
    private float moveMultiplier = 1f;
    private float jumpMultiplier = 1f;

    // ── 외부에서 읽는 값 ──
    public ViewMode CurrentView => viewMode;
    public bool IsRunning => isRunning;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standHeight;
        controller.center = Vector3.zero;

        EnsureRig();

        yaw = transform.eulerAngles.y;
        ApplyView();
    }

    /// HeadPivot과 카메라 연결이 비어 있으면 자동으로 만든다 (수동 설정 누락 대비)
    private void EnsureRig()
    {
        if (headPivot == null)
        {
            var pivotGo = new GameObject("HeadPivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 0.6f, 0f); // 눈 높이
            headPivot = pivotGo.transform;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 카메라는 HeadPivot 아래에 있어야 상하 시점과 3인칭 회전이 함께 동작한다
        if (cameraTransform != null && cameraTransform.parent != headPivot)
        {
            cameraTransform.SetParent(headPivot, false);
        }
    }

    private void Start()
    {
        SetCursorLocked(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null)
        {
            // 입력 장치를 못 찾으면 이동·시점·V 키가 전부 멈춘다 → 원인을 콘솔에 알려준다
            if (!warnedNoInput)
            {
                warnedNoInput = true;
                Debug.LogError("[PlayerController] 새 Input System 장치를 찾지 못했습니다. Edit > Project Settings > Player > Other Settings > Active Input Handling 을 'Input System Package (New)' 또는 'Both'로 설정하고 재시작하세요.");
            }
            return;
        }

        float dt = Time.deltaTime;

        HandleCursor(keyboard, mouse);
        HandleViewToggle(keyboard);
        HandleLook(mouse);
        HandleCrouch(keyboard, dt);
        HandleMove(keyboard, dt);
    }

    // ───────────────────────── 입력 처리 ─────────────────────────

    /// Esc로 커서 해제, 화면 클릭으로 다시 잠금
    private void HandleCursor(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        // 커서가 풀려 있을 때만 클릭으로 다시 잠근다 (잠긴 상태의 클릭은 공격에 사용)
        // 인벤토리가 열려 있으면 클릭으로 커서를 잠그지 않는다 (슬롯 클릭과 충돌 방지)
        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !InventoryUI.IsOpen) SetCursorLocked(true);
    }

    private void HandleViewToggle(Keyboard keyboard)
    {
        if (keyboard.vKey.wasPressedThisFrame) ToggleView();
    }

    /// 마우스 입력으로 좌우는 몸, 상하는 HeadPivot을 회전
    private void HandleLook(Mouse mouse)
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// Ctrl 또는 C를 누르면 앉기. 캡슐 높이를 부드럽게 줄이고 눈 높이도 함께 낮춘다.
    private void HandleCrouch(Keyboard keyboard, float dt)
    {
        isCrouching = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;

        float targetHeight = isCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchSmooth * dt);

        // 발바닥 위치를 고정: 캡슐 중심을 높이 절반에서 standHeight 절반을 뺀 값으로 둔다
        float halfStand = standHeight * 0.5f;
        controller.center = new Vector3(0f, controller.height * 0.5f - halfStand, 0f);

        // 눈 높이: 발바닥 기준으로 캡슐 높이의 0.8배 위치
        float eyeLocalY = -halfStand + controller.height * 0.8f;
        Vector3 pivotPos = headPivot.localPosition;
        pivotPos.y = eyeLocalY;
        headPivot.localPosition = pivotPos;
    }

    /// WASD 이동, Space 점프, Shift 달리기, 중력 적용
    private void HandleMove(Keyboard keyboard, float dt)
    {
        float x = 0f;
        float z = 0f;
        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed) z -= 1f;
        if (keyboard.wKey.isPressed) z += 1f;

        Vector3 input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude > 1f) input.Normalize(); // 대각선 이동 속도 보정

        // 달리기: Shift + 앞으로 이동 중일 때만, 앉은 상태에서는 불가
        isRunning = keyboard.leftShiftKey.isPressed && !isCrouching && z > 0f;

        float speed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        speed *= moveMultiplier;

        // 바닥에 붙어 있으면 낙하 속도를 리셋
        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        // 점프: 바닥에 있고, 앉은 상태가 아닐 때만
        if (grounded && !isCrouching && keyboard.spaceKey.wasPressedThisFrame)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * jumpMultiplier * -2f * gravity);
        }

        velocity.y += gravity * dt;

        Vector3 horizontal = transform.TransformDirection(input) * speed;
        Vector3 motion = horizontal + Vector3.up * velocity.y;
        controller.Move(motion * dt);
    }

    // ───────────────────────── 시점 전환 ─────────────────────────

    /// 1인칭 ↔ 3인칭 전환
    public void ToggleView()
    {
        viewMode = viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
        ApplyView();
    }

    /// 카메라 위치와 몸 표시를 현재 시점에 맞게 갱신
    private void ApplyView()
    {
        bool third = viewMode == ViewMode.ThirdPerson;

        // 1인칭: 카메라를 HeadPivot 중앙에 둔다. 3인칭: 뒤로 물러난다.
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = third ? new Vector3(0f, 0.3f, -thirdPersonDistance) : Vector3.zero;
            cameraTransform.localRotation = Quaternion.identity;
        }

        if (bodyRenderers == null) return;
        foreach (Renderer r in bodyRenderers)
        {
            if (r != null) r.enabled = third;
        }
    }

    // ───────────────────────── 외부 연동 (상점 강화) ─────────────────────────

    /// 이동속도 강화 (예: 1던전 상점 "이동속도 강화" 구매 시 2.0)
    public void SetMoveMultiplier(float multiplier)
    {
        moveMultiplier = Mathf.Max(0f, multiplier);
    }

    /// 점프 강화 (예: 1던전 상점 "점프 강화" 구매 시 1.5)
    public void SetJumpMultiplier(float multiplier)
    {
        jumpMultiplier = Mathf.Max(0f, multiplier);
    }

    // ───────────────────────── 공통 ─────────────────────────

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    [ContextMenu("1인칭 ↔ 3인칭 전환")]
    private void ContextToggleView()
    {
        ToggleView();
    }

    /// 씬 뷰에서 캡슐 범위를 표시
    private void OnDrawGizmosSelected()
    {
        if (!TryGetComponent(out CharacterController cc)) return;

        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(cc.center, new Vector3(cc.radius * 2f, cc.height, cc.radius * 2f));
    }
}
