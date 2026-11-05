using System;
using UnityEngine;

/// 플레이어 체력(HP)과 스태미나 데이터.
/// 값이 바뀌면 OnChanged를 호출해서 HUD가 갱신되게 한다.
/// 몬스터·포션 같은 다른 시스템은 TakeDamage / Heal 로 접근한다.
public class PlayerStats : MonoBehaviour
{
    [Header("체력")]
    [Tooltip("최대 체력 (하트 1개 = 2)")]
    [SerializeField, Min(1f)] private float maxHp = 20f;

    [Header("스태미나 (마크의 허기 자리)")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [Tooltip("달리기 중 초당 소모량")]
    [SerializeField, Min(0f)] private float sprintCostPerSec = 20f;
    [Tooltip("달리기를 멈춘 뒤 회복이 시작되기까지 대기 시간 (초)")]
    [SerializeField, Min(0f)] private float regenDelay = 1f;
    [Tooltip("초당 회복량")]
    [SerializeField, Min(0f)] private float regenPerSec = 15f;

    /// HP나 스태미나가 바뀔 때 호출 (구독자: PlayerHud)
    public event Action OnChanged;

    /// HP가 0이 되었을 때 한 번 호출
    public event Action OnDied;

    private float hp;
    private float stamina;
    private float regenTimer;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public float Stamina => stamina;
    public float MaxStamina => maxStamina;
    public bool IsDead => hp <= 0f;

    private void Awake()
    {
        hp = maxHp;
        stamina = maxStamina;
    }

    /// 매 프레임 호출한다.
    /// sprinting이 true이고 스태미나가 남아 있으면 소모하고 true를 반환한다.
    /// sprinting이 false이면 대기 시간 후 스태미나를 회복한다.
    public bool TickStamina(bool sprinting, float dt)
    {
        if (sprinting && stamina > 0f)
        {
            stamina = Mathf.Max(0f, stamina - sprintCostPerSec * dt);
            regenTimer = 0f;
            Notify();
            return true;
        }

        regenTimer += dt;
        if (regenTimer >= regenDelay && stamina < maxStamina)
        {
            stamina = Mathf.Min(maxStamina, stamina + regenPerSec * dt);
            Notify();
        }
        return false;
    }

    /// 피해를 받는다. 체력이 0 이하가 되면 OnDied를 호출한다.
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        hp = Mathf.Max(0f, hp - amount);
        Notify();

        if (IsDead) OnDied?.Invoke();
    }

    /// 체력을 회복한다 (최대 체력을 넘지 않음).
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        hp = Mathf.Min(maxHp, hp + amount);
        Notify();
    }

    private void Notify()
    {
        OnChanged?.Invoke();
    }
}
