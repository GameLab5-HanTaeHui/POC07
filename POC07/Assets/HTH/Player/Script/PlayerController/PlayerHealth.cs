// ============================================================
// PlayerHealth.cs  v1.1
// 플레이어 체력 / 피격 처리 컴포넌트
//
// [v1.1 변경]
//   히트스탑(HitStop) 추가.
//     적 공격 명중 순간 Time.timeScale 을 일시적으로 낮춰
//     타격감 + 정지감 연출.
//     _hitStopDuration  : 히트스탑 지속 시간 (실시간 초)
//     _hitStopTimeScale : 히트스탑 중 TimeScale
//   넉백 수직(Y) 성분 추가.
//     보스 패턴 피격 시 위로 살짝 튀는 느낌.
//     _knockbackUpward : 상방 힘 비율 (0 = 수평만)
//
// [역할]
//   IDamageable 구현.
//   체력 감소 → 히트스탑 → 넉백 → iFrame → 피격 플래시 → 사망.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 플레이어 체력 / 피격 처리 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [히트스탑 구조]
    ///   TakeDamage() 호출
    ///     → StartCoroutine(HitStopRoutine())
    ///     → Time.timeScale = _hitStopTimeScale
    ///     → WaitForSecondsRealtime(_hitStopDuration)
    ///     → Time.timeScale = 1.0 복구
    ///
    /// [넉백 구조]
    ///   direction 으로 수평 + 상방 혼합 벡터 계산
    ///   → Rigidbody2D.velocity 직접 설정
    ///   → 매 FixedUpdate 감속
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // 싱글턴
        // ──────────────────────────────────────────

        /// <summary> 전역 단일 인스턴스. </summary>
        public static PlayerHealth Instance { get; private set; }

        // ──────────────────────────────────────────
        // Inspector — 체력
        // ──────────────────────────────────────────

        [Header("── 체력 ──────────────────────")]

        /// <summary> 최대 체력. </summary>
        [Tooltip("최대 체력. 권장: 5~10.")]
        [Min(1f)]
        [SerializeField] private float _maxHp = 5f;

        // ──────────────────────────────────────────
        // Inspector — 피격 반응
        // ──────────────────────────────────────────

        [Header("── 피격 반응 ──────────────────────")]

        /// <summary>
        /// 피격 무적 시간 (초).
        /// </summary>
        [Tooltip("피격 무적 시간 (초). 권장: 0.5~1.0.")]
        [Range(0.1f, 3.0f)]
        [SerializeField] private float _iFrameDuration = 0.6f;

        /// <summary>
        /// 피격 플래시 깜빡임 간격 (초).
        /// </summary>
        [Tooltip("피격 플래시 깜빡임 간격. 권장: 0.07~0.12.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hitFlashInterval = 0.08f;

        // ──────────────────────────────────────────
        // Inspector — 넉백
        // ──────────────────────────────────────────

        [Header("── 넉백 ──────────────────────")]

        /// <summary>
        /// 넉백 초기 속도 (units/s).
        /// </summary>
        [Tooltip("넉백 초기 속도. 권장: 5~10.")]
        [Min(0f)]
        [SerializeField] private float _knockbackForce = 6f;

        /// <summary>
        /// 넉백 감속 비율. 매 FixedUpdate 마다 velocity.x 에 곱함.
        /// </summary>
        [Tooltip("넉백 감속 비율. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] private float _knockbackDecay = 0.8f;

        /// <summary>
        /// 상방 넉백 비율 (0~1).
        /// 0 = 수평만. 0.3~0.5 = 위로 살짝 튀는 느낌.
        /// 보스 패턴 피격 시 대각선으로 밀려나는 연출.
        /// </summary>
        [Tooltip("상방 넉백 비율. 0 = 수평만. 권장: 0.2~0.4.")]
        [Range(0f, 1f)]
        [SerializeField] private float _knockbackUpward = 0.3f;

        // ──────────────────────────────────────────
        // Inspector — 히트스탑 ★ v1.1 추가
        // ──────────────────────────────────────────

        [Header("── 히트스탑 ★ ──────────────────────")]

        /// <summary>
        /// 히트스탑 지속 시간 (실시간 초).
        /// 피격 순간 Time.timeScale 을 낮춰 정지감 연출.
        /// 0 = 히트스탑 없음.
        /// WaitForSecondsRealtime 사용 → timeScale 영향 없음.
        /// </summary>
        [Tooltip("히트스탑 지속 시간 (실시간). 0 = 없음. 권장: 0.05~0.1.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hitStopDuration = 0.07f;

        /// <summary>
        /// 히트스탑 중 Time.timeScale.
        /// 0에 가까울수록 완전 정지 느낌.
        /// 권장: 0.0~0.05.
        /// </summary>
        [Tooltip("히트스탑 TimeScale. 0 = 완전 정지. 권장: 0.0~0.05.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopTimeScale = 0.02f;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private float _currentHp;
        private bool _isInvincible;
        private bool _isDead;

        private Coroutine _iFrameCoroutine;
        private Coroutine _hitFlashCoroutine;
        private Coroutine _knockbackCoroutine;
        private Coroutine _hitStopCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 피격 시 발행. UI 체력 감소 애니메이션 등에서 구독.
        /// </summary>
        public event Action<DamageInfo> OnDamaged;

        /// <summary>
        /// 사망 시 1회 발행. GameManager 에서 구독.
        /// </summary>
        public event Action OnDead;

        // ──────────────────────────────────────────
        // IDamageable
        // ──────────────────────────────────────────

        /// <summary> 현재 사망 여부. </summary>
        public bool IsDead => _isDead;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. </summary>
        public float CurrentHp => _currentHp;

        /// <summary> 최대 체력. </summary>
        public float MaxHp => _maxHp;

        /// <summary> 체력 비율 (0~1). UI 체력바용. </summary>
        public float HpRatio => _maxHp > 0f ? _currentHp / _maxHp : 0f;

        /// <summary> 현재 무적 여부. </summary>
        public bool IsInvincible => _isInvincible;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _currentHp = _maxHp;
        }

        private void OnDestroy()
        {
            // 히트스탑 중 파괴되면 TimeScale 복구
            if (_hitStopCoroutine != null)
                Time.timeScale = 1f;

            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// iFrame 중이거나 이미 사망이면 무시.
        ///
        /// [처리 순서 — v1.1]
        ///   1. iFrame / 사망 체크
        ///   2. 체력 감소
        ///   3. 히트스탑 ★ (순간 정지감)
        ///   4. 넉백 (수평 + 상방 혼합)
        ///   5. iFrame 시작
        ///   6. 피격 플래시
        ///   7. OnDamaged 이벤트 발행
        ///   8. 사망 체크
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isInvincible || _isDead) return;

            // ① 체력 감소
            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);

            Debug.Log($"[PlayerHealth] 피격: -{info.Amount} / HP {_currentHp}/{_maxHp}");

            // ② 히트스탑 ★
            if (_hitStopDuration > 0f)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine());
            }

            // ③ 넉백
            if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // ④ iFrame
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(InvincibleRoutine());

            // ⑤ 피격 플래시
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            HitFeedback.EnemyHitPlayer(_spriteRenderer, transform, info.Direction);

            // ⑥ 이벤트 발행
            OnDamaged?.Invoke(info);

            // ⑦ 사망 체크
            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 히트스탑 코루틴. ★ v1.1
        /// Time.timeScale 을 일시적으로 낮춰 피격 정지감 연출.
        /// WaitForSecondsRealtime: timeScale 영향 없이 실시간 대기.
        /// </summary>
        private IEnumerator HitStopRoutine()
        {
            Time.timeScale = _hitStopTimeScale;

            yield return new WaitForSecondsRealtime(_hitStopDuration);

            // 복구 (다른 히트스탑과 충돌 방지)
            if (Mathf.Approximately(Time.timeScale, _hitStopTimeScale))
                Time.timeScale = 1f;

            _hitStopCoroutine = null;
        }

        /// <summary>
        /// 넉백 코루틴. ★ v1.1 상방 성분 추가.
        /// 수평 방향 + _knockbackUpward 비율의 상방 힘 혼합.
        /// → 보스 패턴 피격 시 대각선으로 밀려나는 느낌.
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_knockbackForce <= 0f) yield break;

            // ★ 이동 입력 차단 — PlayerMover.ApplyMovement()가 매 FixedUpdate
            //   velocity.x 를 덮어쓰므로 차단하지 않으면 넉백이 즉시 무효화됨
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockDash();

            // 수평 방향 + 상방 혼합
            Vector2 horizontal = new Vector2(direction.x, 0f).normalized;
            Vector2 knockDir = Vector2.Lerp(horizontal, Vector2.up, _knockbackUpward).normalized;

            _rigid2D.linearVelocity = knockDir * _knockbackForce;

            float elapsed = 0f;
            float maxTime = 0.5f;
            float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float vx = _rigid2D.linearVelocity.x * _knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(vx, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(vx) < threshold) break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);

            // ★ 이동 입력 해제
            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockDash();
        }

        /// <summary>
        /// 피격 무적 코루틴.
        /// _iFrameDuration 동안 _isInvincible = true 유지.
        /// </summary>
        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_iFrameDuration);
            _isInvincible = false;
        }

        // ══════════════════════════════════════════════════════
        // 사망
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 사망 처리.
        /// </summary>
        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 히트스탑 즉시 종료
            if (_hitStopCoroutine != null)
            {
                StopCoroutine(_hitStopCoroutine);
                Time.timeScale = 1f;
            }

            StopAllCoroutines();
            _spriteRenderer.color = Color.white;

            Debug.Log("[PlayerHealth] 플레이어 사망!");
            OnDead?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 체력 + 상태 리셋. 리스폰 / 테스트 시 호출.
        /// </summary>
        public void ResetHealth()
        {
            // 히트스탑 즉시 종료
            if (_hitStopCoroutine != null)
                Time.timeScale = 1f;

            _currentHp = _maxHp;
            _isDead = false;
            _isInvincible = false;

            StopAllCoroutines();

            _iFrameCoroutine = null;
            _hitFlashCoroutine = null;
            _knockbackCoroutine = null;
            _hitStopCoroutine = null;

            if (_rigid2D != null)
                _rigid2D.linearVelocity = Vector2.zero;

            _spriteRenderer.color = Color.white;

            Debug.Log("[PlayerHealth] 체력 리셋.");
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = _isDead ? Color.gray : Color.green;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.8f,
                $"HP {_currentHp:F0}/{_maxHp:F0}  iFrame:{_isInvincible}" +
                $"  Dead:{_isDead}  HitStop:{_hitStopCoroutine != null}");
#endif
        }
    }
}