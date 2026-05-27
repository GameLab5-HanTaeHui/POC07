// ============================================================
// EnemyBase.cs  v1.1
// 적 베이스 클래스
//
// [v1.1 변경 — 넉백 방식 전면 교체]
//   문제:
//     AddForce(Impulse) 방식은 gravityScale = 0 에서
//     마찰/감속이 없어 velocity 가 누적되어 계속 날아감.
//     gravityScale = 1 로 변경해도 중력까지 더해져 더 심해짐.
//   해결:
//     넉백을 코루틴(KnockbackRoutine) 으로 교체.
//     velocity 를 직접 설정 후 _knockbackDecay 비율로 매 프레임 감속.
//     넉백 종료 후 velocity = zero 로 완전 정지 보장.
//     더미는 gravityScale=0 + FreezePositionY 로 Y 축 완전 고정.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 베이스 추상 클래스. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [넉백 동작]
    ///   1. velocity = direction * knockbackForce 로 초기 속도 설정
    ///   2. 매 FixedUpdate 마다 velocity *= knockbackDecay 로 감속
    ///   3. velocity.magnitude 가 0.1 이하 or 시간 초과 시 완전 정지
    ///   → 짧게 밀렸다가 멈추는 자연스러운 넉백
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
        /// </summary>
        [Tooltip("EnemyDataSO. 필수 연결.")]
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

            // ② 넉백 코루틴 (이전 넉백 있으면 즉시 중단 후 재시작)
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
        ///
        /// [동작 방식]
        ///   velocity 를 direction * knockbackForce 로 직접 설정.
        ///   매 FixedUpdate(WaitForFixedUpdate) 마다
        ///   velocity.x *= knockbackDecay 로 감속.
        ///   속도가 threshold 이하 or 최대 시간 초과 시 velocity = zero 로 완전 정지.
        ///
        /// [gravityScale = 0 에서 정상 동작하는 이유]
        ///   AddForce 는 물리 엔진 내부 적분에 의존하므로
        ///   gravityScale = 0 + FreezePositionY 환경에서 감속이 없음.
        ///   velocity 를 직접 제어하면 gravity / drag 설정에 무관하게 동작.
        /// </summary>
        /// <param name="direction">넉백 방향 (정규화된 벡터)</param>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_settings.knockbackForce <= 0f) yield break;

            _isKnockedBack = true;

            // X 방향만 넉백 (Y 는 더미에서 FreezePositionY 로 고정)
            float velocityX = direction.x * _settings.knockbackForce;
            _rigid2D.linearVelocity = new Vector2(velocityX, _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            float maxTime = 0.5f;   // 최대 넉백 지속 시간 (초)
            float threshold = 0.1f;   // 이 속도 이하면 즉시 정지

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                // 매 프레임 X 축 감속
                float decayedX = _rigid2D.linearVelocity.x * _settings.knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(decayedX, _rigid2D.linearVelocity.y);

                // 속도가 임계값 이하면 즉시 정지
                if (Mathf.Abs(_rigid2D.linearVelocity.x) < threshold)
                    break;
            }

            // 완전 정지 보장
            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
            _isKnockedBack = false;
        }

        // ══════════════════════════════════════════════════════
        // iFrame / 플래시 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// iFrame 코루틴.
        /// _settings.iFrameDuration 동안 무적 유지.
        /// </summary>
        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_settings.iFrameDuration);
            _isInvincible = false;
        }

        /// <summary>
        /// 피격 플래시 코루틴.
        /// iFrame 시간 동안 빨간 깜빡임 반복.
        /// </summary>
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
        /// 하위 클래스에서 자물쇠 피격, 이펙트 등 추가 처리.
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