using UnityEngine;

/// 공격 테스트용 더미. Cube 등에 붙이면 맞을 때마다 체력이 줄고, 0이 되면 사라진다.
[RequireComponent(typeof(Collider))]
public class TestDummy : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int maxHp = 100;

    private int hp;

    private void Awake()
    {
        hp = maxHp;
    }

    public void TakeDamage(int amount)
    {
        hp = Mathf.Max(0, hp - amount);
        Debug.Log($"[TestDummy] {name} 피해 {amount}, 체력 {hp}/{maxHp}");

        if (hp == 0) gameObject.SetActive(false);
    }
}
