// ============================================================
// AirAttackDirection.cs  v1.0
// 공중 공격 방향 열거형
//
// [역할]
//   4방향 공중 공격 방향을 명확하게 정의.
//   RustyKeyWeapon, MovementAnimator, PlayerWeaponHitboxManager 에서 공통 사용.
//
// [방향 판단 규칙]
//   ↓ + 좌우 : Down  (하향)
//   ↑ + 좌우 : Up    (상향)
//   좌우만   : Side  (수평 — 기존 AirAttack)
//   입력 없음 : Side  (기본값)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

namespace KEY
{
    /// <summary>
    /// 공중 공격 방향. (v1.0)
    /// </summary>
    public enum PlayerAirAttackDirection
    {
        /// <summary> 수평 방향 (좌우 바라보는 방향). 기본값. </summary>
        Side,
        /// <summary> 하향 (내리찍기). ↓ + 좌우 입력 시. </summary>
        Down,
        /// <summary> 상향. ↑ + 좌우 입력 시. </summary>
        Up,
    }
}