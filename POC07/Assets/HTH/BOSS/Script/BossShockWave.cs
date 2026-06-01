// ============================================================
// BossShockwave.cs  v1.0
// 충격파 전용 컴포넌트 — 데미지 없음, 플레이어 밀침만
//
// [역할]
//   보스 주변 일정 범위 내 플레이어를 밀쳐내는 충격파.
//   데미지 없음. 순수 넉백 전용.
//
// [발동 시점]
//   Phase 전환 시 (BossKnight.EnterPhaseTransition)
//   그로기 회복 중단 시 (BossExecutionHandler.OnExecutionInterrupted)
//   딜타임 종료 시 (BossCoreLock.ExitDilTime)
//
// [충격파 동작]
//   OverlapCircle 로 playerLayer 감지
//   → Rigidbody2D 에 방향 × power 적용
//   → 데미지는 없음 (IDamageable 호출 안 함)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 충격파 전용 컴포넌트. (v1.0)
    /// </summary>
    public class BossShockwave : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이펙트 ──────────────────────")]

        [Tooltip("충격파 파티클. 발동 시 재생.")]
        [SerializeField] private ParticleSystem _shockwaveEffect;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private BossKnightDataSO _data;

        // ──────────────────────────────────────────
        // 버퍼
        // ──────────────────────────────────────────

        private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        public void Initialize(BossKnightDataSO data)
        {
            _data = data;
        }

        // ══════════════════════════════════════════════════════
        // 충격파 발동
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 발동.
        /// 지정 위치를 중심으로 shockwaveRadius 범위 내 플레이어 밀침.
        /// 데미지 없음 — Rigidbody2D.AddForce 만 적용.
        /// </summary>
        public void Trigger(Vector3 origin)
        {
            if (_data == null) return;

            // 이펙트 재생
            if (_shockwaveEffect != null)
            {
                _shockwaveEffect.transform.position = origin;
                _shockwaveEffect.Play();
            }

            // OverlapCircle 로 플레이어 감지
            int count = Physics2D.OverlapCircleNonAlloc(
                origin,
                _data.shockwaveRadius,
                _overlapBuffer,
                _data.playerLayer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null) continue;

                if (!col.TryGetComponent<Rigidbody2D>(out var rb)) continue;

                // 충격파 방향 = 보스 중심 → 플레이어
                Vector2 dir = ((Vector2)col.transform.position - (Vector2)origin).normalized;

                // 데미지 없이 힘만 적용
                rb.AddForce(dir * _data.shockwavePower, ForceMode2D.Impulse);

                Debug.Log($"[BossShockwave] 충격파 발동 → {col.name} " +
                          $"방향:{dir} 강도:{_data.shockwavePower}");
            }
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _data.shockwaveRadius);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.down * 0.5f,
                $"Shockwave R:{_data.shockwaveRadius} P:{_data.shockwavePower}");
#endif
        }
    }
}