// ============================================================
// TestBossShockwave.cs  v1.2
// 테스트 보스 전용 충격파 컴포넌트
//
// [v1.2 변경 — 넉백 미적용 문제 수정]
//
//   [문제 1 — velocity 설정 전 Block 미보장]
//     기존: Trigger() 에서 velocity 설정 후 코루틴으로 Block 시작
//     → 코루틴의 첫 yield 전까지는 동기지만,
//       velocity 설정과 같은 프레임에 PlayerMover.FixedUpdate 가 실행되면
//       velocity 를 덮어씀.
//     수정: velocity 설정 전 BlockMove/Jump/Dash 즉시 동기 호출
//           → velocity 설정
//           → 코루틴으로 일정 시간 후 Unblock
//
//   [문제 2 — Rigidbody2D/위치 탐색 오류]
//     기존: col.TryGetComponent<Rigidbody2D>()
//     → col 이 플레이어 자식 Collider 이면 루트 Rigidbody2D 탐색 실패
//     → 방향 계산도 col.transform.position 기준이라 엉뚱한 방향
//     수정: col.GetComponentInParent<Rigidbody2D>()
//           방향 계산도 rb.transform.position (루트 위치) 기준
//
//   [문제 3 — 상방 bias 계산]
//     기존: Vector2.Lerp(horizontal, Vector2.up, _upwardBias).normalized
//     → Lerp 후 normalized 는 의도한 방향을 유지하지만
//       horizontal 이 순수 수평이면 Lerp 결과에 Y 성분이 섞임.
//       실제로는 정상이나 위치 오류로 horizontal Y 가 -0.01 로 계산됨.
//     수정: horizontal 을 (x, 0) 으로 강제하여 Y 오염 제거
//           + _upwardBias 를 Y 에 직접 더하는 방식으로 변경
//           finalDir = normalize(horizontal.x, _upwardBias * shockwavePower)
//           → 상방 성분이 명시적으로 보장됨
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
    /// 테스트 보스 전용 충격파 컴포넌트. (v1.2)
    /// </summary>
    public class TestBossShockwave : MonoBehaviour
    {
        [Header("── 충격파 수치 ──────────────────────")]
        [Tooltip("충격파 감지 반경 (units). 권장: 6~12.")]
        [Min(0.5f)]
        [SerializeField] private float _shockwaveRadius = 8f;

        [Tooltip("충격파 수평 밀침 강도. 권장: 15~25.")]
        [Min(0f)]
        [SerializeField] private float _shockwavePower = 20f;

        /// <summary>
        /// 상방 튀어오르는 힘 (별도 Y축 velocity).
        /// 수평 밀침과 독립적으로 Y velocity 에 더함.
        /// 권장: 8~15. 클수록 위로 높이 날아감.
        /// </summary>
        [Tooltip("상방 튀어오르는 힘 (Y velocity). 권장: 8~15.")]
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
        ///
        /// [v1.2 수정 — velocity 설정 보장 순서]
        ///   1. Block 즉시 동기 호출 (velocity 덮어쓰기 차단)
        ///   2. velocity 설정
        ///   3. 코루틴으로 blockDuration 후 Unblock
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

                // ★ v1.2: GetComponentInParent — 자식 Collider 여도 루트 Rigidbody2D 탐색
                Rigidbody2D rb = col.GetComponentInParent<Rigidbody2D>();
                if (rb == null) continue;

                // ★ v1.2: 이동 입력 즉시 차단 (velocity 설정 전에 먼저)
                InputManager.Instance?.BlockMove();
                InputManager.Instance?.BlockJump();
                InputManager.Instance?.BlockDash();

                // ★ v1.2: 수평 방향은 순수 X축으로 강제 (Y 오염 제거)
                //   rb.position 기준 → 루트 위치로 방향 계산
                float dx = rb.position.x - (float)origin.x;
                float horizontalSign = dx >= 0f ? 1f : -1f;

                // 수평 + 상방 별도 설정
                // linearVelocity.x = 수평 밀침
                // linearVelocity.y = 상방 힘 (튀어오르는 느낌)
                rb.linearVelocity = new Vector2(
                    horizontalSign * _shockwavePower,
                    _upwardForce);

                Debug.Log($"[TestBossShockwave] 밀침 → 수평:{horizontalSign * _shockwavePower:F1} 상방:{_upwardForce:F1}");

                // ★ v1.2: 코루틴으로 Unblock (WaitForSecondsRealtime — timeScale 무관)
                StartCoroutine(UnblockAfterRealtime(_blockDuration));
            }
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 실시간 duration 후 이동 입력 해제.
        /// WaitForSecondsRealtime → timeScale 영향 없음.
        /// </summary>
        private IEnumerator UnblockAfterRealtime(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);

            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockJump();
            InputManager.Instance?.UnblockDash();
        }

        /// <summary>
        /// 히트스탑 코루틴.
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