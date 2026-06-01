// ============================================================
// TestBossPattern_PunchDown.cs  v1.0
// 테스트 미니보스 — 주먹1: 수직 내리찍기 패턴
//
// [기획 — Key_BOSSTest.md]
//   주먹1: 플레이어 있는 지점으로 주먹으로 내려침
//   차징(Warning) 시전 중 봉인 투사체 적중 시
//     → 해당 주먹 기능 일시 봉인 → 팔이 제자리로 돌아감
//
// [DOTween 연출]
//
//   Warning (차징 예고 — 1.2초)
//     팔 오브젝트가 위로 천천히 올라감 (DOTween MoveLocalY)
//     팔 색상이 주황색으로 서서히 변함 (DOTween Color)
//     → 플레이어에게 "곧 내려찍는다" 시각 신호
//
//   Active (내리찍기)
//     팔 오브젝트가 플레이어 X 위치 아래로 빠르게 내려침
//     (DOTween MoveLocal — OutExpo 이징)
//     히트박스 활성 후 비활성
//     → 접촉 시 플레이어 TakeDamage
//
//   Recovery (후딜레이 — 0.6초)
//     팔이 원래 위치로 천천히 복귀 (DOTween MoveLocal — InOutSine)
//     색상이 기본색으로 복구 (DOTween Color)
//     → OnPatternGroggy 발행 → AI → TestBossCore.EnterGroggy()
//
// [팔 봉인 시 처리]
//   Warning 중 _isArm Sealed == true → 패턴 즉시 중단
//   팔이 제자리(originPos) 로 빠르게 복귀 DOTween
//   TestBossArmPart 봉인 상태 적용은 외부(TestBossCore)에서 처리
//
// [Prefab 연결]
//   TestBoss 자식 "Pattern_PunchDown" 오브젝트에 부착.
//   _armTransform : Arm_L 또는 Arm_R 의 Transform 연결.
//   _hitbox       : Arm 자식의 Collider2D (IsTrigger = true).
//   _armRenderer  : Arm 의 SpriteRenderer.
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
    /// 테스트 미니보스 주먹1 — 수직 내리찍기 패턴. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [연출 흐름]
    ///   Warning  : 팔 위로 올라감 + 주황색
    ///   Active   : 플레이어 위치로 빠르게 내려찍기 + 히트박스 활성
    ///   Recovery : 팔 원위치 복귀 + 색상 복구 → 그로기 유도
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_PunchDown : TestBossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 팔 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 이 패턴이 사용할 팔 Transform.
        /// Arm_L 또는 Arm_R 의 Transform 연결.
        /// </summary>
        [Tooltip("이 패턴을 실행할 팔 Transform (Arm_L 또는 Arm_R).")]
        [SerializeField] private Transform _armTransform;

        /// <summary>
        /// 팔 SpriteRenderer. 색상 피드백 연출.
        /// 미연결 시 _armTransform 에서 자동 탐색.
        /// </summary>
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;

        /// <summary>
        /// 내리찍기 히트박스 Collider2D (IsTrigger = true).
        /// Active 구간에서만 활성화.
        /// </summary>
        [Tooltip("내리찍기 히트박스 Collider2D. IsTrigger = true 필요.")]
        [SerializeField] private Collider2D _hitbox;

        [Header("── 연출 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔이 위로 올라가는 거리 (localY 기준).
        /// </summary>
        [Tooltip("Warning 시 팔 상승 거리 (units). 권장: 1.5~3.0.")]
        [Min(0.1f)]
        [SerializeField] private float _windupHeight = 2.5f;

        /// <summary>
        /// Active 시 팔이 내려찍는 Y 거리 (localY 기준 하강).
        /// </summary>
        [Tooltip("Active 시 팔 하강 거리 (units). 권장: 2.0~4.0.")]
        [Min(0.1f)]
        [SerializeField] private float _slamDepth = 3.5f;

        /// <summary>
        /// Active 내리찍기 소요 시간 (초).
        /// </summary>
        [Tooltip("내리찍기 소요 시간 (초). 권장: 0.15~0.35.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _slamDuration = 0.2f;

        /// <summary>
        /// 내리찍기 데미지.
        /// </summary>
        [Tooltip("내리찍기 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 15f;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// </summary>
        [Tooltip("히트박스 활성 유지 시간 (초). 권장: 0.1~0.3.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.2f;

        [Header("── 색상 피드백 ──────────────────────")]

        [Tooltip("Warning 시 팔 색상 (주황).")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.55f, 0.1f, 1f);

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 팔 로컬 원위치. Awake 에서 저장. </summary>
        private Vector3 _armOriginLocalPos;

        /// <summary> 팔 기본 색상. Awake 에서 저장. </summary>
        private Color _armDefaultColor;

        /// <summary> 플레이어 Transform. </summary>
        private Transform _playerTransform;

        /// <summary> 현재 실행 중인 팔 Tween 핸들. Kill 관리용. </summary>
        private Tween _armTween;
        private Tween _colorTween;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // 팔 원위치 저장
            if (_armTransform != null)
                _armOriginLocalPos = _armTransform.localPosition;

            // SpriteRenderer 자동 탐색
            if (_armRenderer == null && _armTransform != null)
                _armRenderer = _armTransform.GetComponent<SpriteRenderer>();

            if (_armRenderer != null)
                _armDefaultColor = _armRenderer.color;

            // 히트박스 초기 비활성
            if (_hitbox != null)
                _hitbox.enabled = false;

            // 플레이어 탐색
            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            // Recovery 후 그로기 유도
            _triggerGroggyOnRecovery = true;
        }

        private void OnDestroy()
        {
            _armTween?.Kill();
            _colorTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 3단계 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계.
        /// 팔이 위로 천천히 올라가며 주황색으로 변함.
        /// "곧 내려찍는다" 시각 신호.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            // 팔 색상 → 주황색
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_warningColor, _warningDuration * 0.5f)
                .SetEase(Ease.InOutSine);

            // 팔 위로 상승 (windup)
            _armTween?.Kill();
            _armTween = _armTransform
                .DOLocalMoveY(_armOriginLocalPos.y + _windupHeight, _warningDuration)
                .SetEase(Ease.OutCubic);

            // Warning 시간 동안 대기 (봉인/중단 체크 포함)
            yield return WaitScaled(_warningDuration);
        }

        /// <summary>
        /// Active 단계.
        /// 플레이어 X 위치 기준으로 팔이 빠르게 내려찍음.
        /// 히트박스 활성 → 접촉 시 플레이어 피격.
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // 히트박스 활성
            if (_hitbox != null) _hitbox.enabled = true;

            // 현재 위치 기준으로 아래로 내려찍기
            float targetY = _armOriginLocalPos.y - _slamDepth;

            _armTween?.Kill();
            bool done = false;
            _armTween = _armTransform
                .DOLocalMoveY(targetY, _slamDuration)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => done = true);

            // 내리찍기 완료 대기
            float elapsed = 0f;
            while (!done && elapsed < _slamDuration + 0.1f)
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
        /// 팔이 원위치로 천천히 복귀 + 색상 복구.
        /// 완료 후 OnPatternGroggy 발행 (베이스 클래스 처리).
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            if (_armTransform == null) yield break;

            // 팔 색상 원복
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_armDefaultColor, _recoveryDuration)
                .SetEase(Ease.OutCubic);

            // 팔 원위치 복귀
            _armTween?.Kill();
            bool done = false;
            _armTween = _armTransform
                .DOLocalMove(_armOriginLocalPos, _recoveryDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => done = true);

            float elapsed = 0f;
            while (!done && elapsed < _recoveryDuration + 0.1f)
            {
                if (_isInterrupted) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // Interrupt 오버라이드 — 팔 원위치 복귀 포함
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 강제 중단.
        /// 팔을 빠르게 원위치로 복귀시키고 색상 복구.
        /// </summary>
        public new void Interrupt()
        {
            base.Interrupt();

            // 진행 중 Tween Kill
            _armTween?.Kill();
            _colorTween?.Kill();

            if (_hitbox != null) _hitbox.enabled = false;

            // 팔 빠르게 원위치 복귀
            if (_armTransform != null)
            {
                _armTween = _armTransform
                    .DOLocalMove(_armOriginLocalPos, 0.25f)
                    .SetEase(Ease.OutBack);
            }

            // 색상 원복
            if (_armRenderer != null)
            {
                _colorTween = _armRenderer
                    .DOColor(_armDefaultColor, 0.2f);
            }

            Debug.Log("[TestBossPattern_PunchDown] 중단 → 팔 원위치 복귀");
        }

        // ══════════════════════════════════════════════════════
        // 물리 충돌
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
            // 보스 자신은 피격 제외
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

            // 내리찍기 도달 위치 시각화
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