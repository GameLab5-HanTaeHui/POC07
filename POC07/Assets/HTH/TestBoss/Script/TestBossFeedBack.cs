// ============================================================
// TestBossFeedback.cs  v1.1
// 테스트 보스 DOTween 시각 피드백 — 보스 본체 전용
//
// [v1.1 변경]
//   Charge / Stomp 패턴 전용 연출 완전 제거.
//   PunchDown / PunchShot 패턴 연출은 각 패턴 스크립트가 직접 팔 DOTween 처리.
//   이 스크립트는 보스 본체(Body) 만 담당.
//
// [본체 담당 상태별 연출]
//
//   Idle / Chase
//     → 기본 색상 복구, 모든 Tween 없음
//
//   Warning (패턴 예고)
//     → 본체 살짝 Scale 진동 (Punch 예고 느낌)
//     → 색상: 기본색 ↔ 연한 주황 Ping-Pong
//
//   Active (패턴 시전)
//     → 흰 플래시 → 빠르게 복구
//
//   Recovery (후딜레이)
//     → 본체 Shake + 빨간 페이드 아웃
//     → "공격 가능 구간" 신호
//
//   Groggy (처형 가능)
//     → 노란색 느린 Pulse 루프 + Y Scale 축소
//
//   DilTime (집중 공격)
//     → 주황 빠른 Pulse 루프 + 코어 Ping-Pong
//
//   피격 (딜타임 중 코어 맞음)
//     → 흰 플래시 + X 흔들림
//
//   사망
//     → Scale 0 Shrink + 회색
//
// [팔 색상은 TestBossArmPart 가 담당]
//   봉인 = 파란색 / 해제 = 붉은색 — ArmPart 에서 상시 관리.
//   패턴 DOTween 종료 후 ArmPart.RestoreArmColor() 가 자동 복구.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using DG.Tweening;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 보디 DOTween 피드백 컴포넌트. (v1.1)
    /// 팔 연출은 각 패턴 스크립트가 직접 처리.
    /// 이 컴포넌트는 보스 본체 + 코어만 담당.
    /// </summary>
    public class TestBossFeedback : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector — 렌더러 연결
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

        // ──────────────────────────────────────────
        // Inspector — 연출 수치
        // ──────────────────────────────────────────

        [Header("── Warning 연출 (본체) ──────────────────────")]

        /// <summary>
        /// Warning 시 본체 Scale 진동 강도.
        /// </summary>
        [Tooltip("Warning 본체 Scale 진동 강도. 권장: 0.05~0.15.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _warnBodyPunch = 0.08f;

        /// <summary>
        /// Warning 시 본체 색상 Ping-Pong 주기.
        /// </summary>
        [Tooltip("Warning 색상 Ping-Pong 주기 (초). 권장: 0.2~0.4.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _warnColorPeriod = 0.28f;

        /// <summary>
        /// Warning 시 본체 색상 (연한 주황).
        /// </summary>
        [Tooltip("Warning 본체 색상.")]
        [SerializeField] private Color _warnBodyColor = new Color(1f, 0.75f, 0.4f, 1f);

        [Header("── Recovery 연출 ──────────────────────")]

        [Tooltip("Recovery Shake 강도. 권장: 0.05~0.15.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _recoveryShakeStrength = 0.08f;

        [Tooltip("Recovery Shake 지속 시간 (초). 권장: 0.3~0.6.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _recoveryShakeDuration = 0.4f;

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

        [Tooltip("DilTime 본체 Pulse 주기 (초). 권장: 0.2~0.35.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _dilTimePulsePeriod = 0.28f;

        [Tooltip("DilTime 본체 색상 (주황).")]
        [SerializeField] private Color _dilTimeColor = new Color(1f, 0.5f, 0.1f, 1f);

        [Tooltip("DilTime 코어 Pulse 주기 (초). 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _corePulsePeriod = 0.15f;

        [Header("── 피격 연출 ──────────────────────")]

        [Tooltip("피격 흰색 플래시 지속 (초). 권장: 0.08~0.15.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hitFlashDuration = 0.1f;

        [Tooltip("피격 X 흔들림 강도. 권장: 0.1~0.2.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _hitShakeStrength = 0.15f;

        [Header("── 사망 연출 ──────────────────────")]

        [Tooltip("사망 Shrink 시간 (초). 권장: 0.4~0.8.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _deathShrinkDuration = 0.5f;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private TestBossCore _core;
        private TestBossAI _ai;

        private Color _defaultBodyColor;
        private Vector3 _defaultScale;

        // ──────────────────────────────────────────
        // 루프 Tween 핸들
        // ──────────────────────────────────────────

        private Tween _bodyColorLoop;
        private Tween _bodyScaleLoop;
        private Tween _coreColorLoop;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
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
            if (_core != null)
            {
                _core.OnGroggyEnter += PlayGroggyEnter;
                _core.OnGroggyExit += PlayGroggyExit;
                _core.OnDilTimeEnter += PlayDilTimeEnter;
                _core.OnDilTimeExit += PlayDilTimeExit;
                _core.OnDead += PlayDeath;
                _core.OnHitFeedback += PlayHitFlash;
            }

            if (_ai != null)
                _ai.OnStateChanged += HandleStateChanged;
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

        private void OnDisable() => KillAllTweens();

        // ══════════════════════════════════════════════════════
        // AI 상태 전환 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AI 상태 전환 → 보스 본체 연출.
        /// 팔 연출은 각 패턴 스크립트가 처리.
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
                    PlayWarning();
                    break;

                case TestBossAI.TestBossAIState.Active:
                    PlayActive();
                    break;

                case TestBossAI.TestBossAIState.Recovery:
                    PlayRecovery();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 본체 연출
        // ══════════════════════════════════════════════════════

        /// <summary> Idle / Chase — 기본 상태 복구. </summary>
        private void PlayIdle()
        {
            KillAllTweens();
            RestoreDefault();
        }

        /// <summary>
        /// Warning — 본체 Scale 진동 + 연한 주황 Ping-Pong.
        /// "패턴 준비 중" 신호. 팔 연출은 패턴 스크립트 담당.
        /// </summary>
        private void PlayWarning()
        {
            KillAllTweens();
            RestoreDefault();

            if (_bodyRenderer == null) return;

            // 색상 Ping-Pong: 기본색 ↔ 연한 주황
            _bodyColorLoop = _bodyRenderer
                .DOColor(_warnBodyColor, _warnColorPeriod)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            // 본체 Scale 미세 진동 (PunchScale)
            transform.DOPunchScale(
                Vector3.one * _warnBodyPunch,
                _warnColorPeriod * 2f,
                vibrato: 4,
                elasticity: 0.3f);
        }

        /// <summary>
        /// Active — 흰 플래시 후 빠르게 복구.
        /// 실제 공격 연출은 팔(패턴 스크립트) 담당.
        /// </summary>
        private void PlayActive()
        {
            KillAllTweens();

            if (_bodyRenderer == null) return;

            Sequence seq = DOTween.Sequence();
            seq.Append(_bodyRenderer.DOColor(Color.white, 0.05f).SetEase(Ease.OutFlash));
            seq.Append(_bodyRenderer.DOColor(_defaultBodyColor, 0.15f).SetEase(Ease.OutCubic));
        }

        /// <summary>
        /// Recovery — Shake + 빨간 페이드 아웃.
        /// "공격 가능 구간" 명확화.
        /// </summary>
        private void PlayRecovery()
        {
            KillAllTweens();

            if (_bodyRenderer == null) return;

            _bodyRenderer.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            _bodyRenderer.DOColor(_defaultBodyColor, _recoveryShakeDuration * 1.5f)
                .SetEase(Ease.OutCubic);

            transform.DOShakePosition(
                _recoveryShakeDuration,
                strength: new Vector3(_recoveryShakeStrength, _recoveryShakeStrength * 0.5f, 0f),
                vibrato: 18,
                randomness: 60f);

            transform.DOScale(_defaultScale, _recoveryShakeDuration * 0.5f)
                .SetEase(Ease.OutElastic);
        }

        // ══════════════════════════════════════════════════════
        // Groggy / DilTime / 피격 / 사망
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Groggy 진입 — 노란 느린 Pulse + Y 축소.
        /// "처형 가능" 명확히 표시.
        /// </summary>
        private void PlayGroggyEnter()
        {
            KillAllTweens();

            if (_bodyRenderer == null) return;

            transform.DOScaleY(_defaultScale.y * (1f - _groggySquishY), 0.2f)
                .SetEase(Ease.OutBack);

            _bodyColorLoop = _bodyRenderer
                .DOColor(_groggyColor, _groggyPulsePeriod)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        /// <summary> Groggy 종료 — 복구. </summary>
        private void PlayGroggyExit()
        {
            KillAllTweens();
            RestoreDefaultTween(0.2f);
        }

        /// <summary>
        /// DilTime 진입 — 주황 빠른 Pulse + 코어 Ping-Pong.
        /// "집중 공격 구간" 강조.
        /// </summary>
        private void PlayDilTimeEnter()
        {
            KillAllTweens();

            if (_bodyRenderer != null)
            {
                _bodyColorLoop = _bodyRenderer
                    .DOColor(_dilTimeColor, _dilTimePulsePeriod)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutCubic);
            }

            if (_coreRenderer != null)
            {
                _coreColorLoop = _coreRenderer
                    .DOColor(Color.white, _corePulsePeriod)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.Linear);
            }
        }

        /// <summary> DilTime 종료 — 복구. </summary>
        private void PlayDilTimeExit()
        {
            KillAllTweens();
            RestoreDefaultTween(0.3f);

            if (_coreRenderer != null)
                _coreRenderer.DOColor(new Color(1f, 0.9f, 0.2f, 1f), 0.3f);
        }

        /// <summary>
        /// 피격 — 흰 플래시 + X 흔들림.
        /// TestBossCore.OnHitFeedback 이벤트로 호출.
        /// </summary>
        private void PlayHitFlash()
        {
            if (_bodyRenderer == null) return;

            transform.DOKill(complete: false);

            Color before = _bodyRenderer.color;
            Sequence hit = DOTween.Sequence();
            hit.Append(_bodyRenderer.DOColor(Color.white, _hitFlashDuration * 0.5f)
                .SetEase(Ease.OutFlash));
            hit.Append(_bodyRenderer.DOColor(before, _hitFlashDuration * 0.5f));

            transform.DOShakePosition(
                _hitFlashDuration * 2f,
                strength: new Vector3(_hitShakeStrength, 0f, 0f),
                vibrato: 10,
                randomness: 0f);
        }

        /// <summary>
        /// 사망 — Scale 0 Shrink + 회색.
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

        private void KillAllTweens()
        {
            _bodyColorLoop?.Kill();
            _bodyScaleLoop?.Kill();
            _coreColorLoop?.Kill();

            _bodyColorLoop = null;
            _bodyScaleLoop = null;
            _coreColorLoop = null;

            transform.DOKill();
            _bodyRenderer?.DOKill();
            _coreRenderer?.DOKill();
        }

        private void RestoreDefault()
        {
            if (_bodyRenderer != null)
                _bodyRenderer.color = _defaultBodyColor;
            transform.localScale = _defaultScale;
        }

        private void RestoreDefaultTween(float duration = 0.2f)
        {
            if (_bodyRenderer != null)
                _bodyRenderer.DOColor(_defaultBodyColor, duration).SetEase(Ease.OutCubic);
            transform.DOScale(_defaultScale, duration).SetEase(Ease.OutElastic);
        }
    }
}