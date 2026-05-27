// ============================================================
// EnemyDummy.cs  v1.2
// 자물쇠 없는 완전 정지 더미 적
//
// [v1.2 변경]
//   gravityScale = 1 (중력 적용 — 바닥 착지)
//   FreezePositionY 제거 (Y 축 자유 — 낙하 가능)
//   FreezeRotation Z 만 유지 (회전 고정)
//   넉백은 EnemyBase.KnockbackRoutine 에서 X 축만 제어하므로
//   Y 축(중력/낙하)에 간섭 없음.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 자물쇠 없는 완전 정지 더미 적. (v1.2)
    /// </summary>
    public class EnemyDummy : EnemyBase
    {
        protected override void Awake()
        {
            base.Awake();

            if (_rigid2D != null)
            {
                // 중력 적용 — 씬 배치 시 바닥에 정상 착지
                // FreezeRotation Z 만 — 넉백 시 회전 방지
                // FreezePositionY 없음 — 낙하/중력 정상 작동
                _rigid2D.gravityScale = 1f;
                _rigid2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        protected override void OnDamaged(DamageInfo info) { }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}