using UnityEngine;

/// <summary>
/// 껍데기 등 1 데미지 처리가 필요한 중간급 몬스터용. 없으면 껍데기에 맞으면 즉사로 처리.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 2;

    private int currentHp;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHp -= amount;
        if (currentHp <= 0)
            Destroy(gameObject);
    }
}
