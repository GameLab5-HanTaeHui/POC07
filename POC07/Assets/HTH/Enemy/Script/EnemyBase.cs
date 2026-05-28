// ============================================================
// EnemyBase.cs  v1.2
// 적 베이스 클래스
//
// [v1.2 변경 — DataSO 단일 연결 지점 확립]
//   Settings 프로퍼티 추가 (public).
//   EnemyAI / EnemySensor / EnemyKnightAttack 이
//   모두 이 프로퍼티를 통해 DataSO 를 참조.
//   → Inspector 에서 DataSO 를 꽂는 곳은 EnemyBase 하나뿐.
//
// [v1.1 변경]
//   넉백 방식 AddForce → KnockbackRoutine 코루틴으로 교체.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 베이스 추상 클래스. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [DataSO 참조 구조 — v1.2]
    ///   EnemyBase._settings    : Inspector 연결 (유일한 연결 지점)
    ///   EnemyBase.Settings     : public 프로퍼티 → 외부 참조용
    ///   EnemyAI.Awake()        : GetComponent<EnemyBase>().Settings 로 취득
    ///   EnemySensor.SetData()  : EnemyAI 가 Awake 에서 호출
    ///   EnemyKnightAttack      : EnemyAI 가 Start 에서 SetData() 호출
    ///
    /// [기존 EnemyAI Inspector 연결 제거]
    ///   EnemyAI 의 _settings [SerializeField] 슬롯 삭제.
    ///   Inspector 에서 EnemyAI 의 DataSO 슬롯은 더 이상 노출되지 않음.
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
        /// 적 수치 설정 ScriptableObject.
        /// ★ Inspector 연결 지점은 이 필드 하나뿐.
        /// EnemyAI / EnemySensor / EnemyKnightAttack 은
        /// EnemyBase.Settings 프로퍼티를 통해 참조.
        /// </summary>
        [Tooltip("EnemyDataSO. 필수 연결. 이 컴포넌트에만 연결하면 됩니다.")]
        [SerializeField] protected EnemyDataSO _settings;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        protected Rigidbody2D _rigid2D;
        protected SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. 최솟값 1 고정 (사망 없음). </summary>
        protected float _currentHp;

        /// <summary> 피격 무적 플래그. </summary>
        private bool _isInvincible;

        /// <summary> 넉백 처리 중 플래그. </summary>
        private bool _isKnockedBack;

        private Coroutine _iFrameCoroutine;
        private Coroutine _hitFlashCoroutine;
        private Coroutine _knockbackCoroutine;

        // ──────────────────────────────────────────
        // IDamageable
        // ──────────────────────────────────────────

        /// <summary> 더미는 사망하지 않으므로 항상 false. </summary>
        public bool IsDead => false;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. </summary>
        public float CurrentHp => _currentHp;

        /// <summary> 최대 체력. </summary>
        public float MaxHp => _settings != null ? _settings.maxHp : 1f;

        /// <summary> 현재 무적 여부. </summary>
        public bool IsInvincible => _isInvincible;

        /// <summary> 체력 비율 (0~1). UI 체력바용. </summary>
        public float HpRatio => MaxHp > 0f ? _currentHp / MaxHp : 0f;

        /// <summary>
        /// DataSO 외부 참조 프로퍼티. (v1.2 추가)
        /// EnemyAI / EnemySensor / EnemyKnightAttack 에서
        /// GetComponent<EnemyBase>().Settings 로 취득.
        /// null 체크 없이 사용 시 Awake 이전 접근 주의.
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
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// iFrame 중이면 무시. 체력 감소 → 넉백 → iFrame 시작.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isInvincible) return;

            // ① 체력 감소 (최솟값 1 — 사망 없음)
            _currentHp = Mathf.Max(1f, _currentHp - info.Amount);

            // ② 넉백 코루틴
            if (_knockbackCoroutine != null)
                StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // ③ iFrame 코루틴
            if (_iFrameCoroutine != null)
                StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(InvincibleRoutine());

            // ④ 피격 플래시
            if (_hitFlashCoroutine != null)
                StopCoroutine(_hitFlashCoroutine);
            _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());

            // ⑤ 하위 클래스 확장점
            OnDamaged(info);

            Debug.Log($"[{GetType().Name}] 피격: -{info.Amount} / HP {_currentHp}/{MaxHp}");
        }

        // ══════════════════════════════════════════════════════
        // 넉백 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 넉백 코루틴.
        /// velocity 를 direction * knockbackForce 로 설정 후
        /// knockbackDecay 비율로 매 FixedUpdate 감속.
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_settings.knockbackForce <= 0f) yield break;

            _isKnockedBack = true;

            float velocityX = direction.x * _settings.knockbackForce;
            _rigid2D.linearVelocity = new Vector2(velocityX, _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            float maxTime = 0.5f;
            float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float decayedX = _rigid2D.linearVelocity.x * _settings.knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(decayedX, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(_rigid2D.linearVelocity.x) < threshold)
                    break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
            _isKnockedBack = false;
        }

        // ══════════════════════════════════════════════════════
        // iFrame / 플래시 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_settings.iFrameDuration);
            _isInvincible = false;
        }

        private IEnumerator HitFlashRoutine()
        {
            float elapsed = 0f;
            float duration = _settings.iFrameDuration;
            float interval = _settings.hitFlashInterval;

            while (elapsed < duration)
            {
                _spriteRenderer.color = Color.red;
                yield return new WaitForSeconds(interval);
                _spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(interval);
                elapsed += interval * 2f;
            }

            _spriteRenderer.color = Color.white;
        }

        // ══════════════════════════════════════════════════════
        // 가상 메서드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TakeDamage 처리 후 호출되는 확장점.
        /// 하위 클래스에서 추가 처리.
        /// </summary>
        protected virtual void OnDamaged(DamageInfo info) { }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 체력을 최대치로 리셋하고 velocity 를 초기화.
        /// </summary>
        public void ResetHp()
        {
            _currentHp = _settings.maxHp;
            _isInvincible = false;
            _isKnockedBack = false;

            if (_rigid2D != null)
                _rigid2D.linearVelocity = Vector2.zero;

            _spriteRenderer.color = Color.white;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        protected virtual void OnDrawGizmosSelected()
        {
            if (_settings == null) return;
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"HP: {_currentHp:F0}/{MaxHp:F0}  iFrame:{_isInvincible}  KB:{_isKnockedBack}");
#endif
        }
    }
}