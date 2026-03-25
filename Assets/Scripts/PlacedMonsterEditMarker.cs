using UnityEngine;

/// <summary>
/// 에디트 씬에 배치된 몬스터 인스턴스. 저장 시 prefabId·위치 수집에 사용됩니다.
/// 맵 편집 중에는 물리·애니를 멈춰 그 자리에 고정합니다(테스트 플레이 시 다시 동작).
/// </summary>
[DefaultExecutionOrder(100)]
public class PlacedMonsterEditMarker : MonoBehaviour
{
    [HideInInspector] public string paletteId;

    private Rigidbody2D rb;
    private Animator[] animators;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animators = GetComponentsInChildren<Animator>(true);
    }

    private void Update()
    {
        bool edit = GameState.IsMapEditMode;

        if (rb != null)
        {
            if (edit)
            {
                rb.simulated = false;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            else
                rb.simulated = true;
        }

        if (animators != null)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].speed = edit ? 0f : 1f;
            }
        }
    }
}
