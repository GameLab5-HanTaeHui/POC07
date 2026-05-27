// ============================================================
// EnemyAttackBase.cs  v1.0
// 적 공격 추상 베이스 클래스
//
// [역할]
//   모든 적 공격 컴포넌트의 추상 베이스.
//   쿨타임 관리와 히트박스 활성 타이밍을 공통 처리.
//   ExecuteAttack() 을 하위 클래스에서 구현.
//
// [상속 구조]
//   EnemyAttackBase (abstract)
//     └── KnightAttack  근접 내려치기 단타
//     └── DroneAttack   (추후)
//
// [EnemyAI 와의 관계]
//   EnemyAI 가 Attack 상태 진입 시 TryAttack() 호출.
//   쿨타임 체크 후 ExecuteAttack() 코루틴 실행.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공격 추상 베이스 클래스. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [하위 클래스 구현 필수]
    ///   ExecuteAttack() : 실제 공격 로직 (히트박스 활성 등)
    ///
    /// [외부 호출 흐름]
    ///   EnemyAI → TryAttack()
    ///     → 쿨타임 체크
    ///       → ExecuteAttack() 코루틴 실행
    ///         → OnAttackFinished() 콜백
    ///           → EnemyAI 가 Chase 로 복귀
    /// ────────────────────────────────────────────────────
    /// </summary>
    public abstract class EnemyAttackBase : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 공격 중 여부. </summary>
        protected bool _isAttacking;

        /// <summary> 쿨타임 잔여 시간. </summary>
        private float _cooldownTimer;

        /// <summary> 현재 실행 중인 공격 코루틴. </summary>
        private Coroutine _attackCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 공격 완료 시 발행.
        /// EnemyAI 가 구독하여 Chase 상태로 복귀.
        /// </summary>
        public event System.Action OnAttackFinished;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 공격 중 여부. </summary>
        public bool IsAttacking => _isAttacking;

        /// <summary> 공격 가능 여부 (쿨타임 완료 + 공격 중 아님). </summary>
        public bool CanAttack => _cooldownTimer <= 0f && !_isAttacking;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — EnemyAI 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 시도.
        /// 쿨타임 또는 공격 중이면 무시.
        /// EnemyAI 의 Attack 상태 진입 시 호출.
        /// </summary>
        /// <param name="cooldown">이 공격의 쿨타임 (KnightDataSO.attackCooldown)</param>
        public void TryAttack(float cooldown)
        {
            if (!CanAttack) return;

            _cooldownTimer = cooldown;

            if (_attackCoroutine != null)
                StopCoroutine(_attackCoroutine);

            _attackCoroutine = StartCoroutine(AttackSequence());
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 시퀀스 코루틴.
        /// ExecuteAttack() 실행 후 OnAttackFinished 발행.
        /// </summary>
        private IEnumerator AttackSequence()
        {
            _isAttacking = true;
            yield return StartCoroutine(ExecuteAttack());
            _isAttacking = false;
            OnAttackFinished?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 추상 메서드 — 하위 클래스 구현 필수
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 실제 공격 로직.
        /// 히트박스 활성 → 지속 → 비활성 순서로 구현.
        /// AttackSequence 코루틴 내부에서 yield return 으로 실행됨.
        /// </summary>
        protected abstract IEnumerator ExecuteAttack();
    }
}