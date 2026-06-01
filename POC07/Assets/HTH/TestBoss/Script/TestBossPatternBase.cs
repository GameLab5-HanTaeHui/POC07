// ============================================================
// TestBossPatternBase.cs  v1.0
// 테스트 보스 패턴 추상 베이스 클래스
//
// [기존 BossPatternBase 와의 차이점 — 시행착오 반영]
//
//   BossPatternBase (기존)          TestBossPatternBase (개선)
//   ─────────────────────────────   ─────────────────────────────
//   BossKnightDataSO 의존           TestBossDataSO 의존 (독립)
//   BossKnightAI 직접 참조          AI 참조 없음 — 이벤트만 사용
//   OverrideCooldownFromData 필요   쿨타임 자체 Inspector 설정
//   BossPatternSealResult 반환      단순화 — 그로기 이벤트만
//   Pause / Resume 지원             생략 (Counter 시스템 없음)
//   SpeedMultiplier 외부 주입       생략 (팔 봉인 시스템 없음)
//   BossRangeIndicator 연동         생략 (시각화 미착수)
//
// [TestBossAI 와의 관계]
//   TestBossAI.TrySelectPattern() 에서 CanExecute 체크 후 실행.
//   TestBossAI.ExecutePattern()   에서 Warning → Active → Recovery 순서 호출.
//   패턴 → AI 통신은 OnPatternGroggy 이벤트로만.
//   AI → 패턴 통신은 Interrupt() 호출로만.
//
// [하위 클래스 구현 필수]
//   OnWarning()  : 예고 모션 (색상 변경 등)
//   OnActive()   : 실제 공격 (히트박스 활성)
//   OnRecovery() : 후딜레이
//
// [그로기 유도 구조]
//   패턴이 Recovery 구간에서 OnPatternGroggy 발행
//   → TestBossAI 가 구독하여 EnterGroggy() 호출
//   → 플레이어에게 처형 기회 제공
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
    /// 테스트 보스 패턴 추상 베이스 클래스. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [3단계 생애주기]
    ///   Warning  : 예고 (색상 변경, 범위 표시)
    ///   Active   : 시전 (히트박스 활성, 이동)
    ///   Recovery : 후딜레이 (경직, 그로기 유도)
    ///
    /// [쿨타임]
    ///   Recovery 완료 후 자동으로 쿨타임 시작.
    ///   CanExecute = _cooldownTimer <= 0 && !_isExecuting
    ///
    /// [강제 중단]
    ///   Interrupt() 호출 → _isInterrupted = true
    ///   WaitScaled() 내부에서 매 프레임 체크 → 코루틴 자연 종료
    /// ────────────────────────────────────────────────────
    /// </summary>
    public abstract class TestBossPatternBase : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 패턴 기본 설정 ──────────────────────")]

        /// <summary>
        /// 패턴 쿨타임 (초).
        /// Recovery 완료 후 이 시간 동안 CanExecute = false.
        /// </summary>
        [Tooltip("패턴 쿨타임 (초).")]
        [Min(0f)]
        [SerializeField] protected float _cooldown = 5.0f;

        /// <summary>
        /// 예고 구간 지속 시간 (초).
        /// OnWarning() 에서 WaitScaled(_warningDuration) 으로 사용.
        /// </summary>
        [Tooltip("예고 구간 지속 시간 (초). 권장: 0.5~2.0.")]
        [Min(0f)]
        [SerializeField] protected float _warningDuration = 1.0f;

        /// <summary>
        /// 후딜레이 지속 시간 (초).
        /// OnRecovery() 에서 WaitScaled(_recoveryDuration) 으로 사용.
        /// </summary>
        [Tooltip("후딜레이 지속 시간 (초). 권장: 0.3~1.5.")]
        [Min(0f)]
        [SerializeField] protected float _recoveryDuration = 0.8f;

        /// <summary>
        /// Recovery 구간에서 그로기를 유도할지 여부.
        /// true = Recovery 완료 시 OnPatternGroggy 발행 → 플레이어 처형 기회.
        /// </summary>
        [Tooltip("Recovery 완료 시 그로기를 유도할지 여부.")]
        [SerializeField] protected bool _triggerGroggyOnRecovery = true;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 쿨타임 잔여 시간. </summary>
        private float _cooldownTimer;

        /// <summary> 현재 실행 중 여부. </summary>
        protected bool _isExecuting;

        /// <summary> 강제 중단 플래그. WaitScaled 내부에서 체크. </summary>
        protected bool _isInterrupted;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 패턴 시작 시 발행.
        /// TestBossAI 에서 상태 전환에 사용.
        /// </summary>
        public event Action<TestBossPatternBase> OnPatternStart;

        /// <summary>
        /// 패턴 종료 시 발행.
        /// TestBossAI 에서 상태 전환에 사용.
        /// </summary>
        public event Action<TestBossPatternBase> OnPatternEnd;

        /// <summary>
        /// 그로기 진입 조건 충족 시 발행.
        /// TestBossAI 가 구독 → EnterGroggy() 호출.
        ///
        /// [발행 시점]
        ///   _triggerGroggyOnRecovery == true → Recovery 완료 직후
        ///   하위 클래스에서 특정 조건(돌진 벽 충돌 등)에 직접 발행도 가능
        /// </summary>
        public event Action OnPatternGroggy;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 실행 가능 여부.
        /// 쿨타임 완료 + 현재 실행 중 아님.
        /// TestBossAI.TrySelectPattern() 에서 체크.
        /// </summary>
        public virtual bool CanExecute => _cooldownTimer <= 0f && !_isExecuting;

        /// <summary> 현재 실행 중 여부. </summary>
        public bool IsExecuting => _isExecuting;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected virtual void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 3단계 실행 (TestBossAI 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계 실행.
        /// TestBossAI.ExecutePattern() 에서 호출.
        /// </summary>
        public IEnumerator ExecuteWarning()
        {
            _isExecuting = true;
            _isInterrupted = false;

            OnPatternStart?.Invoke(this);

            yield return StartCoroutine(OnWarning());
        }

        /// <summary>
        /// Active 단계 실행.
        /// Warning 완료 후 TestBossAI 에서 호출.
        /// </summary>
        public IEnumerator ExecuteActive()
        {
            if (_isInterrupted) yield break;
            yield return StartCoroutine(OnActive());
        }

        /// <summary>
        /// Recovery 단계 실행.
        /// Active 완료 후 TestBossAI 에서 호출.
        /// Recovery 완료 후 쿨타임 시작 + 이벤트 발행.
        /// _triggerGroggyOnRecovery == true 이면 그로기 이벤트도 발행.
        /// </summary>
        public IEnumerator ExecuteRecovery()
        {
            if (_isInterrupted)
            {
                _isExecuting = false;
                _cooldownTimer = _cooldown;
                OnPatternEnd?.Invoke(this);
                yield break;
            }

            yield return StartCoroutine(OnRecovery());

            _isExecuting = false;
            _cooldownTimer = _cooldown;
            OnPatternEnd?.Invoke(this);

            // 그로기 유도
            if (_triggerGroggyOnRecovery && !_isInterrupted)
                OnPatternGroggy?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 추상 메서드 — 하위 클래스 구현 필수
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 예고 단계. 색상 변경 / 범위 표시 등.
        /// WaitScaled(_warningDuration) 으로 대기 권장.
        /// </summary>
        protected abstract IEnumerator OnWarning();

        /// <summary>
        /// 시전 단계. 히트박스 활성 / 이동 처리.
        /// 내부에서 직접 OnPatternGroggy 발행 가능 (돌진 벽 충돌 등).
        /// </summary>
        protected abstract IEnumerator OnActive();

        /// <summary>
        /// 후딜레이 단계. WaitScaled(_recoveryDuration) 으로 대기 권장.
        /// </summary>
        protected abstract IEnumerator OnRecovery();

        // ══════════════════════════════════════════════════════
        // 외부 제어 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 강제 중단.
        /// TestBossAI 에서 Groggy 진입 시 호출.
        /// _isInterrupted = true → WaitScaled 자연 종료.
        /// </summary>
        public void Interrupt()
        {
            _isInterrupted = true;
            _isExecuting = false;
            _cooldownTimer = _cooldown;

            OnPatternEnd?.Invoke(this);
            Debug.Log($"[{GetType().Name}] 강제 중단");
        }

        // ══════════════════════════════════════════════════════
        // 보조 메서드 (하위 클래스에서 사용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 중단 체크 포함 대기.
        /// 하위 클래스에서 yield return WaitScaled(시간) 으로 사용.
        ///
        /// [BossPatternBase.WaitScaled 와의 차이]
        ///   SpeedMultiplier 없음 (팔 봉인 시스템 미포함)
        ///   Pause 없음 (Counter 시스템 미포함)
        ///   _isInterrupted 체크만 유지
        /// </summary>
        /// <param name="duration">대기 시간 (초).</param>
        protected IEnumerator WaitScaled(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_isInterrupted) yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 그로기 유도 이벤트를 수동으로 발행.
        /// 하위 클래스에서 특정 조건(돌진 벽 충돌 등)에 직접 호출.
        /// </summary>
        protected void TriggerGroggy()
        {
            OnPatternGroggy?.Invoke();
        }
    }
}