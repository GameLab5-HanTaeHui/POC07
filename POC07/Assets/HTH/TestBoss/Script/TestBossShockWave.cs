// ============================================================
// TestBossShockwave.cs  v1.3
// 테스트 보스 전용 충격파 컴포넌트
//
// [v1.3 변경 — 수평 넉백 덮어씌워지는 문제 수정]
//
//   [문제]
//     BlockMove() 를 동기 호출 후 velocity 설정해도
//     같은 프레임 내에서 PlayerMover.ApplyMovement() 가
//     FixedUpdate 에서 velocity.x 를 덮어씀.
//     → 수직(Y)은 PlayerMover 가 건드리지 않아서 upwardForce 는 적용됨.
//     → 수평(X)은 덮어씌워져 넉백 없이 위로만 솟아오름.
//
//   [수정]
//     Trigger() 에서 코루틴 시작.
//     코루틴 내부에서:
//       1. Block 즉시
//       2. WaitForFixedUpdate() — 다음 FixedUpdate 까지 대기
//          → PlayerMover 가 이번 프레임 velocity 덮어쓰기 완료 후
//       3. velocity 설정 (수평 + 수직 동시)
//          → 이후 PlayerMover 는 Block 상태라 velocity.x 안 건드림
//       4. WaitForSecondsRealtime(blockDuration) — 날아가는 동안 차단 유지
//       5. Unblock
//
// [수평 + 수직 분리 설계]
//   수평: 보스 → 플레이어 방향 * _shockwavePower  (뒤로 날아가는 힘)
//   수직: _upwardForce  (위로 튀어오르는 힘)
//   결과: 대각선으로 날아가는 느낌
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 전용 충격파 컴포넌트. (v1.3)
    /// </summary>
    public class TestBossShockwave : MonoBehaviour
    {
        [Header("── 충격파 수치 ──────────────────────")]

        [Tooltip("충격파 감지 반경 (units). 권장: 6~12.")]
        [Min(0.5f)]
        [SerializeField] private float _shockwaveRadius = 8f;

        [Tooltip("수평 밀침 강도 (뒤로 날아가는 힘). 권장: 15~25.")]
        [Min(0f)]
        [SerializeField] private float _shockwavePower = 20f;

        [Tooltip("수직 튀어오르는 힘 (위로 솟는 힘). 권장: 8~15.")]
        [Min(0f)]
        [SerializeField] private float _upwardForce = 10f;

        [Tooltip("이동 차단 지속 시간 (실시간 초). 권장: 0.4~0.8.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _blockDuration = 0.5f;

        [Header("── 레이어 ──────────────────────")]
        [Tooltip("플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("── 히트스탑 ──────────────────────")]
        [Tooltip("히트스탑 지속 시간 (실시간 초). 0 = 없음.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hitStopDuration = 0.08f;

        [Tooltip("히트스탑 TimeScale.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopTimeScale = 0.02f;

        [Header("── 카메라 셰이크 (선택) ──────────────────────")]
        [Tooltip("카메라 Transform. 미연결 시 셰이크 없음.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("카메라 셰이크 강도.")]
        [Min(0f)]
        [SerializeField] private float _cameraShakeStrength = 0.3f;

        [Tooltip("카메라 셰이크 지속 시간.")]
        [Min(0f)]
        [SerializeField] private float _cameraShakeDuration = 0.3f;

        [Header("── 이펙트 (선택) ──────────────────────")]
        [Tooltip("충격파 파티클.")]
        [SerializeField] private ParticleSystem _shockwaveEffect;

        // ──────────────────────────────────────────
        private readonly Collider2D[] _overlapBuffer = new Collider2D[8];
        private Coroutine _hitStopCoroutine;

        // ══════════════════════════════════════════════════════
        // 충격파 발동
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 발동.
        /// TestBossCore.ExitDilTime() 에서 호출.
        /// </summary>
        public void Trigger(Vector3 origin)
        {
            // 파티클
            if (_shockwaveEffect != null)
            {
                _shockwaveEffect.transform.position = origin;
                _shockwaveEffect.Play();
            }

            // 카메라 셰이크
            if (_cameraTransform != null && _cameraShakeStrength > 0f)
            {
                _cameraTransform.DOKill();
                _cameraTransform.DOShakePosition(
                    _cameraShakeDuration,
                    strength: new Vector3(_cameraShakeStrength, _cameraShakeStrength, 0f),
                    vibrato: 20,
                    randomness: 90f);
            }

            // 히트스탑
            if (_hitStopDuration > 0f)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine());
            }

            // 플레이어 감지 → 코루틴으로 넉백 처리
            int count = Physics2D.OverlapCircleNonAlloc(
                origin, _shockwaveRadius, _overlapBuffer, _playerLayer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null) continue;

                Rigidbody2D rb = col.GetComponentInParent<Rigidbody2D>();
                if (rb == null) continue;

                // 수평 방향: 보스 → 플레이어 (순수 X축)
                float dx = rb.position.x - (float)origin.x;
                float horizontalSign = dx >= 0f ? 1f : -1f;

                // ★ 코루틴으로 처리 — WaitForFixedUpdate 후 velocity 설정
                StartCoroutine(ApplyShockwaveRoutine(rb, horizontalSign));
            }
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 넉백 코루틴.
        ///
        /// [순서]
        ///   1. Block 즉시 — PlayerMover 입력 차단
        ///   2. WaitForFixedUpdate — 현재 프레임 PlayerMover.FixedUpdate 완료 대기
        ///   3. velocity 설정 — Block 상태이므로 PlayerMover 덮어쓰기 없음
        ///      수평: horizontalSign * _shockwavePower (뒤로 날아가는 힘)
        ///      수직: _upwardForce (대각선 날아가는 느낌)
        ///   4. WaitForSecondsRealtime(blockDuration) — 날아가는 동안 차단 유지
        ///   5. Unblock
        /// </summary>
        private IEnumerator ApplyShockwaveRoutine(Rigidbody2D rb, float horizontalSign)
        {
            // ① 즉시 Block
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockJump();
            InputManager.Instance?.BlockDash();

            // ② 다음 FixedUpdate 까지 대기
            //    → 이 프레임 PlayerMover.ApplyMovement() 가 먼저 실행되고
            //    → 다음 프레임부터 Block 상태이므로 velocity 덮어쓰기 없음
            yield return new WaitForFixedUpdate();

            // ③ velocity 설정 (수평 + 수직 동시)
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(
                    horizontalSign * _shockwavePower,  // 뒤로 날아가는 수평 힘
                    _upwardForce);                      // 대각선 느낌의 수직 힘

                Debug.Log($"[TestBossShockwave] 넉백 적용 → X:{horizontalSign * _shockwavePower:F1} Y:{_upwardForce:F1}");
            }

            // ④ 날아가는 동안 차단 유지 (실시간 — timeScale 무관)
            yield return new WaitForSecondsRealtime(_blockDuration);

            // ⑤ Unblock
            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockJump();
            InputManager.Instance?.UnblockDash();
        }

        /// <summary>
        /// 히트스탑 코루틴.
        /// WaitForSecondsRealtime: timeScale 영향 없음.
        /// </summary>
        private IEnumerator HitStopRoutine()
        {
            float original = Time.timeScale;
            Time.timeScale = _hitStopTimeScale;
            yield return new WaitForSecondsRealtime(_hitStopDuration);
            Time.timeScale = original;
            _hitStopCoroutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _shockwaveRadius);
        }
    }
}