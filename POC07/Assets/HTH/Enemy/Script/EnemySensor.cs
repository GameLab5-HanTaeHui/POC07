// ============================================================
// EnemySensor.cs  v1.0
// 적 공용 감지 컴포넌트
//
// [역할]
//   모든 적 캐릭터가 공유하는 감지 로직 전담 컴포넌트.
//   EnemyAI 에서 참조하여 상태 전환 판단에 사용.
//
// [감지기 5종]
//   1. PatrolRaycast     : 순찰 전방 직선 — 플레이어 감지 → Chase 전환
//   2. WallRaycast       : 전방 수평 — 벽 감지 → 방향 반전
//   3. CliffRaycast      : 발 앞 하향 — 낭떠러지 감지 → 방향 반전
//   4. ChaseOverlap      : 중심 원형 — 추격 범위 내 플레이어 유지 확인
//   5. AttackOverlap     : 중심 원형(소) — 공격 사정거리 진입 확인
//
// [사용 방법]
//   EnemyDataSO 를 SetData() 로 주입 후 각 Check 메서드 호출.
//   EnemyAI.Update() 에서 매 프레임 호출하거나
//   필요한 상태에서만 선택적으로 호출.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 감지 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [EnemyAI 에서의 사용 흐름]
    ///   Patrol 상태:
    ///     CheckWall()    → true → 방향 반전
    ///     CheckCliff()   → true → 방향 반전
    ///     CheckPatrolSight() → true → Chase 전환
    ///
    ///   Chase 상태:
    ///     CheckChaseRange()  → false → Patrol 복귀
    ///     CheckAttackRange() → true  → Attack 전환
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemySensor : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 데이터 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 감지 수치 데이터. SetData() 로 주입.
        /// sightRange, attackRange, playerLayer, groundLayer 등을 읽음.
        /// </summary>
        private EnemyDataSO _data;

        /// <summary>
        /// 현재 이동 방향. 1 = 오른쪽, -1 = 왼쪽.
        /// EnemyAI 에서 매 프레임 동기화.
        /// </summary>
        private float _facingDirection = 1f;

        // ──────────────────────────────────────────
        // 캐시
        // ──────────────────────────────────────────

        /// <summary> 마지막으로 감지된 플레이어 Transform. </summary>
        private Transform _detectedPlayer;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 마지막으로 감지된 플레이어. 없으면 null. </summary>
        public Transform DetectedPlayer => _detectedPlayer;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 감지 수치 데이터를 주입한다.
        /// EnemyAI.Awake() 또는 Start() 에서 호출.
        /// </summary>
        /// <param name="data">EnemyDataSO (감지 범위/레이어 포함)</param>
        public void SetData(EnemyDataSO data)
        {
            _data = data;
        }

        /// <summary>
        /// 현재 이동 방향을 동기화한다.
        /// EnemyAI 에서 방향 변경 시 호출.
        /// </summary>
        /// <param name="direction">1 = 오른쪽, -1 = 왼쪽</param>
        public void SetFacingDirection(float direction)
        {
            _facingDirection = direction;
        }

        // ══════════════════════════════════════════════════════
        // 감지 메서드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// [순찰 감지] 전방 직선 Raycast — 플레이어 감지 여부.
        /// 감지 시 _detectedPlayer 캐시 갱신.
        /// 순찰 상태에서 Chase 전환 조건으로 사용.
        /// </summary>
        /// <returns>플레이어 감지 시 true</returns>
        public bool CheckPatrolSight()
        {
            if (_data == null) return false;

            Vector2 origin = transform.position;
            Vector2 direction = new Vector2(_facingDirection, 0f);

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction,
                _data.patrolSightRange,
                _data.playerLayer);

            if (hit.collider != null)
            {
                _detectedPlayer = hit.collider.transform;
                return true;
            }

            return false;
        }

        /// <summary>
        /// [지형 감지] 전방 수평 Raycast — 벽 충돌 여부.
        /// Patrol 상태에서 방향 반전 조건으로 사용.
        /// </summary>
        /// <returns>벽 감지 시 true</returns>
        public bool CheckWall()
        {
            if (_data == null) return false;

            Vector2 origin = transform.position;
            Vector2 direction = new Vector2(_facingDirection, 0f);

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction,
                _data.wallCheckDistance,
                _data.groundLayer);

            return hit.collider != null;
        }

        /// <summary>
        /// [지형 감지] 발 앞 하향 Raycast — 낭떠러지 여부.
        /// 발 앞쪽 오프셋에서 아래로 Ray 발사.
        /// 지면이 없으면 낭떠러지 → 방향 반전.
        /// </summary>
        /// <returns>낭떠러지(지면 없음) 시 true</returns>
        public bool CheckCliff()
        {
            if (_data == null) return false;

            // 발 앞쪽 오프셋에서 하향 Ray
            Vector2 origin = (Vector2)transform.position
                             + new Vector2(_facingDirection * _data.cliffCheckOffset, 0f);

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                Vector2.down,
                _data.cliffCheckDistance,
                _data.groundLayer);

            // 지면이 없으면 낭떠러지
            return hit.collider == null;
        }

        /// <summary>
        /// [추격 감지] 원형 OverlapCircle — 추격 범위 내 플레이어 유지 여부.
        /// Chase 상태에서 매 프레임 호출.
        /// false 가 되면 Patrol 복귀.
        /// </summary>
        /// <returns>추격 범위 내 플레이어 존재 시 true</returns>
        public bool CheckChaseRange()
        {
            if (_data == null) return false;

            Collider2D hit = Physics2D.OverlapCircle(
                transform.position,
                _data.chaseSightRadius,
                _data.playerLayer);

            if (hit != null)
            {
                _detectedPlayer = hit.transform;
                return true;
            }

            _detectedPlayer = null;
            return false;
        }

        /// <summary>
        /// [공격 감지] 원형 OverlapCircle(소) — 공격 사정거리 진입 여부.
        /// Chase 상태에서 호출. true 시 Attack 전환.
        /// </summary>
        /// <returns>공격 사정거리 내 플레이어 존재 시 true</returns>
        public bool CheckAttackRange()
        {
            if (_data == null) return false;

            Collider2D hit = Physics2D.OverlapCircle(
                transform.position,
                _data.attackRange,
                _data.playerLayer);

            return hit != null;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            // 순찰 직선 감지
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(
                transform.position,
                new Vector3(_facingDirection * _data.patrolSightRange, 0f, 0f));

            // 벽 감지
            Gizmos.color = Color.red;
            Gizmos.DrawRay(
                transform.position,
                new Vector3(_facingDirection * _data.wallCheckDistance, 0f, 0f));

            // 낭떠러지 감지
            Vector3 cliffOrigin = transform.position
                                  + new Vector3(_facingDirection * _data.cliffCheckOffset, 0f, 0f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(cliffOrigin, new Vector3(0f, -_data.cliffCheckDistance, 0f));

            // 추격 원형
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _data.chaseSightRadius);

            // 공격 원형
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _data.attackRange);
        }
    }
}