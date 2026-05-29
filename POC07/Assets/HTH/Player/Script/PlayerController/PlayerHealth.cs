// ============================================================
// PlayerHealth.cs  v1.0
// 플레이어 체력 / 피격 처리 컴포넌트
//
// [역할]
//   IDamageable 구현.
//   EnemyKnightAttack 등 적 공격의 TakeDamage() 수신 대상.
//   체력 감소 → iFrame → 피격 플래시 → 넉백 → 사망 처리.
//
// [부착 위치]
//   Player 루트 오브젝트 (EnemyKnightAttack 의 playerLayer 와 동일 레이어).
//
// [피격 연결 경로]
//   EnemyKnightAttack.CheckHit()
//     → ContactFilter2D(playerLayer) 로 Player 콜라이더 감지
//       → TryGetComponent<IDamageable>()
//         → PlayerHealth.TakeDamage(info)
//
// [레이어 설정]
//   Player 오브젝트 Layer = Player
//   EnemyDataSO.attackHitLayer = Player 레이어
//   Physics 2D Matrix: EnemyAttackHit ↔ Player 충돌 ON
//
// [넉백 처리]
//   EnemyBase 의 KnockbackRoutine 과 동일 방식.
//   PlayerMover 의 이동 velocity 를 직접 덮어씀.
//   Rigidbody2D 로 X 방향 강제 속도 부여 후 감속.
//
// [사망 처리]
//   _currentHp <= 0 시 OnDead 이벤트 발행.
//   실제 씬 전환 / 리스폰은 GameManager 에서 구독하여 처리.
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
    /// 플레이어 체력 / 피격 처리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [외부 읽기]
    ///   float hp    = PlayerHealth.Instance.CurrentHp;
    ///   float ratio = PlayerHealth.Instance.HpRatio;
    ///   bool  dead  = PlayerHealth.Instance.IsDead;
    ///
    /// [이벤트 구독]
    ///   PlayerHealth.Instance.OnDamaged += HandleDamaged;
    ///   PlayerHealth.Instance.OnDead    += HandleDead;
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
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 체력 ──────────────────────")]

        /// <summary> 최대 체력. </summary>
        [Tooltip("최대 체력. 권장: 5~10 (히트 기반 게임).")]
        [Min(1f)]
        [SerializeField] private float _maxHp = 5f;

        [Header("── 피격 반응 ──────────────────────")]

        /// <summary>
        /// 피격 무적 시간 (초).
        /// 이 시간 동안 추가 피격 무시.
        /// </summary>
        [Tooltip("피격 무적 시간 (초). 권장: 0.5~1.0.")]
        [Range(0.1f, 3.0f)]
        [SerializeField] private float _iFrameDuration = 0.6f;

        /// <summary> 피격 플래시 깜빡임 간격 (초). </summary>
        [Tooltip("피격 플래시 깜빡임 간격. 권장: 0.07~0.12.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hitFlashInterval = 0.08f;

        /// <summary>
        /// 넉백 초기 속도.
        /// 적 공격 방향 × 이 값으로 velocity 설정.
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

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 피격 시 발행.
        /// 파라미터: 수신한 DamageInfo.
        /// UI 체력 감소 애니메이션 등에서 구독.
        /// </summary>
        public event Action<DamageInfo> OnDamaged;

        /// <summary>
        /// 사망 시 1회 발행.
        /// GameManager 에서 구독하여 씬 전환 / 리스폰 처리.
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
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// iFrame 중이거나 이미 사망이면 무시.
        ///
        /// [처리 순서]
        ///   1. iFrame / 사망 체크
        ///   2. 체력 감소
        ///   3. 넉백 코루틴 시작
        ///   4. iFrame 코루틴 시작
        ///   5. 피격 플래시 코루틴 시작
        ///   6. OnDamaged 이벤트 발행
        ///   7. 체력 <= 0 시 Die()
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isInvincible || _isDead) return;

            // ① 체력 감소
            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);

            Debug.Log($"[PlayerHealth] 피격: -{info.Amount} / HP {_currentHp}/{_maxHp}");

            // ② 넉백
            if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // ③ iFrame
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(InvincibleRoutine());

            // ④ 피격 플래시
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            HitFeedback.EnemyHitPlayer(_spriteRenderer, transform, info.Direction);

            // ⑤ 이벤트 발행
            OnDamaged?.Invoke(info);

            // ⑥ 사망 체크
            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 넉백 코루틴.
        /// EnemyBase.KnockbackRoutine 과 동일 방식.
        /// velocity.x 를 방향 × knockbackForce 로 설정 후 감속.
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_knockbackForce <= 0f) yield break;

            float velocityX = direction.x * _knockbackForce;
            _rigid2D.linearVelocity = new Vector2(velocityX, _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            float maxTime = 0.4f;
            float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float decayedX = _rigid2D.linearVelocity.x * _knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(decayedX, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(_rigid2D.linearVelocity.x) < threshold)
                    break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
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
        /// OnDead 이벤트 발행 → GameManager 에서 리스폰 처리.
        /// 현재는 이벤트 발행만. 실제 비활성화 / 씬 전환은 외부 처리.
        /// </summary>
        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            // 모든 코루틴 정지
            StopAllCoroutines();
            _spriteRenderer.color = Color.white;

            Debug.Log("[PlayerHealth] 플레이어 사망!");
            OnDead?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 체력을 최대치로 복원 후 상태 초기화.
        /// 리스폰 / 테스트 리셋 시 호출.
        /// </summary>
        public void ResetHealth()
        {
            _currentHp = _maxHp;
            _isDead = false;
            _isInvincible = false;

            StopAllCoroutines();
            _iFrameCoroutine = null;
            _hitFlashCoroutine = null;
            _knockbackCoroutine = null;

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
                transform.position + Vector3.up * 2.2f,
                $"HP: {_currentHp:F0}/{_maxHp:F0}  iFrame:{_isInvincible}");
#endif
        }
    }
}