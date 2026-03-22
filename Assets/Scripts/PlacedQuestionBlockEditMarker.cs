using UnityEngine;

/// <summary>
/// 에디트 씬에 배치된 물음표 블록 인스턴스. 저장 시 위치와 prefabId 수집에 사용됩니다.
/// </summary>
public class PlacedQuestionBlockEditMarker : MonoBehaviour
{
    [HideInInspector] public string paletteId;
}
