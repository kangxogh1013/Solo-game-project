using UnityEngine;
using UnityEngine.InputSystem;

/// 마우스 왼쪽 버튼으로 공격한다. 앞쪽 구체 범위 안의 IDamageable에게 피해를 준다.
/// 공격력은 PlayerInventory.AttackPower (맨손 + 장착 무기)를 쓴다.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerCombat : MonoBehaviour
{
    [Header("공격 범위")]
    [Tooltip("플레이어 앞쪽으로 얼마나 멀리 공격할지 (m)")]
    [SerializeField, Min(0.1f)] private float attackRange = 2f;
    [Tooltip("공격 판정 구체의 반지름 (m)")]
    [SerializeField, Min(0.1f)] private float attackRadius = 0.7f;

    [Header("속도")]
    [Tooltip("공격 간격 (초)")]
    [SerializeField, Min(0.05f)] private float cooldown = 0.5f;

    [Header("대상")]
    [Tooltip("맞을 수 있는 레이어")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool logHits = true;

    // 매 공격마다 배열을 만들지 않도록 재사용 버퍼를 쓴다 (GC 방지)
    private static readonly Collider[] hitBuffer = new Collider[16];

    private PlayerInventory inventory;
    private float nextAttackTime;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // 커서가 잠긴 상태(플레이 중)에서만 공격한다. 인벤토리를 열면 커서가 풀려 공격되지 않는다.
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + cooldown;
        Attack();
    }

    private void Attack()
    {
        Vector3 center = transform.position + transform.forward * (attackRange * 0.5f);
        int count = Physics.OverlapSphereNonAlloc(center, attackRadius, hitBuffer, hitMask, QueryTriggerInteraction.Ignore);
        int damage = inventory.AttackPower;

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col.transform.IsChildOf(transform)) continue; // 자기 자신 제외

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;

            target.TakeDamage(damage);
            if (logHits) Debug.Log($"[PlayerCombat] {col.name}에게 {damage} 피해");
        }
    }

    /// 씬 뷰에서 공격 범위를 표시 (빨강)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * (attackRange * 0.5f), attackRadius);
    }
}
