using UnityEngine;

/// <summary>
/// 생존·이동 중 몬스터는 Kinematic + 중력 0으로 두어 다른 Rigidbody와의 충돌 임펄스에 덜 끌려가게 함.
/// 낙하·튕김 사망 연출은 각 스크립트에서 <see cref="RigidbodyType2D.Dynamic"/>으로 전환.
/// </summary>
public static class MonsterKinematicSetup
{
    public static void ApplyGameplayKinematic(Rigidbody2D rb)
    {
        if (rb == null) return;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }
}

/// <summary>
/// Kinematic 몬스터용: 발밑 레이로 Ground 여부를 보고, 공중이면 linearVelocity Y를 가속해 낙하.
/// </summary>
public static class MonsterKinematicFall
{
    const float FootRayInset = 0.02f;
    const float FootRayExtraLength = 0.08f;

    public static bool IsGrounded(Rigidbody2D rb, Collider2D bodyCollider, string groundTag, float checkDistance)
    {
        if (bodyCollider == null || rb == null) return false;
        if (string.IsNullOrEmpty(groundTag)) return false;

        float baseDist = Mathf.Max(0.05f, checkDistance);
        float overlapRadius = Mathf.Max(0.08f, baseDist * 0.5f);
        Vector2 footProbe = new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y - overlapRadius * 0.5f);

        foreach (var c in Physics2D.OverlapCircleAll(footProbe, overlapRadius))
        {
            if (c == null) continue;
            if (c.attachedRigidbody == rb) continue;
            if (ReferenceEquals(c, bodyCollider)) continue;
            if (HasGroundTagInHierarchy(c.transform, groundTag))
                return true;
        }

        Vector2 rayOrigin = new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + FootRayInset);
        float rayLen = baseDist + FootRayExtraLength;
        foreach (var h in Physics2D.RaycastAll(rayOrigin, Vector2.down, rayLen))
        {
            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody == rb) continue;
            if (HasGroundTagInHierarchy(h.collider.transform, groundTag))
                return true;
        }

        return false;
    }

    static bool HasGroundTagInHierarchy(Transform t, string groundTag)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag(groundTag))
                return true;
        }
        return false;
    }

    /// <summary>공중이면 아래로 가속, 착지 시 아래 방향 속도만 0으로.</summary>
    public static float NextVerticalVelocity(
        float currentVy,
        bool grounded,
        float fallAcceleration,
        float maxFallSpeed,
        float fixedDeltaTime)
    {
        if (grounded)
        {
            if (currentVy < 0f)
                return 0f;
            return currentVy;
        }

        return Mathf.Max(currentVy - fallAcceleration * fixedDeltaTime, -maxFallSpeed);
    }
}
