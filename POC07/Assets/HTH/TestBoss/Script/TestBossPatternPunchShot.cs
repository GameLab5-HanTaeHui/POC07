// ============================================================
// TestBossPattern_PunchShot.cs  v1.1
// 테스트 미니보스 — 주먹2: 수평 날리기 패턴
//
// [v1.1 변경 — 회전 피드백 추가 + 봉인 색상 복구]
//
//   회전 피드백 추가:
//     Warning : 팔이 뒤로 후퇴하면서 DORotate 로 크게 뒤로 젖혀짐
//               (권투 선수가 펀치 날리기 전 팔을 뒤로 당기는 느낌)
//     Active  : 팔이 수평 발사되면서 앞으로 빠르게 회전
//               (주먹이 힘차게 앞으로 뻗어나가는 느낌)
//     Recovery: 팔 원위치 복귀 + 회전 원상복구
//
//   봉인 색상 복구:
//     OnRecovery 완료 후 TestBossArmPart.RestoreArmColor() 호출
//     Interrupt 후도 동일
//
// [DOTween 연출 전체 흐름]
//
//   Warning (1.0초)
//     팔 후퇴  (DOLocalMoveX — 발사 방향 반대로)
//     팔 회전  (DOLocalRotateZ — 뒤로 크게 젖힘)
//     색상 → 파란색 (에너지 집중)
//
//   Active (수평 발사)
//     팔 수평 발사 (DOLocalMoveX — OutExpo)
//     팔 앞으로 회전 복귀 (DOLocalRotateZ — 0도 + 오버슈트)
//     히트박스 활성 → 접촉 시 플레이어 피격
//
//   Recovery (0.8초)
//     팔 원위치 + 회전 원복 (InOutSine)
//     색상 복구 → 봉인 상태 색상 복구
//     OnPatternGroggy 발행 → 그로기 유도
//
// [팔 회전 기준]
//   수평 발사 방향이 +X (오른쪽) 일 때:
//     Warning 뒤로 젖힘: Z -windupRotate (시계 방향 젖힘)
//     Active 앞으로 뻗기: Z +slamOvershoot (반시계 방향 앞으로)
//   수평 발사 방향이 -X (왼쪽) 일 때:
//     방향 반전 적용
//
// [봉인 시 보스 후퇴]
//   기획: "봉인 시 제자리로 돌아가고 플레이어와 거리를 벌릴려고 함"
//   Interrupt() 호출 시 보스 Rigidbody2D 로 후퇴 이동.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 미니보스 주먹2 — 수평 날리기 패턴. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [연출 흐름]
    ///   Warning  : 팔 후퇴 + 뒤로 젖힘 회전 + 파란색
    ///   Active   : 수평 발사 + 앞으로 회전 오버슈트
    ///   Recovery : 원위치 + 회전 복귀 + 봉인 색상 복구 → 그로기
    ///   봉인 시  : 팔 즉시 복귀 + 보스 후퇴
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_PunchShot : TestBossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector — 팔 연결
        // ──────────────────────────────────────────

        [Header("── 팔 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 이 패턴이 사용할 팔 Transform (Arm_L 또는 Arm_R).
        /// </summary>
        [Tooltip("팔 Transform (Arm_L 또는 Arm_R).")]
        [SerializeField] private Transform _armTransform;

        /// <summary>
        /// 팔 SpriteRenderer. 미연결 시 자동 탐색.
        /// </summary>
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;

        /// <summary>
        /// 팔 봉인 상태 컴포넌트. 색상 복구용.
        /// </summary>
        [Tooltip("TestBossArmPart. 색상 복구용. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossArmPart _armPart;

        /// <summary>
        /// 수평 발사 히트박스 (IsTrigger = true).
        /// </summary>
        [Tooltip("수평 발사 히트박스 Collider2D.")]
        [SerializeField] private Collider2D _hitbox;

        [Header("── 보스 후퇴 연결 (선택) ──────────────────────")]

        /// <summary>
        /// 봉인 시 보스 후퇴용 Rigidbody2D.
        /// 미연결 시 루트에서 자동 탐색.
        /// </summary>
        [Tooltip("봉인 시 보스 후퇴 Rigidbody2D. 미연결 시 자동 탐색.")]
        [SerializeField] private Rigidbody2D _bossRigid2D;

        // ──────────────────────────────────────────
        // Inspector — 이동 수치
        // ──────────────────────────────────────────

        [Header("── 이동 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔 후퇴 거리 (localX, 발사 방향 반대).
        /// </summary>
        [Tooltip("Warning 팔 후퇴 거리 (units). 권장: 0.5~1.5.")]
        [Min(0.1f)]
        [SerializeField] private float _windupPullback = 1.0f;

        /// <summary>
        /// Active 시 팔 발사 거리 (localX, 플레이어 방향).
        /// </summary>
        [Tooltip("Active 발사 거리 (units). 권장: 3.0~6.0.")]
        [Min(0.5f)]
        [SerializeField] private float _shotDistance = 4.5f;

        /// <summary>
        /// Active 발사 소요 시간 (초).
        /// </summary>
        [Tooltip("발사 소요 시간 (초). 권장: 0.15~0.3.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _shotDuration = 0.2f;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// </summary>
        [Tooltip("히트박스 활성 유지 (초). 권장: 0.1~0.25.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.18f;

        /// <summary>
        /// 발사 데미지.
        /// </summary>
        [Tooltip("발사 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 12f;

        // ──────────────────────────────────────────
        // Inspector — 회전 수치
        // ──────────────────────────────────────────

        [Header("── 회전 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔이 뒤로 젖혀지는 Z 회전각 (도).
        /// 발사 방향에 따라 부호 자동 결정.
        /// 권투 선수가 팔을 뒤로 당기는 느낌.
        /// </summary>
        [Tooltip("Warning 팔 뒤로 젖힘 각도 (도). 권장: -90.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _windupRotate = -90f;

        /// <summary>
        /// Warning 회전 소요 시간 (초).
        /// </summary>
        [Tooltip("Warning 회전 소요 시간 (초). 권장: 0.25~0.6.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _windupRotateDuration = 0.4f;

        /// <summary>
        /// Active 발사 시 앞으로 오버슈트 회전각 (도).
        /// 주먹이 힘차게 뻗어나가는 느낌 강조.
        /// </summary>
        [Tooltip("Active 발사 오버슈트 회전각 (도). 권장: 90.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _shotOvershoot = 90f;

        // ──────────────────────────────────────────
        // Inspector — 봉인 후퇴
        // ──────────────────────────────────────────

        /// <summary>
        /// 플레이어 감지 레이어.
        /// ★ OverlapBox 로 직접 감지 (OnTriggerEnter2D 오브젝트 불일치 문제).
        /// </summary>
        [Tooltip("플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] private LayerMask _playerLayer;

        [Header("── 봉인 시 후퇴 ──────────────────────")]

        [Tooltip("봉인 시 후퇴 속도 (units/s). 권장: 4~8.")]
        [Min(0f)]
        [SerializeField] private float _retreatSpeed = 5f;

        [Tooltip("봉인 시 후퇴 지속 시간 (초). 권장: 0.3~0.6.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _retreatDuration = 0.4f;

        // ──────────────────────────────────────────
        // Inspector — 색상
        // ──────────────────────────────────────────

        [Header("── 색상 피드백 ──────────────────────")]

        [Tooltip("Warning 팔 색상 (파랑 — 에너지 집중).")]
        [SerializeField] private Color _warningColor = new Color(0.3f, 0.6f, 1.0f, 1f);

        [Tooltip("Active 발사 순간 팔 색상 (흰색 플래시).")]
        [SerializeField] private Color _activeColor = Color.white;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 이 패턴이 사용하는 팔이 실행 가능한지 여부.
        /// TestBossAI.TrySelectPattern() 에서 체크.
        /// </summary>
        public bool IsArmAvailable
            => _armPart == null || _armPart.CanPatternExecute;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private Vector3 _armOriginLocalPos;
        private Vector3 _armOriginLocalEuler;
        private Color _armDefaultColor;

        private Transform _playerTransform;

        /// <summary>
        /// 발사 방향 (+1 = 오른쪽, -1 = 왼쪽).
        /// Warning 시작 시점 플레이어 방향으로 결정.
        /// </summary>
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

            if (_hitbox != null)
                _hitbox.enabled = false;

            if (_bossRigid2D == null)
                _bossRigid2D = GetComponentInParent<Rigidbody2D>();

            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            _triggerGroggyOnRecovery = true;

            // 봉인 감지 대상 팔 등록
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
        /// "수평 발사 예고" 명확한 시각 신호.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            // 발사 방향 결정
            _shotDirection = (_playerTransform != null
                && _playerTransform.position.x > transform.position.x)
                ? 1f : -1f;

            // 색상 → 파란색 (에너지 집중)
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_warningColor, _warningDuration * 0.5f)
                .SetEase(Ease.InSine);

            // 팔 후퇴 (발사 방향 반대)
            float pullbackX = _armOriginLocalPos.x - _windupPullback * _shotDirection;
            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveX(pullbackX, _warningDuration * 0.7f)
                .SetEase(Ease.OutBack);

            // 팔 뒤로 젖힘 회전
            // 오른쪽 발사 → Z 음수 (시계 방향 젖힘)
            // 왼쪽 발사  → Z 양수 (반시계 방향 젖힘)
            float windupZ = _armOriginLocalEuler.z - _windupRotate * _shotDirection;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, windupZ),
                    _windupRotateDuration)
                .SetEase(Ease.OutBack);

            yield return WaitScaled(_warningDuration);
        }

        /// <summary>
        /// Active 단계.
        /// 팔 수평 발사 + 앞으로 회전 오버슈트.
        /// 주먹이 힘차게 뻗어나가는 느낌.
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // 색상 → 흰색 플래시
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_activeColor, 0.05f)
                .SetEase(Ease.OutFlash);

            // 히트박스 활성
            if (_hitbox != null) _hitbox.enabled = true;

            // 수평 발사
            float targetX = _armOriginLocalPos.x + _shotDistance * _shotDirection;
            _moveTween?.Kill();
            bool done = false;
            _moveTween = _armTransform
                .DOLocalMoveX(targetX, _shotDuration)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => done = true);

            // 앞으로 회전 오버슈트
            // 오른쪽 발사 → Z 양수 (반시계 방향 앞으로)
            // 왼쪽 발사  → Z 음수 (시계 방향 앞으로)
            float shotZ = _armOriginLocalEuler.z + _shotOvershoot * _shotDirection;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, shotZ),
                    _shotDuration)
                .SetEase(Ease.OutExpo);

            float elapsed = 0f;
            while (!done && elapsed < _shotDuration + 0.1f)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(_hitboxDuration);
            if (_hitbox != null) _hitbox.enabled = false;
        }

        /// <summary>
        /// Recovery 단계.
        /// 팔 원위치 + 회전 복귀 + 봉인 색상 복구.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            if (_armTransform == null) yield break;

            // 색상 복구 (기본색)
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, _recoveryDuration * 0.6f)
                .SetEase(Ease.OutCubic);

            // 이동 원위치
            _moveTween?.Kill();
            bool done = false;
            _moveTween = _armTransform
                .DOLocalMove(_armOriginLocalPos, _recoveryDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => done = true);

            // 회전 원위치
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

            // ★ 봉인 상태 색상 복구 (해제=붉은색 / 봉인=파란색)
            _armPart?.RestoreArmColor();
        }

        // ══════════════════════════════════════════════════════
        // Interrupt 오버라이드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 강제 중단 (봉인 적중 포함).
        /// 팔 원위치 + 회전 복귀 + 봉인 색상 복구 + 보스 후퇴.
        /// </summary>
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
                    .OnComplete(() => _armPart?.RestoreArmColor()); // ★ 색상 복구
            }

            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, 0.2f);

            // 보스 후퇴
            if (_bossRigid2D != null && _retreatSpeed > 0f)
                StartCoroutine(RetreatRoutine());

            Debug.Log("[TestBossPattern_PunchShot] 중단/봉인 → 팔 원위치 + 보스 후퇴 + 봉인 색상 복구");
        }

        /// <summary>
        /// 보스 후퇴 코루틴.
        /// 플레이어 반대 방향으로 이동.
        /// </summary>
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

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        private void KillArmTweens()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _colorTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 물리 충돌
        // ══════════════════════════════════════════════════════
        // ★ OnTriggerEnter2D 제거.
        //   _hitbox(Arm_R) 와 스크립트(PunchShot) 가 다른 오브젝트.
        //   대신 OnActive() 내부 Physics2D.OverlapBoxAll 로 직접 감지.

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_armTransform == null) return;

            Vector3 shotTarget = _armTransform.position
                + new Vector3(_shotDirection * _shotDistance, 0f, 0f);

            Gizmos.color = new Color(0.3f, 0.6f, 1.0f, 0.4f);
            Gizmos.DrawWireSphere(shotTarget, 0.4f);
            Gizmos.DrawLine(_armTransform.position, shotTarget);
        }
    }
}