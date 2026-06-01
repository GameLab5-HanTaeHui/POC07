// ============================================================
// TestBossPattern_PunchDown.cs  v1.3
// 테스트 미니보스 — 주먹1: 수직 내리찍기 패턴
//
// [v1.3 변경 — ObjectFlipController 반전 대응]
//
//   [문제]
//     _armOriginLocalPos / _armOriginLocalEuler 를 Awake() 에서 한 번만 캐싱.
//     ObjectFlipController 가 Arm_L.localPosition.x 를 반전하면
//     패턴은 여전히 반전 전 좌표로 DOTween → 팔이 반대 방향으로 날아감.
//     _windupRotate 도 부호 고정이라 반전 시 회전 방향도 반대가 됨.
//
//   [수정]
//     SyncOrigin(float dir) 메서드 추가.
//     ObjectFlipController.HandleFlipped() 에서 이 메서드를 호출하면
//     현재 팔 localPosition / localEulerAngles 를 다시 캐싱.
//     _facingDirection 으로 방향 부호를 관리.
//     → _windupRotate, _slamOvershoot 에 _facingDirection 곱하여
//       반전 시 회전 방향도 올바르게 적용.
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
        [Tooltip("Warning 뒤로 젖힘 회전각 (도). 절댓값으로 설정. 방향은 자동 적용.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _windupRotate = 45f;
        [Tooltip("Warning 회전 소요 시간 (초).")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _windupRotateDuration = 0.5f;
        [Tooltip("Active 내리찍기 오버슈트 회전각 (도). 절댓값으로 설정.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _slamOvershoot = 80f;

        [Header("── 색상 피드백 ──────────────────────")]
        [Tooltip("Warning 팔 색상 (주황).")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.55f, 0.1f, 1f);

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

        /// <summary>
        /// 현재 보스 바라보는 방향. +1 = 오른쪽, -1 = 왼쪽.
        /// SyncOrigin() 에서 갱신. 회전 부호 계산에 사용.
        /// </summary>
        private float _facingDirection = 1f;

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

            if (_hitbox != null) _hitbox.enabled = false;

            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            _triggerGroggyOnRecovery = true;
            SetSealableArm(_armPart);
        }

        private void OnDestroy() => KillArmTweens();

        // ══════════════════════════════════════════════════════
        // ★ v1.3: 원점 동기화 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// ObjectFlipController 가 방향 반전 후 이 메서드를 호출.
        /// 현재 팔의 localPosition / localEulerAngles 를 원점으로 재캐싱.
        /// 이후 DOTween 이동/회전이 반전된 좌표 기준으로 동작.
        /// </summary>
        /// <param name="dir">+1 = 오른쪽, -1 = 왼쪽.</param>
        public void SyncOrigin(float dir)
        {
            _facingDirection = dir;

            if (_armTransform != null)
            {
                _armOriginLocalPos = _armTransform.localPosition;
                _armOriginLocalEuler = _armTransform.localEulerAngles;
            }
        }

        // ══════════════════════════════════════════════════════
        // 3단계 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계.
        /// 팔 위로 상승 + 뒤로 젖힘 회전 + 주황색.
        /// _facingDirection 으로 회전 부호 결정.
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

            // ★ v1.3: _facingDirection 으로 회전 방향 결정
            float targetZ = _armOriginLocalEuler.z + _windupRotate * _facingDirection;
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
        /// 팔 빠르게 아래로 내리찍기.
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            if (_hitbox != null) _hitbox.enabled = true;

            float targetY = _armOriginLocalPos.y - _slamDepth;

            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveY(targetY, _slamDuration)
                .SetEase(Ease.OutExpo);

            // ★ v1.3: _facingDirection 으로 오버슈트 회전 방향 결정
            float slamTargetZ = _armOriginLocalEuler.z - _slamOvershoot * _facingDirection;
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

            if (_hitbox != null) _hitbox.enabled = false;
        }

        /// <summary>
        /// Recovery 단계.
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

        public override void Interrupt()
        {
            base.Interrupt();
            KillArmTweens();

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

            _colorTween = _armRenderer?.DOColor(_armDefaultColor, 0.2f);
        }

        private void KillArmTweens()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _colorTween?.Kill();
        }
    }
}