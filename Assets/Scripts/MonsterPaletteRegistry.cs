using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 팔레트 목록. <c>Assets/Resources</c>에 두고 <see cref="MonsterPaletteLoader"/>로 로드합니다.
/// 프리팹은 <c>Assets/Prefabs/...</c> 등 어디에 두어도 되며, 여기서 참조만 하면 빌드에 포함됩니다.
/// </summary>
[CreateAssetMenu(menuName = "JumpGame/Monster Palette Registry", fileName = "MonsterPaletteRegistry")]
public class MonsterPaletteRegistry : ScriptableObject
{
    [Tooltip("맵 JSON의 prefabId와 일치하는 id. 비우면 프리팹 이름이 id로 쓰입니다.")]
    public List<MonsterPaletteEntry> entries = new List<MonsterPaletteEntry>();
}
