using System;
using UnityEngine;

/// 로비 진행 상태 데이터 (ScriptableObject).
/// 데이터와 로직을 분리하고, 상태가 바뀌면 OnChanged 이벤트로 UI/게이트/상자가 스스로 갱신된다.
[CreateAssetMenu(fileName = "LobbyProgress", menuName = "기억 로비/Lobby Progress")]
public class LobbyProgress : ScriptableObject
{
    public const int DungeonCount = 6;

    [Header("던전 클리어 상태")]
    [Tooltip("던전별 클리어 여부 (0번 인덱스 = 1던전)")]
    [SerializeField] private bool[] cleared = new bool[DungeonCount];

    [Header("기억의 조각")]
    [Tooltip("상자에 투입된 기억의 조각 수 (0~6)")]
    [SerializeField, Range(0, DungeonCount)] private int fragmentsInserted;

    /// 진행 상태가 바뀔 때 호출 (구독자: LobbyGate, MemoryBox, LobbyBoard)
    public event Action OnChanged;

    /// 클리어한 던전 수
    public int ClearedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < cleared.Length; i++)
            {
                if (cleared[i]) count++;
            }
            return count;
        }
    }

    /// 보유 중이면서 아직 투입하지 않은 조각 수 = 클리어 수 - 투입 수
    public int FragmentsOwned => ClearedCount - fragmentsInserted;

    public int FragmentsInserted => fragmentsInserted;

    public bool AllInserted => fragmentsInserted >= DungeonCount;

    public bool IsCleared(int dungeonIndex)
    {
        return dungeonIndex >= 1 && dungeonIndex <= DungeonCount && cleared[dungeonIndex - 1];
    }

    /// 던전 클리어 처리 (던전 씬 마지막에서 호출). 기억의 조각 1개가 생긴다.
    public void MarkCleared(int dungeonIndex)
    {
        if (dungeonIndex < 1 || dungeonIndex > DungeonCount) return;

        cleared[dungeonIndex - 1] = true;
        OnChanged?.Invoke();
    }

    /// 보유한 조각을 상자에 투입한다. 성공 여부를 반환한다.
    public bool TryInsertFragment()
    {
        if (FragmentsOwned <= 0 || AllInserted) return false;

        fragmentsInserted++;
        OnChanged?.Invoke();
        return true;
    }

    [ContextMenu("진행도 초기화")]
    public void ResetProgress()
    {
        Array.Clear(cleared, 0, cleared.Length);
        fragmentsInserted = 0;
        OnChanged?.Invoke();
    }

    [ContextMenu("테스트: 1던전 클리어")]
    private void TestClearDungeon1()
    {
        MarkCleared(1);
    }
}
