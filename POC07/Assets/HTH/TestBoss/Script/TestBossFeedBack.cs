// ============================================================
// TestBossFeedback.cs  v1.0
// 테스트 보스 DOTween 시각 피드백 컴포넌트
//
// [역할]
//   TestBoss 의 모든 상태 전환에 맞는 DOTween 연출을 담당.
//   게임플레이 코드(TestBossCore / TestBossAI / TestBossPattern_*)
//   와 완전히 분리된 순수 비주얼 레이어.
//
// [설계 원칙]
//   1. 게임플레이 로직은 건드리지 않는다.
//      → TestBossCore / TestBossAI 의 이벤트만 구독.
//   2. 이전 Tween 을 반드시 Kill 후 새 Tween 시작.
//      → DOTween 중복 실행 방지.
//   3. OnDestroy / OnDisable 에서 모든 Tween 정리.
//
// [상태별 연출]
//
//   Chase / Idle
//     → 기본 색상 복구, Tween 없음
//
//   Warning — Charge (돌진 예고)
//     → 본체 X 스케일 진동 (좌우 압축) — "힘을 모으는" 느낌
//     → 색상: 기본색 ↔ 주황색 Ping-Pong
//     → 플레이어에게: "옆으로 피해라"
//
//   Warning — Stomp (광역 예고)
//     → 본체 Y 스케일 팽창 (위아래로 커짐) — "부풀어 오르는" 느낌
//     → 색상: 기본색 ↔ 보라색 Ping-Pong
//     → 플레이어에게: "뒤로 피해라"
//
//   Active — Charge (돌진 중)
//     → 색상: 밝은 흰빛 플래시 후 붉은색 유지
//     → 본체 X 스케일 길게 늘어남 (속도감)
//
//   Active — Stomp (광역 폭발)
//     → 본체 Scale Punch (팝 이펙트)
//     → 충격파 오브젝트 Scale Out (있는 경우)
//
//   Recovery (후딜레이 경직)
//     → 본체 Shake Position (미세 진동)
//     → 색상: 빨간색 유지 후 페이드 아웃
//
//   Groggy (처형 가능 구간)
//     → 색상: 노란색 느린 Ping-Pong (처형 타이밍 알림)
//     → Y 스케일 약간 아래로 (축 처진 느낌)
//     → 루프 — Groggy 종료 시 Kill
//
//   DilTime (집중 공격 구간)
//     → 본체 색상: 주황색 빠른 Pulse 루프
//     → 코어 오브젝트: 흰색 ↔ 노란색 빠른 Ping-Pong
//     → 루프 — DilTime 종료 시 Kill
//
//   피격 (딜타임 중 코어 맞음)
//     → 본체 흰색 플래시 (0.1초)
//     → X 위치 미세 흔들림 (넉백 피드백)
//
//   보스 처치
//     → 전체 Scale 0 으로 Shrink
//     → 색상: 회색 페이드
//
// [연결 방법]
//   TestBoss 루트 오브젝트에 TestBossFeedback 부착.
//   TestBossCore, TestBossAI 는 자동 탐색.
//   _bodyRenderer    : 루트 SpriteRenderer (자동 탐색 가능)
//   _coreRenderer    : Core 자식 SpriteRenderer (선택)
//   _chargePattern   : TestBossPattern_Charge (선택, Charge 전용 연출)
//   _stompPattern    : TestBossPattern_Stomp  (선택, Stomp 전용 연출)
//
// [DOTween 사용 버전]
//   DOTween (Free) — DOTween.Init() 불필요 (AutoPlay 기본값)
//   using DG.Tweening
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using DG.Tweening;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 DOTween 시각 피드백 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [외부 의존 없음]
    ///   TestBossCore / TestBossAI 이벤트 구독만.
    ///   게임플레이 로직 전혀 없음.
    ///   이 컴포넌트가 없어도 게임은 정상 동작.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossFeedback : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector — 컴포넌트 연결
        // ──────────────────────────────────────────

        [Header("── 렌더러 연결 ──────────────────────")]

        /// <summary>
        /// 보스 본체 SpriteRenderer.
        /// 미연결 시 자동 탐색.
        /// </summary>
        [Tooltip("보스 본체 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _bodyRenderer;

        /// <summary>
        /// 코어 SpriteRenderer.
        /// DilTime 중 빠른 Pulse 연출.
        /// 미연결 시 코어 연출 생략.
        /// </summary>
        [Tooltip("코어 SpriteRenderer. 미연결 시 코어 연출 생략.")]
        [SerializeField] private SpriteRenderer _coreRenderer;

        [Header("── 패턴 연결 (선택) ──────────────────────")]

        /// <summary>
        /// 돌진 패턴 컴포넌트.
        /// Warning 타입 구분에 사용.
        /// </summary>
        [Tooltip("TestBossPattern_Charge. 연결 시 Charge Warning 전용 연출.")]
        [SerializeField] private TestBossPattern_Charge _chargePattern;

        /// <summary>
        /// 광역 패턴 컴포넌트.
        /// Warning 타입 구분에 사용.
        /// </summary>
        [Tooltip("TestBossPattern_Stomp. 연결 시 Stomp Warning 전용 연출.")]
        [SerializeField] private TestBossPattern_Stomp _stompPattern;

        // ──────────────────────────────────────────
        // Inspector — 연출 수치 (DOTween)
        // ──────────────────────────────────────────

        [Header("── Warning — Charge 연출 ──────────────────────")]

        [Tooltip("Charge Warning 시 X 스케일 진동 강도. 권장: 0.15~0.3.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _chargeWarnShakeStrength = 0.2f;

        [Tooltip("Charge Warning 시 X 스케일 진동 주기 (초). 권장: 0.15~0.25.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _chargeWarnShakePeriod = 0.18f;

        [Tooltip("Charge Warning 색상 (주황).")]
        [SerializeField] private Color _chargeWarnColor = new Color(1f, 0.55f, 0.1f, 1f);

        [Header("── Warning — Stomp 연출 ──────────────────────")]

        [Tooltip("Stomp Warning 시 Y 스케일 팽창 크기. 권장: 0.1~0.25.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _stompWarnExpandY = 0.18f;

        [Tooltip("Stomp Warning 색상 (보라).")]
        [SerializeField] private Color _stompWarnColor = new Color(0.7f, 0.2f, 1.0f, 1f);

        [Header("── Active 연출 ──────────────────────")]

        [Tooltip("Active 돌진 중 X 스케일 늘어남 비율. 권장: 0.15~0.3.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _chargeActiveScaleX = 0.2f;

        [Tooltip("Stomp Active 시 Scale Punch 강도. 권장: 0.3~0.6.")]
        [Range(0f, 1f)]
        [SerializeField] private float _stompActivePunch = 0.45f;

        [Header("── Groggy 연출 ──────────────────────")]

        [Tooltip("Groggy 노란색 Pulse 주기 (초). 권장: 0.4~0.8.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _groggyPulsePeriod = 0.55f;

        [Tooltip("Groggy Y 스케일 축소 비율. 권장: 0.05~0.15.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _groggySquishY = 0.1f;

        [Tooltip("Groggy 색상 (노랑).")]
        [SerializeField] private Color _groggyColor = new Color(1f, 0.95f, 0.2f, 1f);

        [Header("── DilTime 연출 ──────────────────────")]

        [Tooltip("DilTime 본체 Pulse 주기 (초). 권장: 0.2~0.4.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _dilTimePulsePeriod = 0.28f;

        [Tooltip("DilTime 본체 색상 (주황).")]
        [SerializeField] private Color _dilTimeColor = new Color(1f, 0.5f, 0.1f, 1f);

        [Tooltip("DilTime 코어 Pulse 주기 (초). 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _corePulsePeriod = 0.15f;

        [Header("── Recovery 연출 ──────────────────────")]

        [Tooltip("Recovery Shake 강도. 권장: 0.05~0.15.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _recoveryShakeStrength = 0.08f;

        [Tooltip("Recovery Shake 지속 시간 (초). 권장: 0.3~0.6.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _recoveryShakeDuration = 0.4f;

        [Header("── 피격 연출 ──────────────────────")]

        [Tooltip("피격 흰색 플래시 지속 (초). 권장: 0.08~0.15.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hitFlashDuration = 0.1f;

        [Tooltip("피격 X 흔들림 강도. 권장: 0.1~0.2.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _hitShakeStrength = 0.15f;

        [Header("── 처치 연출 ──────────────────────")]

        [Tooltip("처치 시 Scale 0 까지 Shrink 시간 (초). 권장: 0.4~0.8.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _deathShrinkDuration = 0.5f;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private TestBossCore _core;
        private TestBossAI _ai;

        /// <summary> 본체 원래 색상. 복구에 사용. </summary>
        private Color _defaultBodyColor;

        /// <summary> 본체 원래 Scale. 복구에 사용. </summary>
        private Vector3 _defaultScale;

        // ──────────────────────────────────────────
        // 현재 루프 Tween (Kill 관리)
        // ──────────────────────────────────────────

        /// <summary> 루프 중인 본체 색상 Tween. </summary>
        private Tween _bodyColorLoop;

        /// <summary> 루프 중인 본체 Scale Tween. </summary>
        private Tween _bodyScaleLoop;

        /// <summary> 루프 중인 코어 색상 Tween. </summary>
        private Tween _coreColorLoop;

        /// <summary> Warning 중 X Scale 진동 코루틴. </summary>
        private Coroutine _warnShakeCoroutine;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // 자동 탐색
            if (_bodyRenderer == null)
                _bodyRenderer = GetComponent<SpriteRenderer>();

            _core = GetComponent<TestBossCore>();
            _ai = GetComponent<TestBossAI>();

            if (_bodyRenderer != null)
            {
                _defaultBodyColor = _bodyRenderer.color;
                _defaultScale = transform.localScale;
            }
        }

        private void Start()
        {
            // TestBossCore 이벤트 구독
            if (_core != null)
            {
                _core.OnGroggyEnter += PlayGroggyEnter;
                _core.OnGroggyExit += PlayGroggyExit;
                _core.OnDilTimeEnter += PlayDilTimeEnter;
                _core.OnDilTimeExit += PlayDilTimeExit;
                _core.OnDead += PlayDeath;
                _core.OnHitFeedback += PlayHitFlash;   // ← TestBossCore 에 추가 필요
            }

            // TestBossAI 이벤트 구독
            if (_ai != null)
            {
                _ai.OnStateChanged += HandleStateChanged;  // ← TestBossAI 에 추가 필요
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();

            if (_core != null)
            {
                _core.OnGroggyEnter -= PlayGroggyEnter;
                _core.OnGroggyExit -= PlayGroggyExit;
                _core.OnDilTimeEnter -= PlayDilTimeEnter;
                _core.OnDilTimeExit -= PlayDilTimeExit;
                _core.OnDead -= PlayDeath;
                _core.OnHitFeedback -= PlayHitFlash;
            }

            if (_ai != null)
                _ai.OnStateChanged -= HandleStateChanged;
        }

        private void OnDisable()
        {
            KillAllTweens();
        }

        // ══════════════════════════════════════════════════════
        // TestBossAI 상태 전환 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AI 상태 전환 시 호출.
        /// 상태에 따라 적절한 연출 시작.
        /// </summary>
        private void HandleStateChanged(TestBossAI.TestBossAIState newState,
                                         TestBossPatternBase currentPattern)
        {
            switch (newState)
            {
                case TestBossAI.TestBossAIState.Idle:
                case TestBossAI.TestBossAIState.Chase:
                    PlayIdle();
                    break;

                case TestBossAI.TestBossAIState.Warning:
                    PlayWarning(currentPattern);
                    break;

                case TestBossAI.TestBossAIState.Active:
                    PlayActive(currentPattern);
                    break;

                case TestBossAI.TestBossAIState.Recovery:
                    PlayRecovery();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 연출 — Idle / Chase
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Idle / Chase — 기본 상태 복구.
        /// 모든 Tween Kill 후 원래 색상·Scale 복원.
        /// </summary>
        private void PlayIdle()
        {
            KillAllTweens();
            RestoreDefault();
        }

        // ══════════════════════════════════════════════════════
        // 연출 — Warning
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 진입.
        /// 패턴 타입에 따라 Charge / Stomp 분기.
        /// </summary>
        private void PlayWarning(TestBossPatternBase pattern)
        {
            KillAllTweens();
            RestoreDefault();

            bool isCharge = (pattern is TestBossPattern_Charge);

            if (isCharge)
                PlayWarningCharge();
            else
                PlayWarningStomp();
        }

        /// <summary>
        /// Charge Warning 연출.
        /// X 스케일 진동 + 주황색 Ping-Pong.
        /// "힘을 모은다" — 좌우 압축 진동.
        /// </summary>
        private void PlayWarningCharge()
        {
            if (_bodyRenderer == null) return;

            // 색상 Ping-Pong: 기본색 ↔ 주황
            _bodyColorLoop = _bodyRenderer
                .DOColor(_chargeWarnColor, _chargeWarnShakePeriod)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            // X 스케일 진동: 좌우 압축
            _bodyScaleLoop = transform
                .DOScaleX(_defaultScale.x * (1f - _chargeWarnShakeStrength),
                          _chargeWarnShakePeriod)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutQuad);
        }

        /// <summary>
        /// Stomp Warning 연출.
        /// Y 스케일 팽창 Ping-Pong + 보라색.
        /// "부풀어 오른다" — 위아래로 커짐.
        /// </summary>
        private void PlayWarningStomp()
        {
            if (_bodyRenderer == null) return;

            // 색상 Ping-Pong: 기본색 ↔ 보라
            _bodyColorLoop = _bodyRenderer
                .DOColor(_stompWarnColor, _chargeWarnShakePeriod * 1.3f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            // Y 스케일 팽창
            _bodyScaleLoop = transform
                .DOScaleY(_defaultScale.y * (1f + _stompWarnExpandY),
                          _chargeWarnShakePeriod * 1.3f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutBack);
        }

        // ══════════════════════════════════════════════════════
        // 연출 — Active
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Active 진입.
        /// 패턴 타입에 따라 Charge / Stomp 분기.
        /// </summary>
        private void PlayActive(TestBossPatternBase pattern)
        {
            KillAllTweens();

            bool isCharge = (pattern is TestBossPattern_Charge);

            if (isCharge)
                PlayActiveCharge();
            else
                PlayActiveStomp();
        }

        /// <summary>
        /// Charge Active 연출.
        /// 흰색 플래시 → 붉은색 + X 스케일 늘어남 (속도감).
        /// </summary>
        private void PlayActiveCharge()
        {
            if (_bodyRenderer == null) return;

            // 색상: 흰색 플래시 → 붉은색
            Sequence seq = DOTween.Sequence();
            seq.Append(_bodyRenderer.DOColor(Color.white, 0.06f).SetEase(Ease.OutFlash));
            seq.Append(_bodyRenderer.DOColor(new Color(1f, 0.25f, 0.25f, 1f), 0.12f));

            // X 스케일: 길게 늘어남
            transform.DOScaleX(_defaultScale.x * (1f + _chargeActiveScaleX), 0.08f)
                .SetEase(Ease.OutExpo);
        }

        /// <summary>
        /// Stomp Active 연출.
        /// Scale Punch (터지는 느낌) + 노란 플래시.
        /// </summary>
        private void PlayActiveStomp()
        {
            if (_bodyRenderer == null) return;

            // 색상: 노란색 강한 플래시 → 기본
            Sequence seq = DOTween.Sequence();
            seq.Append(_bodyRenderer.DOColor(Color.white, 0.05f));
            seq.Append(_bodyRenderer.DOColor(new Color(1f, 1f, 0.2f, 1f), 0.05f));
            seq.Append(_bodyRenderer.DOColor(_defaultBodyColor, 0.2f));

            // Scale Punch: 터지는 팝 이펙트
            transform.DOPunchScale(
                Vector3.one * _stompActivePunch,
                0.35f,
                vibrato: 6,
                elasticity: 0.5f);
        }

        // ══════════════════════════════════════════════════════
        // 연출 — Recovery
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Recovery 진입.
        /// Shake Position (미세 진동) + 빨간색 페이드 아웃.
        /// "경직 — 공격 가능 구간" 명확화.
        /// </summary>
        private void PlayRecovery()
        {
            KillAllTweens();

            if (_bodyRenderer == null) return;

            // 색상: 빨간색 설정 후 페이드 아웃
            _bodyRenderer.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            _bodyRenderer.DOColor(_defaultBodyColor, _recoveryShakeDuration * 1.5f)
                .SetEase(Ease.OutCubic);

            // Position Shake: 경직 진동
            transform.DOShakePosition(
                _recoveryShakeDuration,
                strength: new Vector3(_recoveryShakeStrength, _recoveryShakeStrength * 0.5f, 0f),
                vibrato: 18,
                randomness: 60f);

            // Scale 복구
            transform.DOScale(_defaultScale, _recoveryShakeDuration * 0.5f)
                .SetEase(Ease.OutElastic);
        }

        // ══════════════════════════════════════════════════════
        // 연출 — Groggy
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Groggy 진입.
        /// 노란색 느린 Pulse 루프 + Y 스케일 축소.
        /// "처형 가능" 명확히 표시.
        /// </summary>
        private void PlayGroggyEnter()
        {
            KillAllTweens();

            if (_bodyRenderer == null) return;

            // Y 스케일 축소: 축 처진 느낌
            transform.DOScaleY(_defaultScale.y * (1f - _groggySquishY), 0.2f)
                .SetEase(Ease.OutBack);

            // 색상: 노란색 Ping-Pong 루프
            _bodyColorLoop = _bodyRenderer
                .DOColor(_groggyColor, _groggyPulsePeriod)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// Groggy 종료.
        /// 루프 Tween Kill + 기본 상태 복구.
        /// </summary>
        private void PlayGroggyExit()
        {
            KillAllTweens();
            RestoreDefaultTween(duration: 0.2f);
        }

        // ══════════════════════════════════════════════════════
        // 연출 — DilTime
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// DilTime 진입.
        /// 본체 빠른 주황 Pulse 루프 + 코어 Ping-Pong 루프.
        /// "집중 공격 구간" 강조.
        /// </summary>
        private void PlayDilTimeEnter()
        {
            KillAllTweens();

            if (_bodyRenderer != null)
            {
                // 본체: 주황색 빠른 Pulse
                _bodyColorLoop = _bodyRenderer
                    .DOColor(_dilTimeColor, _dilTimePulsePeriod)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutCubic);
            }

            // 코어: 흰색 ↔ 노란색 빠른 깜빡임
            if (_coreRenderer != null)
            {
                _coreColorLoop = _coreRenderer
                    .DOColor(Color.white, _corePulsePeriod)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.Linear);
            }
        }

        /// <summary>
        /// DilTime 종료.
        /// 루프 Tween Kill + 기본 상태 복구.
        /// </summary>
        private void PlayDilTimeExit()
        {
            KillAllTweens();
            RestoreDefaultTween(duration: 0.3f);

            // 코어 색상 복구
            if (_coreRenderer != null)
            {
                _coreRenderer.DOColor(new Color(1f, 0.9f, 0.2f, 1f), 0.3f);
            }
        }

        // ══════════════════════════════════════════════════════
        // 연출 — 피격 (DilTime 중 코어 공격)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 피드백.
        /// 흰색 플래시 + X 위치 흔들림.
        /// TestBossCore.OnHitFeedback 이벤트로 호출.
        /// </summary>
        private void PlayHitFlash()
        {
            if (_bodyRenderer == null) return;

            // 진행 중인 Hit 관련 Tween Kill (루프 Tween 은 유지)
            DOTween.Kill(transform, complete: false);

            // 흰색 플래시 (DilTime 루프 색상 위에 덮어씌움)
            Color before = _bodyRenderer.color;
            Sequence hit = DOTween.Sequence();
            hit.Append(_bodyRenderer.DOColor(Color.white, _hitFlashDuration * 0.5f)
                .SetEase(Ease.OutFlash));
            hit.Append(_bodyRenderer.DOColor(before, _hitFlashDuration * 0.5f));

            // X 위치 흔들림 (넉백 피드백)
            transform.DOShakePosition(
                _hitFlashDuration * 2f,
                strength: new Vector3(_hitShakeStrength, 0f, 0f),
                vibrato: 10,
                randomness: 0f);
        }

        // ══════════════════════════════════════════════════════
        // 연출 — 보스 처치
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 처치 연출.
        /// Scale 0 Shrink + 회색 페이드.
        /// </summary>
        private void PlayDeath()
        {
            KillAllTweens();

            if (_bodyRenderer != null)
                _bodyRenderer.DOColor(Color.gray, _deathShrinkDuration * 0.5f);

            transform.DOScale(Vector3.zero, _deathShrinkDuration)
                .SetEase(Ease.InBack);
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 모든 루프 Tween Kill.
        /// 새 연출 시작 전 반드시 호출.
        /// </summary>
        private void KillAllTweens()
        {
            _bodyColorLoop?.Kill();
            _bodyScaleLoop?.Kill();
            _coreColorLoop?.Kill();

            _bodyColorLoop = null;
            _bodyScaleLoop = null;
            _coreColorLoop = null;

            if (_warnShakeCoroutine != null)
            {
                StopCoroutine(_warnShakeCoroutine);
                _warnShakeCoroutine = null;
            }

            // transform 에 걸린 Tween 도 Kill
            transform.DOKill();
            _bodyRenderer?.DOKill();
            _coreRenderer?.DOKill();
        }

        /// <summary>
        /// 즉시 기본 색상·Scale 복구.
        /// </summary>
        private void RestoreDefault()
        {
            if (_bodyRenderer != null)
                _bodyRenderer.color = _defaultBodyColor;

            transform.localScale = _defaultScale;
        }

        /// <summary>
        /// Tween 으로 부드럽게 기본 색상·Scale 복구.
        /// </summary>
        private void RestoreDefaultTween(float duration = 0.2f)
        {
            if (_bodyRenderer != null)
                _bodyRenderer.DOColor(_defaultBodyColor, duration).SetEase(Ease.OutCubic);

            transform.DOScale(_defaultScale, duration).SetEase(Ease.OutElastic);
        }
    }
}