// ============================================================
// PlayerHealth.cs  v1.2
// 플레이어 체력 / 피격 처리 컴포넌트
//
// [v1.2 변경 — 넉백 타이밍 보장]
//
//   [기존 v1.1 문제]
//     히트스탑(Time.timeScale = 0.02) 중에
//     KnockbackRoutine 이 WaitForFixedUpdate() 로 대기.
//     → timeScale 이 낮으면 FixedUpdate 가 거의 실행 안 됨.
//     → 히트스탑 종료 후 timeScale = 1.0 복구된 직후
//       PlayerMover.ApplyMovement() 가 먼저 실행되어 velocity 덮어씀.
//     → 그 다음 프레임에 넉백 velocity 설정 → 순서 뒤집힘.
//     BlockJump() 누락으로 점프 입력 시 velocity.y 혼입.
//
//   [v1.2 수정]
//     KnockbackRoutine:
//       1. BlockMove / BlockDash / BlockJump 동시 차단 (Jump 추가)
//       2. 히트스탑이 진행 중이면 끝날 때까지 WaitForSecondsRealtime 대기
//          → timeScale 복구 이후에 velocity 설정 보장
//       3. velocity 설정 후 WaitForFixedUpdate 감속 루프
//       4. UnblockMove / UnblockDash / UnblockJump 해제
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
    /// 플레이어 체력 / 피격 처리 컴포넌트. (v1.2)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("── 체력 ──────────────────────")]
        [Tooltip("최대 체력. 권장: 5~10.")]
        [Min(1f)]
        [SerializeField] private float _maxHp = 5f;

        [Header("── 피격 반응 ──────────────────────")]
        [Tooltip("피격 무적 시간 (초). 권장: 0.5~1.0.")]
        [Range(0.1f, 3.0f)]
        [SerializeField] private float _iFrameDuration = 0.6f;

        [Tooltip("피격 플래시 깜빡임 간격. 권장: 0.07~0.12.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hitFlashInterval = 0.08f;

        [Header("── 넉백 ──────────────────────")]
        [Tooltip("넉백 초기 속도. 권장: 5~10.")]
        [Min(0f)]
        [SerializeField] private float _knockbackForce = 6f;

        [Tooltip("넉백 감속 비율. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] private float _knockbackDecay = 0.8f;

        [Tooltip("상방 넉백 비율. 0 = 수평만. 권장: 0.2~0.4.")]
        [Range(0f, 1f)]
        [SerializeField] private float _knockbackUpward = 0.3f;

        [Header("── 히트스탑 ──────────────────────")]
        [Tooltip("히트스탑 지속 시간 (실시간). 0 = 없음. 권장: 0.05~0.1.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _hitStopDuration = 0.07f;

        [Tooltip("히트스탑 TimeScale. 0 = 완전 정지. 권장: 0.0~0.05.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopTimeScale = 0.02f;

        // ──────────────────────────────────────────
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        private float _currentHp;
        private bool _isInvincible;
        private bool _isDead;

        private Coroutine _iFrameCoroutine;
        private Coroutine _knockbackCoroutine;
        private Coroutine _hitStopCoroutine;

        public event Action<DamageInfo> OnDamaged;
        public event Action OnDead;

        public bool IsDead => _isDead;
        public float CurrentHp => _currentHp;
        public float MaxHp => _maxHp;
        public float HpRatio => _maxHp > 0f ? _currentHp / _maxHp : 0f;
        public bool IsInvincible => _isInvincible;

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
            if (_hitStopCoroutine != null)
                Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════
        // IDamageable
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// 체력 감소 → 히트스탑 → 넉백 → iFrame → 피격 플래시.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isInvincible || _isDead) return;

            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);
            Debug.Log($"[PlayerHealth] 피격: -{info.Amount} / HP {_currentHp}/{_maxHp}");

            // 히트스탑
            if (_hitStopDuration > 0f)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine());
            }

            // 넉백 — 히트스탑 종료 후 velocity 설정을 보장하는 순서로 실행
            if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // iFrame
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(InvincibleRoutine());

            // 피격 플래시
            HitFeedback.EnemyHitPlayer(_spriteRenderer, transform, info.Direction);

            OnDamaged?.Invoke(info);

            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 히트스탑 코루틴.
        /// WaitForSecondsRealtime → timeScale 영향 없이 실시간 대기.
        /// </summary>
        private IEnumerator HitStopRoutine()
        {
            Time.timeScale = _hitStopTimeScale;
            yield return new WaitForSecondsRealtime(_hitStopDuration);
            if (Mathf.Approximately(Time.timeScale, _hitStopTimeScale))
                Time.timeScale = 1f;
            _hitStopCoroutine = null;
        }

        /// <summary>
        /// 넉백 코루틴. (v1.2)
        ///
        /// [v1.2 수정]
        ///   1. BlockJump() 추가 — 점프 입력으로 velocity.y 혼입 차단
        ///   2. 히트스탑 종료 대기 — timeScale 복구 후 velocity 설정 보장
        ///      WaitForSecondsRealtime 사용 → timeScale 영향 없음
        ///   3. velocity 설정 → WaitForFixedUpdate 감속 루프
        ///   4. UnblockJump() 추가
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_knockbackForce <= 0f) yield break;

            // ① 이동 입력 전부 차단 (Jump 포함) ← v1.2 BlockJump 추가
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockDash();
            InputManager.Instance?.BlockJump();

            // ② 히트스탑이 진행 중이면 끝날 때까지 대기 ← v1.2 핵심 수정
            //    WaitForSecondsRealtime: timeScale 영향 없이 실시간 대기
            //    이 대기 후 velocity 설정하면 PlayerMover 덮어쓰기 순서 역전 방지
            if (_hitStopDuration > 0f)
                yield return new WaitForSecondsRealtime(_hitStopDuration);

            // ③ 히트스탑 종료 직후 velocity 설정 — PlayerMover 보다 먼저 보장
            Vector2 horizontal = new Vector2(direction.x, 0f).normalized;
            Vector2 knockDir = Vector2.Lerp(horizontal, Vector2.up, _knockbackUpward).normalized;
            _rigid2D.linearVelocity = knockDir * _knockbackForce;

            // ④ 감속 루프
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

            // ⑤ 이동 입력 해제
            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockDash();
            InputManager.Instance?.UnblockJump();
        }

        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_iFrameDuration);
            _isInvincible = false;
        }

        // ══════════════════════════════════════════════════════
        // 사망
        // ══════════════════════════════════════════════════════

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

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
    }
}