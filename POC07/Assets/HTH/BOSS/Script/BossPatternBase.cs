// ============================================================
// BossPatternBase.cs  v1.0
// 보스 패턴 추상 베이스 클래스
//
// [역할]
//   모든 보스 패턴 컴포넌트의 추상 베이스.
//   Warning → Active → Recovery 3단계 생애주기 관리.
//   쿨타임 / 일시정지 / 강제 중단 / 속도 배율 공통 처리.
//
// [EnemyAttackBase 와의 차이]
//   EnemyAttackBase: 단순 쿨타임 + ExecuteAttack 코루틴
//   BossPatternBase: Warning / Active / Recovery 3단계 분리
//                   일시정지 (Pause) / 재개 (Resume) 지원
//                   강제 중단 (Interrupt) 지원
//                   속도 배율 (_speedMultiplier) 지원
//                   BossRangeIndicator 연동
//                   봉인 감지 결과 반환 (BossPatternSealResult)
//
// [상속 구조]
//   BossPatternBase (abstract)
//     └── Phase 1
//           BossPattern_ShieldCharge    방패 돌진
//           BossPattern_DefenseStance   방어 자세
//           BossPattern_PunchR          오른팔 주먹 공격
//     └── Phase 2
//           BossPattern_Advance         전방 진군 (3연속 돌진)
//           BossPattern_Charge          전방 돌격 (긴 돌진)
//           BossPattern_SwordSlash7     검 제식 7
//           BossPattern_SwordSlash12    검 제식 12
//     └── Phase 3
//           BossPattern_Slash4          검 제식 4
//           BossPattern_Slash0          검 제식 0
//           BossPattern_Slash1          검 제식 1
//           BossPattern_PunchDash       주먹 돌진
//           BossPattern_Grab            횡 잡기
//
// [BossKnightAI 와의 관계]
//   BossKnightAI.TrySelectPattern() 에서 CanExecute 체크 후 실행.
//   BossKnightAI.ExecutePattern() 코루틴에서 3단계 순서 호출.
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
    /// 보스 패턴 추상 베이스 클래스. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [하위 클래스 구현 필수]
    ///   ExecuteWarning()  : 예고 모션 + 범위 표시
    ///   ExecuteActive()   : 실제 공격 + 히트박스 활성
    ///   ExecuteRecovery() : 후딜레이 모션
    ///
    /// [봉인 감지 처리]
    ///   하위 클래스에서 봉인 투사체 명중 시 OnSealHit() 호출.
    ///   반환값(BossPatternSealResult)에 따라
    ///   BossCounterSystem 이 검 무식 / 대타 출동 / 그로기 처리.
    ///
    /// [속도 배율]
    ///   BossPartComponent 가 SetSpeedMultiplier() 로 주입.
    ///   팔 봉인 시 해당 팔 패턴의 WaitScaled() 시간 늘어남.
    ///   → 플레이어에게 공략 시간 추가 제공.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public abstract class BossPatternBase : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 패턴 기본 설정 ──────────────────────")]

        /// <summary>
        /// 패턴 재사용 대기 시간 (초).
        /// DataSO 에서 값을 주입받아 덮어쓸 수 있음.
        /// </summary>
        [Tooltip("패턴 쿨타임. DataSO 에서 Override 가능.")]
        [Min(0f)]
        [SerializeField] protected float _cooldown = 5.0f;

        /// <summary>
        /// 예고 구간 지속 시간 (초).
        /// 플레이어가 패턴을 인식하고 회피 준비하는 시간.
        /// </summary>
        [Tooltip("예고 구간 지속 시간. 권장: 0.5~2.0.")]
        [Min(0f)]
        [SerializeField] protected float _warningDuration = 1.0f;

        /// <summary>
        /// 후딜레이 지속 시간 (초).
        /// 패턴 완료 후 경직 구간. 플레이어 공격 기회.
        /// </summary>
        [Tooltip("후딜레이 지속 시간. 플레이어 공격 기회. 권장: 0.3~1.0.")]
        [Min(0f)]
        [SerializeField] protected float _recoveryDuration = 0.5f;

        [Header("── 봉인 반응 설정 ──────────────────────")]

        /// <summary>
        /// 예고 중 봉인 가능 여부.
        /// true: 예고 중 봉인 투사체 감지 시 패턴 중단 or 검 무식 발동.
        /// false: 예고 중 봉인 투사체 무시 (흡수).
        /// </summary>
        [Tooltip("예고 구간에서 봉인 반응 여부.")]
        [SerializeField] protected bool _canInterruptDuringWarning = false;

        /// <summary>
        /// 시전 중 봉인 가능 여부.
        /// true: 시전 중 봉인 투사체 감지 시 패턴 중단 or 대타 출동.
        /// false: 시전 중 봉인 투사체 무시.
        /// </summary>
        [Tooltip("시전 중 봉인 반응 여부.")]
        [SerializeField] protected bool _canInterruptDuringActive = false;

        [Header("── Phase 3 패턴 타입 ──────────────────────")]

        /// <summary>
        /// Phase 3 에서 검을 사용하는 패턴인지 여부.
        /// BossCounterSystem 이 검 무식 / 대타 출동 우선순위 결정에 사용.
        /// true  = 검 패턴 → 대타 출동 우선
        /// false = 주먹 패턴 → 대타 출동만 허용
        /// </summary>
        [Tooltip("Phase 3 검 패턴 여부. 반격 우선순위 결정에 사용.")]
        [SerializeField] protected bool _isSwordPattern = false;

        [Header("── 범위 표시 ──────────────────────")]

        /// <summary>
        /// 예상 범위 시각화 컴포넌트.
        /// Warning 시작 시 활성, 종료 시 비활성.
        /// DataSO.rangeIndicatorEnabled = false 이면 무시.
        /// </summary>
        [Tooltip("예상 범위 시각화. Inspector on/off.")]
        [SerializeField] protected BossRangeIndicator _rangeIndicator;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private float _cooldownTimer;
        private bool _isPaused;
        private bool _isInterrupted;
        private bool _isExecuting;

        /// <summary>
        /// 속도 배율. 1.0 = 정상 속도.
        /// BossPartComponent 가 팔 봉인 시 1.0 초과 값 주입.
        /// → WaitScaled() 시간이 배율에 비례하여 늘어남.
        /// </summary>
        protected float _speedMultiplier = 1.0f;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        protected BossKnightDataSO _data;
        protected BossKnightAI _ai;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 패턴 시작 시 발행. BossCounterSystem 이 구독.
        /// </summary>
        public event Action<BossPatternBase> OnPatternStart;

        /// <summary>
        /// 패턴 종료 시 발행.
        /// </summary>
        public event Action<BossPatternBase> OnPatternEnd;

        /// <summary>
        /// 패턴 중 그로기 진입 조건 충족 시 발행.
        /// BossKnightAI 가 구독 → EnterGroggy() 호출.
        /// </summary>
        public event Action OnPatternGroggy;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 실행 가능 여부. 쿨타임 완료 + 현재 실행 중 아님.
        /// BossKnightAI.TrySelectPattern() 에서 체크.
        /// </summary>
        public bool CanExecute => _cooldownTimer <= 0f && !_isExecuting;

        /// <summary> 현재 실행 중 여부. </summary>
        public bool IsExecuting => _isExecuting;

        /// <summary> 검 패턴 여부. BossCounterSystem 반격 우선순위 결정용. </summary>
        public bool IsSwordPattern => _isSwordPattern;

        /// <summary> 예고 중 봉인 가능 여부. </summary>
        public bool CanInterruptDuringWarning => _canInterruptDuringWarning;

        /// <summary> 시전 중 봉인 가능 여부. </summary>
        public bool CanInterruptDuringActive => _canInterruptDuringActive;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. BossKnight.Start() 에서 호출.
        /// DataSO 에서 쿨타임 값 덮어쓰기 가능.
        /// </summary>
        public virtual void Initialize(BossKnightDataSO data, BossKnightAI ai)
        {
            _data = data;
            _ai = ai;

            // 하위 클래스에서 DataSO 수치를 _cooldown 에 덮어씀
            OverrideCooldownFromData(data);
        }

        /// <summary>
        /// DataSO 에서 쿨타임 값 적용.
        /// 하위 클래스에서 override 하여 해당 패턴의 쿨타임 필드 연결.
        ///
        /// 예시 (BossPattern_PunchR):
        ///   protected override void OverrideCooldownFromData(BossKnightDataSO data)
        ///       => _cooldown = data.p1.punchCooldown;
        /// </summary>
        protected virtual void OverrideCooldownFromData(BossKnightDataSO data) { }

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected virtual void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 3단계 실행 (BossKnightAI 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계 실행.
        /// 예고 모션 + 범위 표시.
        /// BossKnightAI.ExecutePattern() 에서 호출.
        /// </summary>
        public IEnumerator ExecuteWarning()
        {
            _isExecuting = true;
            _isInterrupted = false;
            _isPaused = false;

            OnPatternStart?.Invoke(this);
            ShowRangeIndicator(true);

            yield return StartCoroutine(OnWarning());

            ShowRangeIndicator(false);
        }

        /// <summary>
        /// Active 단계 실행.
        /// 실제 공격 + 히트박스 활성.
        /// </summary>
        public IEnumerator ExecuteActive()
        {
            if (_isInterrupted) yield break;
            yield return StartCoroutine(OnActive());
        }

        /// <summary>
        /// Recovery 단계 실행.
        /// 후딜레이 모션.
        /// </summary>
        public IEnumerator ExecuteRecovery()
        {
            if (_isInterrupted) yield break;

            yield return StartCoroutine(OnRecovery());

            _isExecuting = false;
            StartCooldown();
            OnPatternEnd?.Invoke(this);
        }

        // ══════════════════════════════════════════════════════
        // 추상 메서드 — 하위 클래스 구현 필수
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 예고 단계 구현.
        /// 예고 모션 + 범위 시각화 제어 포함.
        /// WaitScaled() 로 대기 시간에 속도 배율 적용.
        /// </summary>
        protected abstract IEnumerator OnWarning();

        /// <summary>
        /// 시전 단계 구현.
        /// 히트박스 활성 + 실제 공격 처리.
        /// 봉인 감지 시 TryGroggy() 호출.
        /// </summary>
        protected abstract IEnumerator OnActive();

        /// <summary>
        /// 후딜레이 단계 구현.
        /// WaitScaled() 로 대기.
        /// </summary>
        protected abstract IEnumerator OnRecovery();

        // ══════════════════════════════════════════════════════
        // 외부 제어 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 일시 정지.
        /// BossKnightAI.EnterCounter() 에서 호출.
        /// 검 무식 진행 중 현재 패턴 대기.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 일시 정지 해제.
        /// BossKnightAI.ExitCounter() 에서 호출.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
        }

        /// <summary>
        /// 강제 중단.
        /// Groggy 진입 / Phase 전환 시 호출.
        /// 진행 중인 코루틴이 _isInterrupted 체크로 자연 종료.
        /// </summary>
        public void Interrupt()
        {
            _isInterrupted = true;
            _isPaused = false;
            _isExecuting = false;

            ShowRangeIndicator(false);
            StartCooldown();

            OnPatternEnd?.Invoke(this);
        }

        /// <summary>
        /// 속도 배율 설정.
        /// BossPartComponent 가 팔 봉인 시 1.0 초과 값 주입.
        /// 봉인 해제 시 1.0 복귀.
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        // ══════════════════════════════════════════════════════
        // 봉인 감지 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 투사체 명중 시 BossCounterSystem 에서 호출.
        /// 현재 패턴 상태와 설정에 따라 결과 반환.
        ///
        /// [결과별 처리]
        ///   Absorbed        → 아무것도 안 함
        ///   Interrupted     → BossKnightAI.EnterGroggy()
        ///   RequestParry    → BossCounterSystem 검 무식 발동
        ///   RequestIntercept→ BossCounterSystem 대타 출동 발동
        /// </summary>
        public virtual BossPatternSealResult OnSealHit(bool isDuringWarning, bool isDuringActive)
        {
            // 현재 실행 중이 아님 → 전투 대기 중 → 검 무식 요청
            if (!_isExecuting)
                return BossPatternSealResult.RequestParry;

            // Warning 중
            if (isDuringWarning)
            {
                if (!_canInterruptDuringWarning)
                    return BossPatternSealResult.Absorbed;

                // 봉인 가능 패턴
                // Phase 3 검 패턴 → 대타 출동 요청 (AI가 가용 주먹 없으면 검 무식으로 전환)
                if (_isSwordPattern)
                    return BossPatternSealResult.RequestIntercept;

                // Phase 1/2 or Phase 3 주먹 패턴 → 중단 + 그로기
                _isInterrupted = true;
                OnPatternGroggy?.Invoke();
                return BossPatternSealResult.Interrupted;
            }

            // Active 중
            if (isDuringActive)
            {
                if (!_canInterruptDuringActive)
                    return BossPatternSealResult.Absorbed;

                // 대타 출동 요청
                return BossPatternSealResult.RequestIntercept;
            }

            return BossPatternSealResult.Absorbed;
        }

        // ══════════════════════════════════════════════════════
        // 보조 메서드 (하위 클래스에서 사용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 일시정지 / 중단 체크 포함 대기.
        /// 하위 클래스에서 yield return WaitScaled(시간) 으로 사용.
        /// _speedMultiplier 배율 적용 (팔 봉인 시 시간 증가).
        /// </summary>
        protected IEnumerator WaitScaled(float duration)
        {
            float scaled = duration * _speedMultiplier;
            float elapsed = 0f;

            while (elapsed < scaled)
            {
                if (_isInterrupted) yield break;

                // 일시정지 중 대기
                if (_isPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 그로기 진입 트리거.
        /// 패턴 봉인 성공 / 특정 조건 충족 시 호출.
        /// OnPatternGroggy 이벤트 발행 → BossKnightAI.EnterGroggy().
        /// </summary>
        protected void TriggerGroggy()
        {
            _isInterrupted = true;
            OnPatternGroggy?.Invoke();
        }

        /// <summary>
        /// 쿨타임 시작.
        /// ExecuteRecovery() 완료 또는 Interrupt() 에서 호출.
        /// </summary>
        private void StartCooldown()
        {
            _cooldownTimer = _cooldown;
        }

        /// <summary>
        /// 범위 표시 on/off.
        /// DataSO.rangeIndicatorEnabled 가 false 이면 무시.
        /// </summary>
        private void ShowRangeIndicator(bool show)
        {
            if (_rangeIndicator == null) return;
            if (_data != null && !_data.rangeIndicatorEnabled) return;
            _rangeIndicator.SetVisible(show);
        }
    }
}