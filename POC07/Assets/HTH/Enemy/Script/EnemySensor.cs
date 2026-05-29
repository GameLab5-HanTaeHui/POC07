// ============================================================
// EnemySensor.cs  v2.0
// 적 공용 감지 컴포넌트 — 리모델링
//
// [v2.0 리모델링 변경]
//
//   [attackRange / CheckAttackRange() 제거]
//     기사형은 차징 돌진만 사용.
//     근접 공격 범위 체크 불필요.
//     EnemyDataSO v3.0 에서 attackRange 필드도 제거됨.
//
//   [CheckChargeRange() 유지]
//     차징 발동 감지 범위.
//     chargeDetectRange 범위 안에 플레이어 → 차징 시작 조건.
//
//   [SetData 방식 유지]
//     EnemyAI.Start() 에서 SetData(_settings) 호출.
//     SetFacingDirection(dir) 으로 Flip 방향 동기화.
//
//   [DetectedPlayer 프로퍼티 유지]
//     CheckPatrolSight() / CheckChaseRange() 에서 감지 시 갱신.
//     EnemyAI.UpdateChaseDirection() / GroggyRoutine() 에서 읽음.
//
// [v1.1 변경]
//   CheckChargeRange() 추가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 감지 컴포넌트. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [감지 메서드 목록]
    ///   CheckPatrolSight() : 순찰 중 전방 직선 Raycast — 플레이어 감지
    ///   CheckWall()        : 전방 수평 Raycast — 벽 감지
    ///   CheckCliff()       : 발 앞 하향 Raycast — 낭떠러지 감지
    ///   CheckChaseRange()  : 원형 OverlapCircle — 추격 유지 범위
    ///   CheckChargeRange() : 원형 OverlapCircle — 차징 발동 범위
    ///
    /// [EnemyAI 에서 사용 방식]
    ///   Patrol 상태 : CheckWall(), CheckCliff(), CheckPatrolSight()
    ///   Chase 상태  : CheckChaseRange(), CheckChargeRange()
    ///   Groggy 종료 : DetectedPlayer 방향으로 TurnTowardPlayer()
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemySensor : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private float _facingDirection = 1f;
        private Transform _detectedPlayer;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 마지막으로 감지된 플레이어 Transform.
        /// CheckPatrolSight() / CheckChaseRange() 에서 갱신.
        /// null 이면 플레이어 미감지 상태.
        /// </summary>
        public Transform DetectedPlayer => _detectedPlayer;

        // ══════════════════════════════════════════════════════
        // 외부 API — EnemyAI.Start() 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// DataSO 주입. EnemyAI.Start() 에서 호출.
        /// </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        /// <summary>
        /// 현재 바라보는 방향 갱신.
        /// EnemyAI.SetFacing() 에서 방향 전환 시 호출.
        /// </summary>
        public void SetFacingDirection(float dir) => _facingDirection = dir;

        // ══════════════════════════════════════════════════════
        // 감지 메서드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 순찰 중 전방 직선 Raycast — 플레이어 감지.
        /// patrolSightRange 거리 내에 플레이어 발견 시 true.
        /// 감지 시 _detectedPlayer 갱신.
        ///
        /// [EnemyAI 사용처]
        ///   Patrol / Idle 상태에서 매 프레임 체크.
        ///   true → ChangeState(Chase).
        /// </summary>
        public bool CheckPatrolSight()
        {
            if (_data == null) return false;

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                new Vector2(_facingDirection, 0f),
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
        /// 전방 수평 Raycast — 벽 감지.
        /// wallCheckDistance 거리 내에 Ground 레이어 감지 시 true.
        ///
        /// [EnemyAI 사용처]
        ///   Patrol 상태. true → Flip() + TryEnterIdle().
        /// </summary>
        public bool CheckWall()
        {
            if (_data == null) return false;
            return Physics2D.Raycast(
                transform.position,
                new Vector2(_facingDirection, 0f),
                _data.wallCheckDistance,
                _data.groundLayer).collider != null;
        }

        /// <summary>
        /// 발 앞 하향 Raycast — 낭떠러지 감지.
        /// cliffCheckOffset 만큼 앞에서 아래로 Ray 발사.
        /// 바닥이 없으면 (Ray 미충돌) true → 낭떠러지.
        ///
        /// [EnemyAI 사용처]
        ///   Patrol 상태. true → Flip() + TryEnterIdle().
        ///   ChargeAttack 의 ScanForObstacle 에서도 활용.
        /// </summary>
        public bool CheckCliff()
        {
            if (_data == null) return false;

            Vector2 origin = (Vector2)transform.position
                + new Vector2(_facingDirection * _data.cliffCheckOffset, 0f);

            // 바닥 Ray 가 아무것도 감지 못하면 낭떠러지
            return Physics2D.Raycast(
                origin,
                Vector2.down,
                _data.cliffCheckDistance,
                _data.groundLayer).collider == null;
        }

        /// <summary>
        /// 원형 OverlapCircle — 추격 유지 범위.
        /// chaseSightRadius 범위 내에 플레이어 있으면 true.
        /// 없으면 _detectedPlayer = null.
        ///
        /// [EnemyAI 사용처]
        ///   Chase 상태. false → ChangeState(Patrol).
        /// </summary>
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
        /// 원형 OverlapCircle — 차징 발동 감지 범위.
        /// chargeDetectRange 범위 내에 플레이어 있으면 true.
        ///
        /// [EnemyAI 사용처]
        ///   Chase 상태. true + 차징 쿨타임 완료 → ChangeState(Attack) → 차징 실행.
        ///   chargeDetectRange : patrolSightRange < x < chaseSightRadius 범위 권장.
        /// </summary>
        public bool CheckChargeRange()
        {
            if (_data == null) return false;

            return Physics2D.OverlapCircle(
                transform.position,
                _data.chargeDetectRange,
                _data.playerLayer) != null;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            // 순찰 직선 감지 — 노란선
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position,
                new Vector3(_facingDirection * _data.patrolSightRange, 0f, 0f));

            // 벽 감지 — 빨간선
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position,
                new Vector3(_facingDirection * _data.wallCheckDistance, 0f, 0f));

            // 낭떠러지 하향 — 보라선
            Vector3 cliffOrigin = transform.position
                + new Vector3(_facingDirection * _data.cliffCheckOffset, 0f, 0f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(cliffOrigin, new Vector3(0f, -_data.cliffCheckDistance, 0f));

            // 추격 유지 범위 — 반투명 주황원
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _data.chaseSightRadius);

            // 차징 발동 범위 — 주황원 (실선)
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _data.chargeDetectRange);
        }
    }
}