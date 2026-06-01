// ============================================================
// TestBossPattern_PunchDown.cs  v1.1
// 테스트 미니보스 — 주먹1: 수직 내리찍기 패턴
//
// [v1.1 변경 — 회전 피드백 추가 + 봉인 색상 복구]
//
//   회전 피드백 추가:
//     Warning : 팔이 위로 올라가면서 DORotate 로 뒤로 젖혀짐
//               (회전 방향: 팔이 내리찍기 전 크게 뒤로 젖히는 모션)
//     Active  : 팔이 빠르게 아래로 내려찍으면서 앞으로 회전
//               (OutExpo — 망치처럼 내려치는 느낌)
//     Recovery: 팔이 원위치 복귀하면서 회전도 원상복구
//
//   봉인 색상 복구:
//     OnRecovery 완료 후 TestBossArmPart.RestoreArmColor() 호출
//     → 해제=붉은색 / 봉인=파란색 원상복구
//     Interrupt 후도 동일하게 복구
//
// [DOTween 연출 전체 흐름]
//
//   Warning (1.2초)
//     팔 위로 상승  (DOLocalMoveY + windupHeight)
//     팔 뒤로 젖힘 (DOLocalRotateZ + windupRotate — 뒤로 기울기)
//     팔 색상 → 주황색 (DOColor)
//
//   Active (내리찍기)
//     팔 아래로 빠르게 하강 (DOLocalMoveY — OutExpo)
//     팔 앞으로 회전 (DOLocalRotateZ — 0도 복귀)
//     히트박스 활성 → 접촉 시 플레이어 TakeDamage
//
//   Recovery (0.6초)
//     팔 원위치 + 원래 회전각 복귀 (InOutSine)
//     팔 색상 복구 (DOColor → 기본색)
//     TestBossArmPart.RestoreArmColor() → 봉인 색상 복구
//     OnPatternGroggy 발행 → AI → EnterGroggy
//
// [팔 회전 기준]
//   Arm_L : 오른쪽에서 보면 시계 반대 방향 젖힘 → 시계 방향 찍기
//   localEulerAngles Z 축 기준:
//     원위치   : 0도
//     위로 젖힘: +windupRotate (뒤로 기울기)
//     내리찍기 : 0도 복귀 (앞으로 회전하며 내리찍는 느낌)
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
    /// 테스트 미니보스 주먹1 — 수직 내리찍기 패턴. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [연출 흐름]
    ///   Warning  : 팔 위 상승 + 뒤로 젖힘 회전 + 주황색
    ///   Active   : 팔 아래로 빠른 내리찍기 + 앞으로 회전 복귀
    ///   Recovery : 팔 원위치 + 회전 복귀 + 봉인 색상 복구 → 그로기
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_PunchDown : TestBossPatternBase
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
        /// 팔 SpriteRenderer. 색상 피드백 연출.
        /// 미연결 시 _armTransform 에서 자동 탐색.
        /// </summary>
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;

        /// <summary>
        /// 팔 봉인 상태 컴포넌트.
        /// Recovery 완료 후 RestoreArmColor() 로 봉인 색상 복구.
        /// </summary>
        [Tooltip("TestBossArmPart. 색상 복구용. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossArmPart _armPart;

        /// <summary>
        /// 내리찍기 히트박스 Collider2D (IsTrigger = true).
        /// Active 구간에서만 활성화.
        /// </summary>
        [Tooltip("내리찍기 히트박스 Collider2D. IsTrigger 필요.")]
        [SerializeField] private Collider2D _hitbox;

        // ──────────────────────────────────────────
        // Inspector — 연출 수치
        // ──────────────────────────────────────────

        [Header("── 이동 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔이 위로 올라가는 거리 (localY).
        /// </summary>
        [Tooltip("Warning 팔 상승 거리 (units). 권장: 1.5~3.0.")]
        [Min(0.1f)]
        [SerializeField] private float _windupHeight = 2.5f;

        /// <summary>
        /// Active 시 팔이 내려찍는 거리 (localY 하강).
        /// </summary>
        [Tooltip("Active 팔 하강 거리 (units). 권장: 2.0~4.0.")]
        [Min(0.1f)]
        [SerializeField] private float _slamDepth = 3.5f;

        /// <summary>
        /// Active 내리찍기 소요 시간 (초).
        /// </summary>
        [Tooltip("내리찍기 소요 시간 (초). 권장: 0.15~0.35.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _slamDuration = 0.2f;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// </summary>
        [Tooltip("히트박스 활성 유지 (초). 권장: 0.1~0.3.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.2f;

        /// <summary>
        /// 내리찍기 데미지.
        /// </summary>
        [Tooltip("내리찍기 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 15f;

        [Header("── 회전 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔이 뒤로 젖혀지는 Z 회전각 (도).
        /// 양수 = 반시계 방향 (Arm_L 기준 뒤로 젖힘).
        /// 팔이 내리찍기 직전 크게 들어올리는 느낌.
        /// </summary>
        [Tooltip("Warning 팔 뒤로 젖힘 각도 (도). 권장: -35~-45.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _windupRotate = -35f;

        /// <summary>
        /// Warning 회전 소요 시간 (초).
        /// </summary>
        [Tooltip("Warning 회전 소요 시간 (초). 권장: 0.3~0.8.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float _windupRotateDuration = 0.5f;

        /// <summary>
        /// Active 내리찍기 회전 방향 오버슈트 (도).
        /// 0도로 복귀할 때 살짝 앞으로 더 회전하는 오버슈트.
        /// 망치처럼 세게 내려치는 느낌 강조.
        /// </summary>
        [Tooltip("Active 내리찍기 오버슈트 회전각 (도). 권장: 70~90.")]
        [Range(-90f, 90f)]
        [SerializeField] private float _slamOvershoot = 80f;

        [Header("── 색상 피드백 ──────────────────────")]

        [Tooltip("Warning 팔 색상 (주황).")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.55f, 0.1f, 1f);

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 이 패턴이 사용하는 팔이 실행 가능한지 여부.
        /// _armPart.CanPatternExecute == false (투사체 봉인 중) 이면 false.
        /// TestBossAI.TrySelectPattern() 에서 체크.
        /// </summary>
        public bool IsArmAvailable
            => _armPart == null || _armPart.CanPatternExecute;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 팔 로컬 원위치. Awake 에서 저장. </summary>
        private Vector3 _armOriginLocalPos;

        /// <summary> 팔 로컬 원래 회전각. Awake 에서 저장. </summary>
        private Vector3 _armOriginLocalEuler;

        /// <summary> 팔 기본 색상. </summary>
        private Color _armDefaultColor;

        /// <summary> 플레이어 Transform. </summary>
        private Transform _playerTransform;

        /// <summary> 현재 Tween 핸들. </summary>
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

            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            _triggerGroggyOnRecovery = true;

            // 봉인 감지 대상 팔 등록 (베이스 클래스 WaitScaled 에서 체크)
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
        /// "내리찍기 준비" 명확한 시각 신호.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            // 색상 → 주황색
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_warningColor, _warningDuration * 0.4f)
                .SetEase(Ease.InSine);

            // 팔 위로 상승
            _moveTween?.Kill();
            _moveTween = _armTransform
                .DOLocalMoveY(_armOriginLocalPos.y + _windupHeight, _warningDuration * 0.8f)
                .SetEase(Ease.OutCubic);

            // 팔 뒤로 젖힘 회전 (Z축 — 크게 들어올리는 느낌)
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
        /// 망치처럼 세게 내려치는 느낌.
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // 히트박스 활성
            if (_hitbox != null) _hitbox.enabled = true;

            float targetY = _armOriginLocalPos.y - _slamDepth;

            // 내리찍기 이동 (OutExpo — 망치 내리치기)
            _moveTween?.Kill();
            bool moveDone = false;
            _moveTween = _armTransform
                .DOLocalMoveY(targetY, _slamDuration)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => moveDone = true);

            // 앞으로 회전 오버슈트 (-overshooot → 앞으로 더 꺾임)
            float slamTargetZ = _armOriginLocalEuler.z - _slamOvershoot;
            _rotateTween?.Kill();
            _rotateTween = _armTransform
                .DOLocalRotate(
                    new Vector3(_armOriginLocalEuler.x, _armOriginLocalEuler.y, slamTargetZ),
                    _slamDuration)
                .SetEase(Ease.OutExpo);

            // 내리찍기 완료 대기
            float elapsed = 0f;
            while (!moveDone && elapsed < _slamDuration + 0.1f)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 히트박스 유지 후 비활성
            yield return new WaitForSeconds(_hitboxDuration);
            if (_hitbox != null) _hitbox.enabled = false;
        }

        /// <summary>
        /// Recovery 단계.
        /// 팔 원위치 + 원래 회전각 복귀 + 색상 복구.
        /// 완료 후 봉인 색상 복구 → OnPatternGroggy 발행.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            if (_armTransform == null) yield break;

            // 색상 → 기본색 복구
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, _recoveryDuration * 0.6f)
                .SetEase(Ease.OutCubic);

            // 팔 이동 원위치
            _moveTween?.Kill();
            bool done = false;
            _moveTween = _armTransform
                .DOLocalMove(_armOriginLocalPos, _recoveryDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => done = true);

            // 회전 원위치 (InOutSine — 자연스럽게)
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
        /// 강제 중단.
        /// 팔 빠르게 원위치 + 회전 복귀 + 봉인 색상 복구.
        /// </summary>
        public new void Interrupt()
        {
            base.Interrupt();
            KillArmTweens();

            if (_hitbox != null) _hitbox.enabled = false;

            if (_armTransform != null)
            {
                // 이동 복귀
                _moveTween = _armTransform
                    .DOLocalMove(_armOriginLocalPos, 0.3f)
                    .SetEase(Ease.OutBack);

                // 회전 복귀
                _rotateTween = _armTransform
                    .DOLocalRotate(_armOriginLocalEuler, 0.25f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => _armPart?.RestoreArmColor()); // ★ 색상 복구
            }

            // 색상 복구
            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, 0.2f);

            Debug.Log("[TestBossPattern_PunchDown] 중단 → 팔 원위치 + 봉인 색상 복구");
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
            if (other.GetComponentInParent<TestBossCore>() != null) return;

            var info = new DamageInfo(
                _armTransform ? _armTransform.position : transform.position,
                _punchDamage,
                Vector2.down,
                AttackType.Combo1);

            damageable.TakeDamage(info);
            Debug.Log($"[TestBossPattern_PunchDown] 플레이어 피격: -{_punchDamage}");
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_armTransform == null) return;

            Vector3 slamTarget = _armTransform.parent
                ? _armTransform.parent.TransformPoint(
                    new Vector3(_armOriginLocalPos.x,
                                _armOriginLocalPos.y - _slamDepth,
                                _armOriginLocalPos.z))
                : transform.position + Vector3.down * _slamDepth;

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(slamTarget, 0.4f);
            Gizmos.DrawLine(_armTransform.position, slamTarget);
        }
    }
}