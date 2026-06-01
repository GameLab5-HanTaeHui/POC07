// ============================================================
// EnemyBase.cs  v2.0
// 적 베이스 클래스 — 리모델링
//
// [v2.0 리모델링 변경]
//
//   ① TakeDamage virtual 로 변경
//       기존: public void TakeDamage (new 키워드 방식)
//       변경: public virtual void TakeDamage (override 방식)
//       이유: IDamageable 참조로 호출 시 EnemyKnight.TakeDamage() 가
//             실행되지 않는 버그 (C# 인터페이스 + new 키워드 문제) 수정.
//             PlayerWeaponHitboxManager 에서 IDamageable.TakeDamage() 호출 시
//             반드시 하위 클래스의 override 가 실행되어야 함.
//
//   ② 사망 처리 추가
//       기존: HP 최솟값 1 고정 (사망 없음).
//       변경: HP 0 이하 시 Die() 호출 → OnDead 이벤트 발행.
//             단, 하위 클래스(EnemyKnight)가 Lock 해제 여부를 판단해
//             override TakeDamage 에서 조건 충족 시 base.TakeDamage() 호출.
//             더미 타입은 OnDead 에 아무것도 연결하지 않으면 기존 동작 유지.
//
//   ③ Settings 프로퍼티 유지
//       EnemyAI / EnemySensor / ChargeAttack 이 이 프로퍼티로 DataSO 참조.
//
// [v1.2 변경]
//   Settings 프로퍼티 추가.
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
    /// 적 베이스 추상 클래스. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 호출 흐름]
    ///   PlayerWeaponHitboxManager.CheckHit()
    ///     → col.TryGetComponent&lt;IDamageable&gt;()
    ///       → IDamageable.TakeDamage(info)
    ///         → (virtual) EnemyKnight.TakeDamage(info) ← override 실행
    ///           → Lock 미해제 시 방패 차단 or 자물쇠 전달
    ///           → Lock 전부 해제 시 base.TakeDamage(info) 호출
    ///             → EnemyBase.TakeDamage(info) ← 체력 감소 + 사망 처리
    ///
    /// [DataSO 참조 구조]
    ///   EnemyBase._settings    : Inspector 연결 (유일한 연결 지점)
    ///   EnemyBase.Settings     : public 프로퍼티
    ///   EnemyAI.Awake()        : GetComponent&lt;EnemyBase&gt;().Settings 취득
    ///   EnemySensor            : EnemyAI 가 SetData() 주입
    ///   EnemyKnightChargeAttack: EnemyAI 가 SetData() 주입
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary>
        /// 적 수치 설정 SO.
        /// ★ Inspector 연결 지점은 이 필드 하나뿐.
        /// EnemyAI / EnemySensor / ChargeAttack 은
        /// Settings 프로퍼티로 참조.
        /// </summary>
        [Tooltip("EnemyDataSO. 필수 연결. 이 컴포넌트에만 연결.")]
        [SerializeField] protected EnemyDataSO _settings;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        protected Rigidbody2D _rigid2D;
        protected SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. </summary>
        protected float _currentHp;

        private bool _isInvincible;
        private bool _isDead;

        private Coroutine _iFrameCoroutine;
        private Coroutine _hitFlashCoroutine;
        private Coroutine _knockbackCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 사망 시 1회 발행.
        /// GameManager 구독 → 씬 전환 / 리스폰 처리.
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
        public float MaxHp => _settings != null ? _settings.maxHp : 1f;

        /// <summary> 체력 비율 (0~1). UI 체력바용. </summary>
        public virtual float HpRatio => MaxHp > 0f ? _currentHp / MaxHp : 0f;

        /// <summary> 현재 무적 여부. </summary>
        public bool IsInvincible => _isInvincible;

        /// <summary>
        /// DataSO 외부 참조 프로퍼티.
        /// EnemyAI / EnemySensor / ChargeAttack 에서
        /// GetComponent&lt;EnemyBase&gt;().Settings 로 취득.
        /// </summary>
        public EnemyDataSO Settings => _settings;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected virtual void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_settings == null)
            {
                Debug.LogError($"[{GetType().Name}] EnemyDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            _currentHp = _settings.maxHp;
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현 — virtual
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리. (virtual — EnemyKnight 에서 override)
        ///
        /// [중요 — virtual 선언 이유]
        ///   IDamageable 참조로 TakeDamage() 를 호출할 때
        ///   C# 은 참조 타입의 실제 구현을 실행한다.
        ///   단, 이는 virtual + override 쌍에서만 보장됨.
        ///   'new' 키워드는 인터페이스 참조에서 무시되므로
        ///   EnemyBase 를 virtual, EnemyKnight 를 override 로 선언해야 함.
        ///
        /// [흐름]
        ///   직접 호출 시    : EnemyBase.TakeDamage() 실행 (체력 감소)
        ///   EnemyKnight 경우: EnemyKnight.TakeDamage() 실행 (방패 판단 후 base 호출)
        /// </summary>
        public virtual void TakeDamage(DamageInfo info)
        {
            if (_isInvincible || _isDead) return;

            // ① 체력 감소
            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);

            // ② 넉백
            if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // ③ iFrame
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(InvincibleRoutine());

            // ④ 피격 플래시
            if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
            HitFeedback.PlayerHitEnemy(_spriteRenderer, transform, info.Direction);

            // ⑤ 하위 클래스 확장점
            OnDamaged(info);

            Debug.Log($"[{GetType().Name}] 피격: -{info.Amount:F0} / HP {_currentHp:F0}/{MaxHp:F0}");

            // ⑥ 사망 체크
            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 사망 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 사망 처리.
        /// OnDead 이벤트 발행 → GameManager 에서 처리.
        /// EnemyAI disabled 처리는 외부(GameManager or 구독자)에서 담당.
        /// </summary>
        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;

            StopAllCoroutines();
            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

            Debug.Log($"[{GetType().Name}] 사망!");
            OnDead?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 넉백 코루틴.
        /// velocity.x 를 direction.x * knockbackForce 로 설정 후 감속.
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_settings == null || _settings.knockbackForce <= 0f) yield break;

            _rigid2D.linearVelocity = new Vector2(
                direction.x * _settings.knockbackForce,
                _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            float maxTime = 0.5f;
            float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float vx = _rigid2D.linearVelocity.x * _settings.knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(vx, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(vx) < threshold) break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_settings.iFrameDuration);
            _isInvincible = false;
        }

        // ══════════════════════════════════════════════════════
        // 가상 메서드 — 하위 클래스 확장점
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TakeDamage 처리 후 추가 로직.
        /// 하위 클래스에서 필요 시 override.
        /// </summary>
        protected virtual void OnDamaged(DamageInfo info) { }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 체력 + 상태 리셋. 테스트 / 리스폰 시 호출.
        /// </summary>
        public virtual void ResetEnemy()
        {
            _currentHp = _settings != null ? _settings.maxHp : 1f;
            _isInvincible = false;
            _isDead = false;

            StopAllCoroutines();

            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        protected virtual void OnDrawGizmosSelected()
        {
            if (_settings == null) return;
#if UNITY_EDITOR
            UnityEditor.Handles.color = _isDead ? Color.gray : Color.green;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.8f,
                $"HP {_currentHp:F0}/{MaxHp:F0}  iFrame:{_isInvincible}  Dead:{_isDead}");
#endif
        }
    }
}