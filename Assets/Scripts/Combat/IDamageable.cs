/// 피해를 받을 수 있는 대상의 공통 인터페이스 (몬스터, 테스트 더미 등이 구현)
public interface IDamageable
{
    void TakeDamage(int amount);
}
