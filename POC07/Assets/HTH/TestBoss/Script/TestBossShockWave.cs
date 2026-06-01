// ============================================================
// TestBossShockwave.cs  v1.1
// 테스트 보스 전용 충격파 컴포넌트
//
// [v1.1 변경 — BlockPlayerMoveRoutine WaitForSeconds → WaitForSecondsRealtime]
//
//   [기존 v1.0 문제]
//     BlockPlayerMoveRoutine 에서 WaitForSeconds(duration) 사용.
//     → WaitForSeconds 는 Time.timeScale 영향을 받음.
//     → 히트스탑 중 timeScale = 0.02 이면
//       WaitForSeconds(0.3f) 가 실제로 15초를 대기.
//     → Block 이 풀리지 않는 동안 PlayerMover 가 velocity 덮어씀.
//     → 충격파 넉백 무효화.
//
//   [v1.2 수정]
//     WaitForSeconds → WaitForSecondsRealtime 으로 교체.
//     → timeScale 영향 없이 실시간 duration 동안 차단 보장.
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
    /// 테스트 보스 전용 충격파 컴포넌트. (v1.1)
    /// </summary>
    public class TestBossShockwave : MonoBehaviour
    {
        [Header("── 충격파 수치 ──────────────────────")]
        [Tooltip("충격파 감지 반경 (units). 권장: 6~12.")]
        [Min(0.5f)]
        [SerializeField] private float _shockwaveRadius = 8f;

        [Tooltip("충격파 밀침 강도. 권장: 15~30.")]
        [Min(0f)]
        [SerializeField] private float _shockwavePower = 20f;

        [Tooltip("상방 힘 가중치. 권장: 0.3~0.5.")]
        [Range(0f, 1f)]
        [SerializeField] private float _upwardBias = 0.4f;

        [Header("── 레이어 ──────────────────────")]
        [Tooltip("플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("── 히트스탑 ──────────────────────")]
        [Tooltip("히트스탑 지속 시간 (실시간 초). 0 = 없음.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hitStopDuration = 0.08f;

        [Tooltip("히트스탑 TimeScale. 권장: 0.0~0.05.")]
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
            // 1. 파티클
            if (_shockwaveEffect != null)
            {
                _shockwaveEffect.transform.position = origin;
                _shockwaveEffect.Play();
            }

            // 2. 카메라 셰이크
            if (_cameraTransform != null && _cameraShakeStrength > 0f)
            {
                _cameraTransform.DOKill();
                _cameraTransform.DOShakePosition(
                    _cameraShakeDuration,
                    strength: new Vector3(_cameraShakeStrength, _cameraShakeStrength, 0f),
                    vibrato: 20,
                    randomness: 90f);
            }

            // 3. 히트스탑
            if (_hitStopDuration > 0f)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine());
            }

            // 4. 플레이어 밀침
            int count = Physics2D.OverlapCircleNonAlloc(
                origin, _shockwaveRadius, _overlapBuffer, _playerLayer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent<Rigidbody2D>(out var rb)) continue;

                Vector2 horizontal = ((Vector2)col.transform.position
                    - (Vector2)origin).normalized;
                Vector2 finalDir = Vector2.Lerp(horizontal, Vector2.up, _upwardBias).normalized;

                rb.linearVelocity = finalDir * _shockwavePower;

                // 이동 차단 — 충격파 날아가는 동안
                if (InputManager.Instance != null)
                    StartCoroutine(BlockPlayerMoveRoutine(_hitStopDuration + 0.3f));

                Debug.Log($"[TestBossShockwave] 플레이어 밀침 방향:{finalDir} 강도:{_shockwavePower}");
            }
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 이동 입력 일시 차단.
        ///
        /// [v1.1 수정]
        ///   WaitForSeconds → WaitForSecondsRealtime
        ///   → timeScale 이 낮아도 실시간 duration 보장
        ///   → 히트스탑 중에도 정확한 차단 시간 유지
        /// </summary>
        private IEnumerator BlockPlayerMoveRoutine(float duration)
        {
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockJump();
            InputManager.Instance?.BlockDash();

            // ★ v1.1: WaitForSecondsRealtime — timeScale 영향 없음
            yield return new WaitForSecondsRealtime(duration);

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