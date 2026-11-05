using UnityEngine;
using UnityEngine.InputSystem;

/// 플레이어 이동·점프·걷기·달리기·앉기·1/3인칭 시점 (New Input System 기준)
/// 키: WASD 이동 · 마우스 시점 · Space 점프 · Shift 달리기(스태미나 소모) · Ctrl/C 앉기
///     V 1인칭/3인칭 전환 · Esc 커서 해제 · 화면 클릭 커서 잠금
/// 구조: Player(이 스크립트) → HeadPivot(눈 높이) → Main Camera
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    public enum ViewMode { FirstPerson, ThirdPerson }

    /// 인벤토리 등 UI가 열려 있으면 true로 설정한다. 열린 동안 시점 회전과 커서 잠금을 막는다.
    public static bool IsUiOpen { get; set; }

    private static bool warnedNoInput; // 입력 장치 경고는 한 번만 출력

    [Header("이동 속도 (m/s)")]
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float runSpeed = 6f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;

    [Header("점프 / 중력")]
    [Tooltip("기본 점프 높이 (m)")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("앉기")]
    [SerializeField, Min(0.5f)] private float standHeight = 2f;
    [SerializeField, Min(0.5f)] private float crouchHeight = 1f;
    [SerializeField, Min(1f)] private float crouchSmooth = 10f;

    [Header("시점")]
    [Tooltip("눈 위치 (자동 생성 가능)")]
    [SerializeField] private Transform headPivot;
    [Tooltip("1인칭/3인칭에서 움직이는 카메라")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(-89f, 0f)] private float minPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 80f;
    [Tooltip("3인칭 카메라 거리 (m)")]
    [SerializeField, Min(1f)] private float thirdPersonDistance = 4f;

    [Header("시각 요소")]
    [Tooltip("3인칭에서만 보이는 몸 렌더러 (Player의 Mesh Renderer)")]
    [SerializeField] private Renderer[] bodyRenderers;

    // 캐시 (Update에서 GetComponent 금지)
    private CharacterController controller;
    private PlayerStats stats;

    // 상태
    private Vector3 velocity;
    private float yaw;
    private float pitch;
    private bool isRunning;
    private bool isCrouching;
    private ViewMode viewMode = ViewMode.FirstPerson;

    // 상점 강화 배율 (이동속도/점프 강화 연동용)
    private float moveMultiplier = 1f;
    private float jumpMultiplier = 1f;

    public ViewMode CurrentView => viewMode;
    public bool IsRunning => isRunning;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();

        controller.height = standHeight;
        controller.center = Vector3.zero;

        EnsureRig();

        yaw = transform.eulerAngles.y;
        ApplyView();
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
            if (!warnedNoInput)
            {
                warnedNoInput = true;
                Debug.LogError("[PlayerController] 새 Input System 장치를 찾지 못했습니다. " +
                               "Edit > Project Settings > Player > Other Settings > Active Input Handling 을 " +
                               "'Input System Package (New)' 또는 'Both'로 설정하고 재시작하세요.");
            }
            return;
        }

        float dt = Time.deltaTime;

        HandleCursor(keyboard, mouse);

        if (keyboard.vKey.wasPressedThisFrame) ToggleView();

        HandleLook(mouse);
        HandleCrouch(keyboard, dt);
        HandleMove(keyboard, dt);
    }

    // ───────────────────────── 입력 ─────────────────────────

    /// Esc로 커서 해제, 화면 클릭으로 다시 잠금 (UI가 열려 있으면 잠그지 않음)
    private void HandleCursor(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);

        if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !IsUiOpen)
        {
            SetCursorLocked(true);
        }
    }

    /// 마우스 이동으로 좌우는 몸, 상하는 HeadPivot을 회전
    private void HandleLook(Mouse mouse)
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// Ctrl 또는 C를 누르고 있으면 앉기. 캡슐과 눈 높이를 부드럽게 낮춘다.
    private void HandleCrouch(Keyboard keyboard, float dt)
    {
        isCrouching = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;

        float targetHeight = isCrouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchSmooth * dt);

        // 발바닥 위치를 고정한다 (캡슐 중심 = 높이 절반 - 서 있을 때 높이 절반)
        float halfStand = standHeight * 0.5f;
        controller.center = new Vector3(0f, controller.height * 0.5f - halfStand, 0f);

        // 눈 높이: 발바닥 기준 캡슐 높이의 0.8배
        Vector3 pivotPos = headPivot.localPosition;
        pivotPos.y = -halfStand + controller.height * 0.8f;
        headPivot.localPosition = pivotPos;
    }

    /// 이동, 달리기(스태미나), 점프, 중력
    private void HandleMove(Keyboard keyboard, float dt)
    {
        float x = 0f;
        float z = 0f;
        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed) z -= 1f;
        if (keyboard.wKey.isPressed) z += 1f;

        Vector3 input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude > 1f) input.Normalize();

        // 달리기: Shift + 앞으로 이동 + 앉지 않음 + 스태미나 남음
        bool wantsSprint = keyboard.leftShiftKey.isPressed && !isCrouching && z > 0f;
        bool sprintAllowed = stats.TickStamina(wantsSprint, dt);
        isRunning = wantsSprint && sprintAllowed;

        float speed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        speed *= moveMultiplier;

        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;

        if (grounded && !isCrouching && keyboard.spaceKey.wasPressedThisFrame)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * jumpMultiplier * -2f * gravity);
        }

        velocity.y += gravity * dt;

        Vector3 horizontal = transform.TransformDirection(input) * speed;
        Vector3 motion = horizontal + Vector3.up * velocity.y;
        controller.Move(motion * dt);
    }

    // ───────────────────────── 시점 ─────────────────────────

    public void ToggleView()
    {
        viewMode = viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
        ApplyView();
    }

    /// 1인칭: 카메라를 눈 위치에 둔다. 3인칭: 뒤로 물러나고 몸을 보여 준다.
    private void ApplyView()
    {
        bool third = viewMode == ViewMode.ThirdPerson;

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

    /// HeadPivot과 카메라 연결이 비어 있으면 자동으로 만든다
    private void EnsureRig()
    {
        if (headPivot == null)
        {
            var pivotGo = new GameObject("HeadPivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            headPivot = pivotGo.transform;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null && cameraTransform.parent != headPivot)
        {
            cameraTransform.SetParent(headPivot, false);
        }
    }

    // ───────────────────────── 외부 연동 (상점 강화) ─────────────────────────

    /// 이동속도 강화 배율 (예: 상점 "이속 증가" 구매 시 2)
    public void SetMoveMultiplier(float multiplier)
    {
        moveMultiplier = Mathf.Max(0f, multiplier);
    }

    /// 점프 강화 배율 (예: 상점 "점프 증가" 구매 시 2)
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

    /// 씬 뷰에서 캡슐 범위를 표시 (선택했을 때만)
    private void OnDrawGizmosSelected()
    {
        if (!TryGetComponent(out CharacterController cc)) return;

        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(cc.center, new Vector3(cc.radius * 2f, cc.height, cc.radius * 2f));
    }
}
