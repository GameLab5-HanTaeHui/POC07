// ============================================================
// EnemyBossBase.cs  v1.0
// 보스 전용 베이스 추상 클래스
//
// [제작 배경]
//   기존 BossKnight 는 EnemyBase(일반 적 베이스)를 억지 상속.
//   EnemyBase 내부의 EnemyDataSO(_settings) 슬롯이 강제 노출되고
//   _settings = null 우회 처리, base.Awake() 제거 등 부작용 발생.
//   보스 전용 베이스를 분리하여 구조적 결합 해소.
//
// [EnemyBase 와의 차이]
//   EnemyBase         : EnemyDataSO 의존 / 일반 적 전용
//   EnemyBossBase     : BossDataSO(제네릭) 독립 / 보스 전용
//
//   공통 보존 항목:
//     - IDamageable 구현 (TakeDamage / Die)
//     - HP / 무적프레임 / 넉백 / 피격플래시 코루틴
//     - OnDead 이벤트
//     - virtual OnDamaged() 확장점
//     - ResetBoss() API
//     - Gizmos HP 표시
//
//   제거 항목:
//     - EnemyDataSO _settings (일반 적 전용 SO — 보스 불필요)
//     - Settings 프로퍼티 (EnemyDataSO 반환 — 보스 불필요)
//
//   추가/변경 항목:
//     - 수치 직접 참조 : BossKnightDataSO._bossData (하위에서 주입)
//     - 수치 추상 프로퍼티 :
//         abstract float BossMaxHp
//         abstract float BossKnockbackForce
//         abstract float BossKnockbackDecay
//         abstract float BossIFrameDuration
//     - Phase 전환 중 무적 : _isPhaseInvincible (BossKnight 에서 이전)
//     - HpRatio override 불필요 → BossMaxHp 기반으로 자체 계산
//
// [상속 구조]
//   MonoBehaviour
//     └── EnemyBossBase (abstract)  ← IDamageable
//           └── BossKnight
//
// [BossKnight 변경 사항]
//   : EnemyBase  →  : EnemyBossBase
//   base.Awake() 우회 코드 제거
//   _settings null 허용 처리 제거
//   HpRatio override 제거 (EnemyBossBase 가 자체 처리)
//   _isPhaseInvincible 선언 EnemyBossBase 로 이전
//   abstract 수치 프로퍼티 override 4개 추가
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 보스 전용 베이스 추상 클래스. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 흐름]
    ///   IDamageable.TakeDamage(info)
    ///     → _isPhaseInvincible 체크  (Phase 전환 중 완전 무시)
    ///     → _isInvincible 체크       (iFrame 중 무시)
    ///     → _isDead 체크
    ///     → virtual TakeDamage() 실행
    ///         → 하위(BossKnight) 에서 override
    ///             → 부위/조건 판단
    ///               → base.TakeDamage() 호출 → 실제 체력 감소
    ///
    /// [Phase 전환 무적]
    ///   BossKnight.EnterPhaseTransition() 에서
    ///   _isPhaseInvincible = true / false 직접 제어.
    ///   EnemyBossBase 에서 TakeDamage 진입 전에 체크.
    ///
    /// [수치 공급 구조]
    ///   BossMaxHp / BossKnockbackForce 등 abstract 프로퍼티
    ///   → BossKnight 에서 _bossData.maxHp 등으로 override 구현
    ///   → EnemyBossBase 내부 코루틴이 이 프로퍼티를 참조
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class EnemyBossBase : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // 컴포넌트 참조 (protected — 하위 클래스 접근 허용)
        // ──────────────────────────────────────────

        /// <summary> Rigidbody2D. Awake 에서 자동 취득. </summary>
        protected Rigidbody2D _rigid2D;

        /// <summary> SpriteRenderer. Awake 에서 자동 취득. </summary>
        protected SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. 하위 클래스 접근 허용. </summary>
        protected float _currentHp;

        /// <summary>
        /// iFrame 무적 여부.
        /// InvincibleRoutine 코루틴에서 제어.
        /// </summary>
        private bool _isInvincible;

        /// <summary> 사망 여부. 중복 Die() 방지. </summary>
        private bool _isDead;

        /// <summary>
        /// Phase 전환 중 무적 여부.
        /// true 이면 TakeDamage 완전 무시.
        /// BossKnight.EnterPhaseTransition() 에서 직접 제어.
        /// </summary>
        protected bool _isPhaseInvincible;

        // ──────────────────────────────────────────
        // 코루틴 핸들 (중복 방지용)
        // ──────────────────────────────────────────

        private Coroutine _iFrameCoroutine;
        private Coroutine _knockbackCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 사망 시 1회 발행.
        /// GameManager 구독 → 씬 전환 / 엔딩 처리.
        /// </summary>
        public event Action OnDead;

        // ──────────────────────────────────────────
        // 프로퍼티 — 공통
        // ──────────────────────────────────────────

        /// <summary> 현재 체력. </summary>
        public float CurrentHp => _currentHp;

        /// <summary> 체력 비율 (0~1). 보스 HP 바 용. </summary>
        public float HpRatio => BossMaxHp > 0f ? _currentHp / BossMaxHp : 0f;

        /// <summary> 현재 사망 여부. </summary>
        public bool IsDead => _isDead;

        /// <summary> 현재 무적 여부 (iFrame 또는 Phase 전환). </summary>
        public bool IsInvincible => _isInvincible || _isPhaseInvincible;

        // ──────────────────────────────────────────
        // 추상 프로퍼티 — 수치 공급 (하위 클래스 필수 구현)
        // ──────────────────────────────────────────

        /// <summary>
        /// 보스 최대 체력.
        /// BossKnight → _bossData.maxHp 반환.
        /// </summary>
        protected abstract float BossMaxHp { get; }

        /// <summary>
        /// 넉백 초기 속도.
        /// BossKnight → _bossData.knockbackForce 반환.
        /// </summary>
        protected abstract float BossKnockbackForce { get; }

        /// <summary>
        /// 넉백 감속 비율.
        /// BossKnight → _bossData.knockbackDecay 반환.
        /// </summary>
        protected abstract float BossKnockbackDecay { get; }

        /// <summary>
        /// iFrame 지속 시간 (초).
        /// BossKnight → _bossData.iFrameDuration 반환.
        /// </summary>
        protected abstract float BossIFrameDuration { get; }

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Rigidbody2D / SpriteRenderer 자동 취득 + HP 초기화.
        /// 하위 클래스에서 override 시 반드시 base.Awake() 호출.
        ///
        /// [EnemyBase 와의 차이]
        ///   EnemyBase : _settings null 시 enabled=false (DataSO 강제)
        ///   EnemyBossBase : DataSO 체크 없음 → 하위 클래스 Awake 에서 처리
        /// </summary>
        protected virtual void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // BossMaxHp 는 abstract — 하위 클래스에서 _bossData 설정 후 반환
            // Awake 시점에 _bossData 가 아직 null 일 수 있으므로
            // HP 초기화는 하위 클래스 Awake 에서 InitializeHp() 호출로 처리
        }

        // ══════════════════════════════════════════════════════
        // 초기화 API (하위 클래스에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// HP 를 BossMaxHp 로 초기화.
        /// 하위 클래스 Awake() 에서 _bossData 연결 후 호출.
        ///
        /// [호출 예시 — BossKnight.Awake()]
        ///   _bossData 유효성 체크
        ///   → InitializeHp()
        /// </summary>
        protected void InitializeHp()
        {
            _currentHp = BossMaxHp;
        }

        /// <summary>
        /// HP 를 지정 값으로 회복.
        /// Phase 2→3 전환 시 HP 100% 회복에 사용.
        /// </summary>
        /// <param name="amount">회복량. 0 미만 무시.</param>
        public void RestoreHp(float amount)
        {
            if (amount <= 0f) return;
            _currentHp = Mathf.Min(_currentHp + amount, BossMaxHp);
            Debug.Log($"[{GetType().Name}] HP 회복: +{amount:F0} → {_currentHp:F0}/{BossMaxHp:F0}");
        }

        /// <summary>
        /// HP 를 BossMaxHp 로 완전 회복.
        /// Phase 전환 HP 리셋 시 호출.
        /// </summary>
        public void RestoreFullHp()
        {
            _currentHp = BossMaxHp;
            Debug.Log($"[{GetType().Name}] HP 완전 회복: {_currentHp:F0}/{BossMaxHp:F0}");
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현 — virtual
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리. (virtual — BossKnight 에서 override)
        ///
        /// [체크 우선순위]
        ///   1. _isPhaseInvincible → 완전 무시 (Phase 전환 중)
        ///   2. _isInvincible      → iFrame 무시
        ///   3. _isDead            → 이미 사망, 무시
        ///
        /// [흐름]
        ///   BossKnight.TakeDamage()
        ///     → 부위 조건 판단
        ///       → base.TakeDamage(info) 호출 → 실제 체력 감소
        /// </summary>
        public virtual void TakeDamage(DamageInfo info)
        {
            // Phase 전환 중 완전 무적
            if (_isPhaseInvincible) return;
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
            HitFeedback.PlayerHitEnemy(_spriteRenderer, transform, info.Direction);

            // ⑤ 하위 클래스 확장점
            OnDamaged(info);

            Debug.Log($"[{GetType().Name}] 피격: -{info.Amount:F0} / HP {_currentHp:F0}/{BossMaxHp:F0}");

            // ⑥ 사망 체크
            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 사망 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 사망 처리.
        /// OnDead 이벤트 발행 → GameManager / 엔딩 처리.
        ///
        /// [EnemyBase 와 동일 구조]
        ///   중복 호출 방지 (_isDead 플래그)
        ///   velocity = 0 / SpriteRenderer 색상 복원
        ///   하위 클래스 확장점: OnBossDied()
        /// </summary>
        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;

            StopAllCoroutines();
            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

            Debug.Log($"[{GetType().Name}] 보스 사망!");
            OnDead?.Invoke();

            // 하위 클래스 확장점
            OnBossDied();
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 넉백 코루틴.
        /// velocity.x = direction.x * BossKnockbackForce 후 BossKnockbackDecay 감속.
        ///
        /// [보스 특성]
        ///   BossKnockbackForce 가 0 이면 즉시 종료 (넉백 없음).
        ///   일반 적보다 낮은 BossKnockbackForce 권장 (3 이하).
        /// </summary>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (BossKnockbackForce <= 0f) yield break;

            _rigid2D.linearVelocity = new Vector2(
                direction.x * BossKnockbackForce,
                _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            const float maxTime = 0.5f;
            const float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float vx = _rigid2D.linearVelocity.x * BossKnockbackDecay;
                _rigid2D.linearVelocity = new Vector2(vx, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(vx) < threshold) break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        /// <summary>
        /// iFrame 코루틴.
        /// BossIFrameDuration 동안 _isInvincible = true.
        /// </summary>
        private IEnumerator InvincibleRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(BossIFrameDuration);
            _isInvincible = false;
        }

        // ══════════════════════════════════════════════════════
        // 가상 메서드 — 하위 클래스 확장점
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TakeDamage 처리 후 추가 로직.
        /// BossKnight 에서 Phase 전환 체크, 부위 반응 등에 사용.
        /// </summary>
        protected virtual void OnDamaged(DamageInfo info) { }

        /// <summary>
        /// Die() 완료 후 추가 로직.
        /// BossKnight 에서 처형 연출, 최종 이벤트 등에 사용.
        /// </summary>
        protected virtual void OnBossDied() { }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 상태 완전 리셋.
        /// 테스트 / 보스 룸 재진입 시 호출.
        ///
        /// [주의]
        ///   HP 는 BossMaxHp 로 리셋.
        ///   Phase 는 BossKnight 에서 별도 처리 필요.
        /// </summary>
        public virtual void ResetBoss()
        {
            _currentHp = BossMaxHp;
            _isInvincible = false;
            _isPhaseInvincible = false;
            _isDead = false;

            StopAllCoroutines();

            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos — HP 디버그 표시
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Scene 뷰에서 보스 HP / 상태 표시.
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = _isDead ? Color.gray : Color.red;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                $"[Boss] HP {_currentHp:F0}/{BossMaxHp:F0}  " +
                $"iFrame:{_isInvincible}  PhaseInv:{_isPhaseInvincible}  Dead:{_isDead}");
#endif
        }
    }
}