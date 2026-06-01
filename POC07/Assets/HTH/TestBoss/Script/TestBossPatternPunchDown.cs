// ============================================================
// TestBossPattern_PunchDown.cs  v1.2
// 테스트 미니보스 — 주먹1: 수직 내리찍기 패턴
//
// [v1.2 변경 — OnActive() 히트박스 활성화 추가]
//
//   [기존 v1.1 문제]
//     Awake()     : _hitbox.enabled = false (초기화)
//     Interrupt() : _hitbox.enabled = false (중단 시 OFF)
//     OnActive()  : _hitbox.enabled = true 코드 없음 ← 누락
//
//     결과: 게임 시작 후 첫 패턴은 Prefab 기본값(m_Enabled:1)으로 동작
//           Interrupt() 한 번이라도 호출되면 _hitbox가 꺼진 채 유지
//           이후 모든 패턴에서 히트박스 비활성 → 피격 불가
//
//   [v1.2 수정]
//     OnActive() 시작 시 → _hitbox.enabled = true
//     OnActive() 종료 시 → _hitbox.enabled = false
//     Interrupt()        → _hitbox.enabled = false (유지)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace KEY
{
    public class TestBossPattern_PunchDown : TestBossPatternBase
    {
        [Header("── 팔 연결 (필수) ──────────────────────")]
        [Tooltip("팔 Transform (Arm_L).")]
        [SerializeField] private Transform _armTransform;
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;
        [Tooltip("TestBossArmPart. 색상 복구용. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossArmPart _armPart;
        [Tooltip("내리찍기 히트박스 Collider2D.")]
        [SerializeField] protected Collider2D _hitbox;

        [Header("── 내리찍기 수치 ──────────────────────")]
        [Tooltip("팔이 올라가는 높이 (units).")]
        [Min(0f)]
        [SerializeField] private float _windupHeight = 2.5f;
        [Tooltip("팔이 내려찍는 깊이 (units).")]
        [Min(0f)]
        [SerializeField] private float _slamDepth = 2.5f;
        [Tooltip("내리찍기 이동 시간 (초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _slamDuration = 0.2f;
        [Tooltip("히트박스 활성 유지 시간 (초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.2f;
        [Tooltip("피격 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 15f;
        [Tooltip("Warning 회전 각도 (도).")]
        [Range(-90f, 90f)]
        [SerializeField] private float _windupRotate = -45f;
        [Tooltip("Warning 회전 소요 시간 (초).")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _windupRotateDuration = 0.5f;
        [Tooltip("Active 내리찍기 오버슈트 회전각 (도).")]
        [Range(-90f, 90f)]
        [SerializeField] private float _slamOvershoot = 80f;

        [Header("── 색상 피드백 ──────────────────────")]
        [Tooltip("Warning 팔 색상 (주황).")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.55f, 0.1f, 1f);

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────
        public bool IsArmAvailable
            => _armPart == null || _armPart.CanPatternExecute;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────
        private Vector3 _armOriginLocalPos;
        private Vector3 _armOriginLocalEuler;
        private Color _armDefaultColor;
        private Transform _playerTransform;
        private Tween _moveTween;
        private Tween _rotateTween;
        private Tween _colorTween;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_armTransform != null)
            {
                _armOriginLocalPos = _armTransform.localPosition;
                _armOriginLocalEuler = _armTransform.localEulerAngles;
            }

            if (_armRenderer == null && _armTransform != null)
                _armRenderer = _armTransform.GetComponent<SpriteRenderer>();

            if (_armRenderer != null)
                _armDefaultColor = _armRenderer.color;

            if (_armPart == null && _armTransform != null)
                _armPart = _armTransform.GetComponent<TestBossArmPart>();

            // ★ 시작 시 히트박스 비활성화 — Active 시작 전까지 OFF 유지
            if (_hitbox != null) _hitbox.enabled = false;

            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            _triggerGroggyOnRecovery = true;
            SetSealableArm(_armPart);
        }

        private void OnDestroy()
        {
            KillArmTweens();
        }

        // ══════════════════════════════════════════════════════
        // 3단계 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계.
        /// 팔 위로 상승 + 뒤로 젖힘 회전 + 주황색.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_warningColor, _warningDuration * 0.4f)
                .SetEase(Ease.InSine);

            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveY(_armOriginLocalPos.y + _windupHeight, _warningDuration * 0.8f)
                .SetEase(Ease.OutCubic);

            float targetZ = _armOriginLocalEuler.z + _windupRotate;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, targetZ),
                    _windupRotateDuration)
                .SetEase(Ease.OutBack);

            yield return WaitScaled(_warningDuration);
        }

        /// <summary>
        /// Active 단계.
        /// 팔 빠르게 아래로 내리찍기 + 앞으로 회전 오버슈트.
        ///
        /// [v1.2 수정]
        ///   OnActive() 시작 시 _hitbox.enabled = true
        ///   OnActive() 종료 시 _hitbox.enabled = false
        ///   → Interrupt() 호출 후에도 다음 패턴에서 정상 활성화 보장
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // ★ v1.2: 히트박스 활성화
            if (_hitbox != null) _hitbox.enabled = true;

            float targetY = _armOriginLocalPos.y - _slamDepth;

            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveY(targetY, _slamDuration)
                .SetEase(Ease.OutExpo);

            float slamTargetZ = _armOriginLocalEuler.z - _slamOvershoot;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, slamTargetZ),
                    _slamDuration)
                .SetEase(Ease.OutExpo);

            Debug.Log("[TestBossPattern_PunchDown] Active — 내리찍기 시작 (HitBox ON)");

            float elapsed = 0f;
            float totalWait = _slamDuration + _hitboxDuration;
            while (elapsed < totalWait)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ★ v1.2: 히트박스 비활성화
            if (_hitbox != null) _hitbox.enabled = false;

            Debug.Log("[TestBossPattern_PunchDown] Active — 내리찍기 종료 (HitBox OFF)");
        }

        /// <summary>
        /// Recovery 단계.
        /// 팔 원위치 + 원래 회전각 복귀 + 색상 복구.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            if (_armTransform == null) yield break;

            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, _recoveryDuration * 0.6f)
                .SetEase(Ease.OutCubic);

            _moveTween?.Kill();
            bool done = false;
            _moveTween = _armTransform
                .DOLocalMove(_armOriginLocalPos, _recoveryDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => done = true);

            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(_armOriginLocalEuler, _recoveryDuration)
                .SetEase(Ease.InOutSine);

            float elapsed = 0f;
            while (!done && elapsed < _recoveryDuration + 0.1f)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            _armPart?.RestoreArmColor();
        }

        // ══════════════════════════════════════════════════════
        // Interrupt 오버라이드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 강제 중단.
        /// 히트박스 즉시 OFF + 팔 원위치 복귀.
        /// </summary>
        public override void Interrupt()
        {
            base.Interrupt();
            KillArmTweens();

            // ★ 중단 시 즉시 히트박스 OFF
            if (_hitbox != null) _hitbox.enabled = false;

            if (_armTransform != null)
            {
                _moveTween = _armTransform
                    .DOLocalMove(_armOriginLocalPos, 0.3f)
                    .SetEase(Ease.OutBack);

                _rotateTween = _armTransform
                    .DOLocalRotate(_armOriginLocalEuler, 0.25f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => _armPart?.RestoreArmColor());
            }

            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, 0.2f);

            Debug.Log("[TestBossPattern_PunchDown] 중단 → 팔 원위치 + HitBox OFF");
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        private void KillArmTweens()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _colorTween?.Kill();
        }
    }
}