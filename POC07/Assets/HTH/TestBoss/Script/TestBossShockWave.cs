// ============================================================
// TestBossShockwave.cs  v1.0
// 테스트 보스 전용 충격파 컴포넌트
//
// [역할]
//   딜타임 종료 시 보스 주변 플레이어를 강하게 밀쳐낸다.
//   데미지 없음. 순수 밀침(넉백) + DOTween 연출.
//
// [BossShockwave 와의 차이]
//   BossShockwave    : BossKnightDataSO 의존 (Initialize 필요)
//   TestBossShockwave: TestBossDataSO 의존, 자체 Initialize 없음
//                      DOTween 카메라 셰이크 연출 추가
//                      Y축 상방 힘 추가 (날아가는 느낌)
//
// [호출 흐름]
//   TestBossCore.ExitDilTime()
//     → TestBossShockwave.Trigger(origin)
//       → OverlapCircle 플레이어 감지
//       → Rigidbody2D.AddForce(dir + upward) 밀침
//       → DOTween 카메라 셰이크 (선택)
//       → 파티클 재생 (선택)
//
// [Prefab 연결]
//   TestBoss 루트에 부착.
//   TestBossCore._shockwave 에 연결.
//   _playerLayer  = Player 레이어       ★필수
//   _shockwaveRadius, _shockwavePower   Inspector 에서 설정
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
    /// 테스트 보스 전용 충격파 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [충격파 방향]
    ///   수평: 보스 → 플레이어 방향
    ///   수직: +Y 상방 가중치 추가
    ///   결과: 플레이어가 대각선으로 날아가는 느낌
    ///
    /// [연출 순서]
    ///   1. 파티클 재생 (선택)
    ///   2. 카메라 셰이크 (선택)
    ///   3. Rigidbody2D.AddForce 적용
    ///   4. 짧은 히트스탑 (선택)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossShockwave : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector — 수치
        // ──────────────────────────────────────────

        [Header("── 충격파 수치 ──────────────────────")]

        /// <summary>
        /// 충격파 감지 반경 (units).
        /// 이 범위 내 플레이어에게 밀침 적용.
        /// </summary>
        [Tooltip("충격파 감지 반경 (units). 권장: 6~12.")]
        [Min(0.5f)]
        [SerializeField] private float _shockwaveRadius = 8f;

        /// <summary>
        /// 충격파 밀침 강도 (Impulse).
        /// 플레이어 Rigidbody2D.AddForce 에 적용.
        /// </summary>
        [Tooltip("충격파 밀침 강도. 권장: 15~30.")]
        [Min(0f)]
        [SerializeField] private float _shockwavePower = 20f;

        /// <summary>
        /// 상방 힘 가중치 (0~1).
        /// 1.0 = 수직 방향만. 0.3~0.5 권장.
        /// 플레이어가 위로 튀어오르는 느낌.
        /// </summary>
        [Tooltip("상방 힘 가중치. 권장: 0.3~0.5.")]
        [Range(0f, 1f)]
        [SerializeField] private float _upwardBias = 0.4f;

        [Header("── 레이어 ──────────────────────")]

        /// <summary>
        /// 플레이어 감지 레이어.
        /// Player 레이어 선택.
        /// </summary>
        [Tooltip("플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("── 히트스탑 ──────────────────────")]

        /// <summary>
        /// 히트스탑 지속 시간 (초).
        /// 0이면 히트스탑 없음.
        /// Time.timeScale 을 일시적으로 0에 가깝게 낮춤.
        /// </summary>
        [Tooltip("히트스탑 지속 시간 (초). 0 = 없음. 권장: 0.05~0.12.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hitStopDuration = 0.08f;

        /// <summary>
        /// 히트스탑 중 TimeScale.
        /// 0에 가까울수록 완전 정지 느낌.
        /// </summary>
        [Tooltip("히트스탑 TimeScale. 권장: 0.0~0.05.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopTimeScale = 0.02f;

        [Header("── 카메라 셰이크 (선택) ──────────────────────")]

        /// <summary>
        /// 셰이크 대상 카메라 Transform.
        /// 미연결 시 Camera.main 자동 탐색.
        /// </summary>
        [Tooltip("셰이크 카메라 Transform. 미연결 시 Camera.main 자동 탐색.")]
        [SerializeField] private Transform _cameraTransform;

        /// <summary>
        /// 카메라 셰이크 강도.
        /// 0이면 셰이크 없음.
        /// </summary>
        [Tooltip("카메라 셰이크 강도. 권장: 0.2~0.5. 0 = 없음.")]
        [Range(0f, 1f)]
        [SerializeField] private float _cameraShakeStrength = 0.3f;

        /// <summary>
        /// 카메라 셰이크 지속 시간 (초).
        /// </summary>
        [Tooltip("카메라 셰이크 지속 시간 (초). 권장: 0.2~0.4.")]
        [Range(0f, 1f)]
        [SerializeField] private float _cameraShakeDuration = 0.3f;

        [Header("── 이펙트 (선택) ──────────────────────")]

        /// <summary>
        /// 충격파 파티클.
        /// 발동 시 재생.
        /// </summary>
        [Tooltip("충격파 파티클. 미연결 시 생략.")]
        [SerializeField] private ParticleSystem _shockwaveEffect;

        // ──────────────────────────────────────────
        // 내부
        // ──────────────────────────────────────────

        private readonly Collider2D[] _overlapBuffer = new Collider2D[8];
        private Coroutine _hitStopCoroutine;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // 카메라 자동 탐색
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        // ══════════════════════════════════════════════════════
        // 충격파 발동 (TestBossCore 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 발동.
        /// TestBossCore.ExitDilTime() 에서 호출.
        ///
        /// [처리 순서]
        ///   1. 파티클 재생
        ///   2. 카메라 셰이크
        ///   3. 히트스탑
        ///   4. 플레이어 밀침 (대각선 날아가기)
        /// </summary>
        /// <param name="origin">충격파 발생 위치 (보스 위치).</param>
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
                origin,
                _shockwaveRadius,
                _overlapBuffer,
                _playerLayer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null) continue;

                if (!col.TryGetComponent<Rigidbody2D>(out var rb)) continue;

                // 수평 방향: 보스 → 플레이어
                Vector2 horizontal = ((Vector2)col.transform.position
                    - (Vector2)origin).normalized;

                // 상방 혼합: 수평 + 상방 bias
                // ★ Lerp 후 normalized 로 단위 벡터 보장
                Vector2 finalDir = Vector2.Lerp(horizontal, Vector2.up, _upwardBias).normalized;

                // ★ AddForce 대신 linearVelocity 직접 설정
                //   PlayerMover 가 매 FixedUpdate velocity.x 를 덮어쓰므로
                //   AddForce 는 즉시 무효화됨.
                //   velocity 직접 설정 후 InputManager 이동 입력 일시 차단으로 보장.
                rb.linearVelocity = finalDir * _shockwavePower;

                // 플레이어 이동 입력 일시 차단 (충격파 날아가는 동안 이동 불가)
                if (InputManager.Instance != null)
                    StartCoroutine(BlockPlayerMoveRoutine(_hitStopDuration + 0.3f));

                Debug.Log($"[TestBossShockwave] 플레이어 밀침 → 방향:{finalDir}" +
                          $" 강도:{_shockwavePower}");
            }
        }

        // ══════════════════════════════════════════════════════
        // 히트스탑
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 이동 입력 일시 차단 코루틴.
        /// 충격파로 날아가는 동안 이동 키 입력이 velocity 를 덮어쓰지 않도록.
        /// </summary>
        private IEnumerator BlockPlayerMoveRoutine(float duration)
        {
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockJump();
            InputManager.Instance?.BlockDash();

            yield return new WaitForSeconds(duration);

            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockJump();
            InputManager.Instance?.UnblockDash();
        }

        /// <summary>
        /// 히트스탑 코루틴.
        /// Time.timeScale 을 일시적으로 낮춰 정지감 연출.
        /// 실시간(_hitStopDuration) 후 복구.
        /// </summary>
        private IEnumerator HitStopRoutine()
        {
            float original = Time.timeScale;
            Time.timeScale = _hitStopTimeScale;

            // WaitForSecondsRealtime: timeScale 영향 없음
            yield return new WaitForSecondsRealtime(_hitStopDuration);

            Time.timeScale = original;
            _hitStopCoroutine = null;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _shockwaveRadius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.down * 1f,
                $"Shockwave  R:{_shockwaveRadius}  P:{_shockwavePower}  " +
                $"Up:{_upwardBias}");
#endif
        }
    }
}