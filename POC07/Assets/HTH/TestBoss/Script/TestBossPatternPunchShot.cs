// ============================================================
// TestBossPattern_PunchShot.cs  v1.0
// 테스트 미니보스 — 주먹2: 수평 날리기 패턴
//
// [기획 — Key_BOSSTest.md]
//   주먹2: 플레이어 있는 방향으로 주먹을 날림
//   차징(Warning) 시전 중 봉인 투사체 적중 시
//     → 해당 주먹 기능 일시 봉인 → 팔이 제자리로 돌아감
//
// [DOTween 연출]
//
//   Warning (차징 예고 — 1.0초)
//     팔 오브젝트가 반대 방향(뒤로)으로 DOTween MoveLocalX 후퇴
//     팔 색상이 파란색으로 서서히 변함
//     → 플레이어에게 "곧 옆으로 날아온다" 시각 신호
//
//   Active (수평 발사)
//     팔 오브젝트가 플레이어 방향으로 빠르게 돌진
//     (DOTween MoveLocalX — OutExpo 이징)
//     히트박스 활성 → 접촉 시 플레이어 TakeDamage
//     플레이어 방향 기준 _shotDistance 만큼 이동 후 정지
//
//   Recovery (후딜레이 — 0.8초)
//     팔이 원래 위치로 천천히 복귀 (DOTween MoveLocal — InOutSine)
//     색상이 기본색으로 복구
//     → OnPatternGroggy 발행 → AI → TestBossCore.EnterGroggy()
//
// [봉인 처리]
//   Warning 중 팔 봉인 시 → Interrupt() 호출
//   팔 빠르게 원위치 복귀 DOTween
//   보스 보디가 플레이어 반대 방향으로 후퇴 (거리 벌리기)
//
// [Prefab 연결]
//   TestBoss 자식 "Pattern_PunchShot" 오브젝트에 부착.
//   _armTransform   : Arm_L 또는 Arm_R Transform
//   _bossTransform  : 보스 루트 Transform (보스 후퇴 이동용)
//   _hitbox         : Arm 자식 Collider2D (IsTrigger)
//   _armRenderer    : Arm SpriteRenderer
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
    /// 테스트 미니보스 주먹2 — 수평 날리기 패턴. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [연출 흐름]
    ///   Warning  : 팔 뒤로 후퇴 + 파란색 (에너지 모으기)
    ///   Active   : 플레이어 방향으로 수평 발사 + 히트박스
    ///   Recovery : 팔 원위치 복귀 + 색상 복구 → 그로기 유도
    ///
    /// [PunchDown 과의 차이]
    ///   PunchDown : 수직 내리찍기 (Y 축)
    ///   PunchShot : 수평 날리기 (X 축) — 플레이어 방향 기준
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_PunchShot : TestBossPatternBase
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
        /// 미연결 시 자동 탐색.
        /// </summary>
        [Tooltip("팔 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _armRenderer;

        /// <summary>
        /// 수평 발사 히트박스 Collider2D (IsTrigger = true).
        /// Active 구간에서만 활성화.
        /// </summary>
        [Tooltip("수평 발사 히트박스 Collider2D.")]
        [SerializeField] private Collider2D _hitbox;

        [Header("── 보스 후퇴 연결 (선택) ──────────────────────")]

        /// <summary>
        /// 봉인 시 보스 루트 Rigidbody2D.
        /// 봉인 성공 시 보스가 플레이어 반대 방향으로 후퇴.
        /// 미연결 시 후퇴 연출 생략.
        /// </summary>
        [Tooltip("봉인 시 보스 후퇴용 Rigidbody2D. 미연결 시 생략.")]
        [SerializeField] private Rigidbody2D _bossRigid2D;

        [Header("── 연출 수치 ──────────────────────")]

        /// <summary>
        /// Warning 시 팔이 뒤로 후퇴하는 localX 거리.
        /// 플레이어 반대 방향으로 당김 (에너지 모으기).
        /// </summary>
        [Tooltip("Warning 시 팔 후퇴 거리 (units). 권장: 0.5~1.5.")]
        [Min(0.1f)]
        [SerializeField] private float _windupPullback = 1.0f;

        /// <summary>
        /// Active 시 팔이 발사되는 localX 거리.
        /// 플레이어 방향 기준.
        /// </summary>
        [Tooltip("Active 시 발사 거리 (units). 권장: 3.0~6.0.")]
        [Min(0.5f)]
        [SerializeField] private float _shotDistance = 4.5f;

        /// <summary>
        /// Active 발사 소요 시간 (초).
        /// </summary>
        [Tooltip("발사 소요 시간 (초). 권장: 0.15~0.3.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _shotDuration = 0.2f;

        /// <summary>
        /// 발사 데미지.
        /// </summary>
        [Tooltip("발사 데미지.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 12f;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// </summary>
        [Tooltip("히트박스 활성 유지 시간 (초). 권장: 0.1~0.25.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _hitboxDuration = 0.18f;

        /// <summary>
        /// 봉인 시 보스 후퇴 속도 (units/s).
        /// </summary>
        [Tooltip("봉인 시 보스 후퇴 속도. 권장: 4~8.")]
        [Min(0f)]
        [SerializeField] private float _retreatSpeed = 5f;

        /// <summary>
        /// 봉인 시 보스 후퇴 지속 시간 (초).
        /// </summary>
        [Tooltip("봉인 시 보스 후퇴 지속 시간 (초). 권장: 0.3~0.6.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float _retreatDuration = 0.4f;

        [Header("── 색상 피드백 ──────────────────────")]

        [Tooltip("Warning 시 팔 색상 (파랑 — 에너지 집중).")]
        [SerializeField] private Color _warningColor = new Color(0.3f, 0.6f, 1.0f, 1f);

        [Tooltip("Active 시 팔 색상 (흰색 — 발사 순간).")]
        [SerializeField] private Color _activeColor = Color.white;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 팔 로컬 원위치. Awake 에서 저장. </summary>
        private Vector3 _armOriginLocalPos;

        /// <summary> 팔 기본 색상. Awake 에서 저장. </summary>
        private Color _armDefaultColor;

        /// <summary> 플레이어 Transform. </summary>
        private Transform _playerTransform;

        /// <summary>
        /// 발사 방향 (+1 = 오른쪽, -1 = 왼쪽).
        /// Warning 시작 시점 플레이어 방향으로 결정.
        /// </summary>
        private float _shotDirection;

        /// <summary> 현재 팔 Tween. </summary>
        private Tween _armTween;
        private Tween _colorTween;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_armTransform != null)
                _armOriginLocalPos = _armTransform.localPosition;

            if (_armRenderer == null && _armTransform != null)
                _armRenderer = _armTransform.GetComponent<SpriteRenderer>();

            if (_armRenderer != null)
                _armDefaultColor = _armRenderer.color;

            if (_hitbox != null)
                _hitbox.enabled = false;

            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            // 보스 루트 Rigidbody2D 자동 탐색
            if (_bossRigid2D == null)
                _bossRigid2D = GetComponentInParent<Rigidbody2D>();

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
        /// 팔이 플레이어 반대 방향으로 후퇴 (에너지 모으기).
        /// 파란색으로 변함 → "수평 발사 예고".
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            if (_armTransform == null) yield break;

            // 발사 방향 결정 (Warning 시점 기준 플레이어 방향)
            if (_playerTransform != null)
            {
                _shotDirection = _playerTransform.position.x > transform.position.x
                    ? 1f : -1f;
            }
            else
            {
                _shotDirection = 1f;
            }

            // 색상 → 파란색 (에너지 집중)
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_warningColor, _warningDuration * 0.6f)
                .SetEase(Ease.InSine);

            // 팔 후퇴 : 발사 방향 반대로 당김 (localX 기준)
            float pullbackX = _armOriginLocalPos.x - _windupPullback * _shotDirection;
            _armTween?.Kill();
            _armTween = _armTransform
                .DOLocalMoveX(pullbackX, _warningDuration * 0.7f)
                .SetEase(Ease.OutBack);

            yield return WaitScaled(_warningDuration);
        }

        /// <summary>
        /// Active 단계.
        /// 팔이 플레이어 방향으로 빠르게 발사.
        /// 히트박스 활성 → 접촉 시 플레이어 피격.
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_armTransform == null || _isInterrupted) yield break;

            // 색상 → 흰색 플래시 (발사 순간)
            _colorTween?.Kill();
            _colorTween = _armRenderer?
                .DOColor(_activeColor, 0.05f)
                .SetEase(Ease.OutFlash);

            // 히트박스 활성
            if (_hitbox != null) _hitbox.enabled = true;

            // 발사: 플레이어 방향으로 shotDistance 만큼 이동
            float targetX = _armOriginLocalPos.x + _shotDistance * _shotDirection;

            _armTween?.Kill();
            bool done = false;
            _armTween = _armTransform
                .DOLocalMoveX(targetX, _shotDuration)
                .SetEase(Ease.OutExpo)
                .OnComplete(() => done = true);

            float elapsed = 0f;
            while (!done && elapsed < _shotDuration + 0.1f)
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
        /// 팔이 원위치로 복귀 + 색상 복구.
        /// 완료 후 OnPatternGroggy 발행.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            if (_armTransform == null) yield break;

            // 색상 원복
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
        // Interrupt 오버라이드 — 팔 원위치 복귀 + 보스 후퇴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 강제 중단 (봉인 적중 포함).
        /// 팔 원위치 복귀 + 보스가 플레이어 반대로 후퇴.
        /// 기획: "봉인 시 제자리로 돌아가고 플레이어와 거리를 벌릴려고 함"
        /// </summary>
        public new void Interrupt()
        {
            base.Interrupt();

            _armTween?.Kill();
            _colorTween?.Kill();

            if (_hitbox != null) _hitbox.enabled = false;

            // 팔 빠르게 원위치 복귀
            if (_armTransform != null)
            {
                _armTween = _armTransform
                    .DOLocalMove(_armOriginLocalPos, 0.3f)
                    .SetEase(Ease.OutBack);
            }

            // 색상 원복
            if (_armRenderer != null)
            {
                _colorTween = _armRenderer
                    .DOColor(_armDefaultColor, 0.25f);
            }

            // 보스 후퇴 (플레이어 반대 방향)
            if (_bossRigid2D != null && _retreatSpeed > 0f)
            {
                StartCoroutine(RetreatRoutine());
            }

            Debug.Log("[TestBossPattern_PunchShot] 중단/봉인 → 팔 원위치 + 보스 후퇴");
        }

        /// <summary>
        /// 보스 후퇴 코루틴.
        /// 플레이어 반대 방향으로 retreatDuration 동안 이동.
        /// </summary>
        private IEnumerator RetreatRoutine()
        {
            float retreatDir = -_shotDirection; // 플레이어 반대 방향
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
        // 물리 충돌
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
            if (other.GetComponentInParent<TestBossCore>() != null) return;

            Vector2 dir = new Vector2(_shotDirection, 0f);
            var info = new DamageInfo(
                _armTransform ? _armTransform.position : transform.position,
                _punchDamage,
                dir,
                AttackType.Combo1);

            damageable.TakeDamage(info);
            Debug.Log($"[TestBossPattern_PunchShot] 플레이어 피격: -{_punchDamage}");
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_armTransform == null) return;

            // 발사 도달 위치 시각화
            Vector3 shotTarget = _armTransform.position
                + new Vector3(_shotDirection * _shotDistance, 0f, 0f);

            Gizmos.color = new Color(0.3f, 0.6f, 1.0f, 0.4f);
            Gizmos.DrawWireSphere(shotTarget, 0.4f);
            Gizmos.DrawLine(_armTransform.position, shotTarget);
        }
    }
}