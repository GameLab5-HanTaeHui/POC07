// ============================================================
// TestBossPattern_PunchShot.cs  v1.2
// 테스트 미니보스 — 주먹2: 수평 날리기 패턴
//
// [v1.2 변경 — OnActive() 히트박스 활성화 추가]
//
//   [기존 v1.1 문제]
//     Awake()     : _hitbox.enabled = false (초기화)
//     Interrupt() : _hitbox.enabled = false (중단 시 OFF)
//     OnActive()  : _hitbox.enabled = true 코드 없음 ← 누락
//
//     결과: Interrupt() 호출 후 히트박스가 꺼진 채 유지
//           이후 모든 패턴에서 피격 불가
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
    public class TestBossPattern_PunchShot : TestBossPatternBase
    {
        [Header("── 팔 연결 (필수) ──────────────────────")]
        [Tooltip("팔 Transform (Arm_R).")]
        [SerializeField] private Transform _armTransform;
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;
        [Tooltip("TestBossArmPart. 색상 복구용. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossArmPart _armPart;
        [Tooltip("수평 발사 히트박스 Collider2D.")]
        [SerializeField] protected Collider2D _hitbox;
        [Tooltip("보스 루트 Rigidbody2D. 후퇴 이동에 사용.")]
        [SerializeField] private Rigidbody2D _bossRigid2D;

        [Header("── 수평 발사 수치 ──────────────────────")]
        [Tooltip("발사 전 팔 후퇴 거리 (units).")]
        [Min(0f)]
        [SerializeField] private float _windupPullback = 1f;
        [Tooltip("팔 발사 거리 (units).")]
        [Min(0f)]
        [SerializeField] private float _shotDistance = 6f;
        [Tooltip("발사 이동 시간 (초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _shotDuration = 0.2f;
        [Tooltip("히트박스 활성 유지 시간 (초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.18f;
        [Tooltip("피격 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 12f;
        [Tooltip("Warning 회전 각도 (도).")]
        [Range(-90f, 90f)]
        [SerializeField] private float _windupRotate = -90f;
        [Tooltip("Warning 회전 소요 시간 (초).")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _windupRotateDuration = 0.4f;
        [Tooltip("Active 발사 오버슈트 회전각 (도).")]
        [Range(-180f, 180f)]
        [SerializeField] private float _shotOvershoot = 90f;
        [Tooltip("후퇴 속도 (units/s).")]
        [Min(0f)]
        [SerializeField] private float _retreatSpeed = 5f;
        [Tooltip("후퇴 지속 시간 (초).")]
        [Min(0f)]
        [SerializeField] private float _retreatDuration = 0.4f;

        [Header("── 색상 피드백 ──────────────────────")]
        [Tooltip("Active 팔 색상 (흰색 플래시).")]
        [SerializeField] private Color _activeColor = Color.white;

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
        private float _shotDirection;
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

            // ★ 시작 시 히트박스 비활성화
            if (_hitbox != null) _hitbox.enabled = false;

            if (_bossRigid2D == null)
                _bossRigid2D = GetComponentInParent<Rigidbody2D>();

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
        /// 팔 후퇴 + 뒤로 젖힘 회전 + 파란색.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            _shotDirection = (_playerTransform != null
                && _playerTransform.position.x > transform.position.x)
                ? 1f : -1f;

            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(new Color(0.4f, 0.6f, 1f, 1f), _warningDuration * 0.4f)
                .SetEase(Ease.InSine);

            float pullbackX = _armOriginLocalPos.x - _windupPullback * _shotDirection;
            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveX(pullbackX, _warningDuration * 0.7f)
                .SetEase(Ease.OutCubic);

            float targetZ = _armOriginLocalEuler.z + _windupRotate * _shotDirection;
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
        /// 팔 수평 발사 + 앞으로 회전 오버슈트.
        ///
        /// [v1.2 수정]
        ///   OnActive() 시작 시 _hitbox.enabled = true
        ///   OnActive() 종료 시 _hitbox.enabled = false
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // ★ v1.2: 히트박스 활성화
            if (_hitbox != null) _hitbox.enabled = true;

            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_activeColor, 0.05f)
                .SetEase(Ease.OutFlash);

            float targetX = _armOriginLocalPos.x + _shotDistance * _shotDirection;
            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveX(targetX, _shotDuration)
                .SetEase(Ease.OutExpo);

            float shotZ = _armOriginLocalEuler.z + _shotOvershoot * _shotDirection;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, shotZ),
                    _shotDuration)
                .SetEase(Ease.OutExpo);

            Debug.Log("[TestBossPattern_PunchShot] Active — 수평 발사 시작 (HitBox ON)");

            float elapsed = 0f;
            float totalWait = _shotDuration + _hitboxDuration;
            while (elapsed < totalWait)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ★ v1.2: 히트박스 비활성화
            if (_hitbox != null) _hitbox.enabled = false;

            Debug.Log("[TestBossPattern_PunchShot] Active — 수평 발사 종료 (HitBox OFF)");
        }

        /// <summary>
        /// Recovery 단계.
        /// 팔 원위치 + 회전 복귀 + 봉인 색상 복구.
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
        /// 히트박스 즉시 OFF + 팔 원위치 복귀 + 보스 후퇴.
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

            if (_bossRigid2D != null && _retreatSpeed > 0f)
                StartCoroutine(RetreatRoutine());

            Debug.Log("[TestBossPattern_PunchShot] 중단 → 팔 원위치 + HitBox OFF + 보스 후퇴");
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        private IEnumerator RetreatRoutine()
        {
            float retreatDir = -_shotDirection;
            float elapsed = 0f;
            while (elapsed < _retreatDuration)
            {
                if (_bossRigid2D == null) yield break;
                _bossRigid2D.linearVelocity = new Vector2(
                    retreatDir * _retreatSpeed,
                    _bossRigid2D.linearVelocity.y);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (_bossRigid2D != null)
                _bossRigid2D.linearVelocity = new Vector2(0f, _bossRigid2D.linearVelocity.y);
        }

        private void KillArmTweens()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _colorTween?.Kill();
        }
    }
}