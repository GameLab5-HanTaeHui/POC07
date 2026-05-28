// ============================================================
// EnemySensor.cs  v1.1
// 적 공용 감지 컴포넌트 — chargeRange 추가
//
// [v1.1 변경]
//   CheckChargeRange() 추가.
//   EnemyAI 에서 차징 공격 진입 조건으로 사용.
//   차징은 일반 공격보다 훨씬 먼 거리에서 발동.
//   EnemyDataSO.chargeDetectRange 로 범위 설정.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    public class EnemySensor : MonoBehaviour
    {
        private EnemyDataSO _data;
        private float _facingDirection = 1f;
        private Transform _detectedPlayer;

        public Transform DetectedPlayer => _detectedPlayer;

        public void SetData(EnemyDataSO data) => _data = data;
        public void SetFacingDirection(float dir) => _facingDirection = dir;

        /// <summary> 순찰 전방 직선 — 플레이어 감지. </summary>
        public bool CheckPatrolSight()
        {
            if (_data == null) return false;
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                new Vector2(_facingDirection, 0f),
                _data.patrolSightRange,
                _data.playerLayer);

            if (hit.collider != null) { _detectedPlayer = hit.collider.transform; return true; }
            return false;
        }

        /// <summary> 전방 수평 — 벽 감지. </summary>
        public bool CheckWall()
        {
            if (_data == null) return false;
            return Physics2D.Raycast(
                transform.position,
                new Vector2(_facingDirection, 0f),
                _data.wallCheckDistance,
                _data.groundLayer).collider != null;
        }

        /// <summary> 발 앞 하향 — 낭떠러지 감지. </summary>
        public bool CheckCliff()
        {
            if (_data == null) return false;
            Vector2 origin = (Vector2)transform.position
                             + new Vector2(_facingDirection * _data.cliffCheckOffset, 0f);
            return Physics2D.Raycast(origin, Vector2.down,
                _data.cliffCheckDistance, _data.groundLayer).collider == null;
        }

        /// <summary> 원형 — 추격 범위 내 플레이어 유지. </summary>
        public bool CheckChaseRange()
        {
            if (_data == null) return false;
            Collider2D hit = Physics2D.OverlapCircle(
                transform.position, _data.chaseSightRadius, _data.playerLayer);
            if (hit != null) { _detectedPlayer = hit.transform; return true; }
            _detectedPlayer = null;
            return false;
        }

        /// <summary> 원형(소) — 근접 공격 사정거리 진입. </summary>
        public bool CheckAttackRange()
        {
            if (_data == null) return false;
            return Physics2D.OverlapCircle(
                transform.position, _data.attackRange, _data.playerLayer) != null;
        }

        /// <summary>
        /// 원형(중) — 차징 공격 발동 감지 범위. (v1.1 추가)
        /// attackRange 보다 크고 chaseSightRadius 보다 작게 설정.
        /// EnemyAI.UpdateState 에서 매 프레임 체크.
        /// </summary>
        public bool CheckChargeRange()
        {
            if (_data == null) return false;
            return Physics2D.OverlapCircle(
                transform.position, _data.chargeDetectRange, _data.playerLayer) != null;
        }

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position,
                new Vector3(_facingDirection * _data.patrolSightRange, 0f));

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position,
                new Vector3(_facingDirection * _data.wallCheckDistance, 0f));

            Vector3 cliffOrigin = transform.position
                + new Vector3(_facingDirection * _data.cliffCheckOffset, 0f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(cliffOrigin, new Vector3(0f, -_data.cliffCheckDistance, 0f));

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _data.chaseSightRadius);

            // 차징 감지 범위 — 주황 실선
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _data.chargeDetectRange);

            // 근접 공격 범위 — 빨간
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _data.attackRange);
        }
    }
}