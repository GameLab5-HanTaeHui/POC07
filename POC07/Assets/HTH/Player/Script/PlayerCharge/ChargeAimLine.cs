// ============================================================
// ChargeAimLine.cs  v1.0
// 차징 조준선 — LineRenderer + DOTween 차징 피드백
//
// [역할]
//   차징 중 발사 방향을 LineRenderer 로 시각화.
//   DOTween 으로 차징 비율에 따른 피드백 표현:
//     - 라인 길이 증가 (짧게 → 길게)
//     - 색상 변화 (흰 → 노랑 → 빨강)
//     - Player 오브젝트 Punch 진동 (시위 당기는 느낌)
//
// [Hierarchy]
//   Player
//   └── AimLine
//         ├── [ChargeAimLine]
//         └── [LineRenderer]
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 차징 조준선 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [PlayerChargeAttack 에서의 호출]
    ///   Show(direction)        : 차징 시작 시 라인 표시
    ///   UpdateAim(direction)   : 각도 변경 시 방향 갱신
    ///   UpdateCharge(ratio)    : 매 프레임 차징 비율로 피드백 갱신
    ///   Hide()                 : 발사 / 취소 시 라인 숨김
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ChargeAimLine : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 라인 설정 ──────────────────────")]

        /// <summary>
        /// 최소 라인 길이 (차징 시작 시).
        /// </summary>
        [Tooltip("차징 시작 시 라인 최소 길이. 권장: 1.0~2.0")]
        [Min(0.1f)]
        [SerializeField] private float _minLength = 1.5f;

        /// <summary>
        /// 최대 라인 길이 (최대 차징 시).
        /// </summary>
        [Tooltip("최대 차징 시 라인 최대 길이. 권장: 4.0~8.0")]
        [Min(0.5f)]
        [SerializeField] private float _maxLength = 6f;

        /// <summary>
        /// 라인 굵기.
        /// </summary>
        [Tooltip("라인 굵기. 권장: 0.03~0.08")]
        [Min(0.01f)]
        [SerializeField] private float _lineWidth = 0.05f;

        [Header("── 차징 색상 ──────────────────────")]

        /// <summary>
        /// 차징 시작 색상 (차징 비율 0).
        /// </summary>
        [Tooltip("차징 시작 색상.")]
        [SerializeField] private Color _colorMin = Color.white;

        /// <summary>
        /// 차징 중간 색상 (차징 비율 0.5).
        /// </summary>
        [Tooltip("차징 중간 색상.")]
        [SerializeField] private Color _colorMid = Color.yellow;

        /// <summary>
        /// 최대 차징 색상 (차징 비율 1).
        /// </summary>
        [Tooltip("최대 차징 색상.")]
        [SerializeField] private Color _colorMax = Color.red;

        [Header("── DOTween 피드백 ──────────────────────")]

        /// <summary>
        /// 차징 시작 시 라인 등장 연출 시간 (초).
        /// 0 에서 minLength 까지 Ease.OutQuart 로 늘어남.
        /// </summary>
        [Tooltip("차징 시작 시 라인 등장 시간 (초). 권장: 0.1~0.2")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _showDuration = 0.12f;

        /// <summary>
        /// 발사 / 취소 시 라인 사라짐 시간 (초).
        /// </summary>
        [Tooltip("라인 사라짐 시간 (초). 권장: 0.05~0.15")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float _hideDuration = 0.08f;

        /// <summary>
        /// 최대 차징 도달 시 Player 오브젝트 Punch 강도.
        /// 시위를 최대로 당긴 느낌.
        /// </summary>
        [Tooltip("최대 차징 Punch 강도. 권장: 0.1~0.3")]
        [Min(0f)]
        [SerializeField] private float _maxChargePunchStrength = 0.15f;

        /// <summary>
        /// Player Transform 참조.
        /// Punch 진동 적용 대상.
        /// 미연결 시 Awake 에서 부모 탐색.
        /// </summary>
        [Tooltip("Player Transform. Punch 진동 대상. 미연결 시 자동 탐색.")]
        [SerializeField] private Transform _playerTransform;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private LineRenderer _lineRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 라인 방향. </summary>
        private Vector2 _direction = Vector2.right;

        /// <summary> 현재 라인 길이. DOTween 으로 제어. </summary>
        private float _currentLength;

        /// <summary> 이전 프레임 차징 비율. 최대 차징 감지용. </summary>
        private float _prevRatio;

        /// <summary> 최대 차징 Punch 이미 발행 여부. </summary>
        private bool _maxChargePunched;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();

            // LineRenderer 기본 설정
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth * 0.3f; // 끝으로 갈수록 가늘게
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.enabled = false;

            // Player Transform 자동 탐색 (부모 오브젝트)
            if (_playerTransform == null)
                _playerTransform = transform.parent;
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — PlayerChargeAttack 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 차징 시작 시 조준선 표시.
        /// DOTween 으로 0 → minLength 등장 연출.
        /// </summary>
        /// <param name="direction">초기 발사 방향 (정규화)</param>
        public void Show(Vector2 direction)
        {
            _direction = direction;
            _currentLength = 0f;
            _prevRatio = 0f;
            _maxChargePunched = false;

            _lineRenderer.enabled = true;
            ApplyColor(0f);
            UpdateLinePositions();

            // 0 → minLength 등장
            DOTween.Kill(this);
            DOTween.To(
                    () => _currentLength,
                    x => { _currentLength = x; UpdateLinePositions(); },
                    _minLength,
                    _showDuration)
                .SetEase(Ease.OutQuart)
                .SetTarget(this);
        }

        /// <summary>
        /// 발사 각도 변경 시 방향 갱신.
        /// </summary>
        /// <param name="direction">새 방향 (정규화)</param>
        public void UpdateAim(Vector2 direction)
        {
            _direction = direction;
            UpdateLinePositions();
        }

        /// <summary>
        /// 매 프레임 차징 비율로 피드백 갱신.
        ///
        /// [갱신 내용]
        ///   라인 길이 : minLength ~ maxLength 선형 보간
        ///   라인 색상 : _colorMin → _colorMid → _colorMax
        ///   최대 차징 : Punch 진동 1회 발행
        /// </summary>
        /// <param name="chargeRatio">차징 비율 0~1</param>
        public void UpdateCharge(float chargeRatio)
        {
            // 라인 길이
            float targetLength = Mathf.Lerp(_minLength, _maxLength, chargeRatio);
            _currentLength = targetLength;
            UpdateLinePositions();

            // 색상
            ApplyColor(chargeRatio);

            // 최대 차징 도달 시 Punch 1회
            if (chargeRatio >= 1f && !_maxChargePunched)
            {
                _maxChargePunched = true;
                FireMaxChargePunch();
            }

            _prevRatio = chargeRatio;
        }

        /// <summary>
        /// 발사 / 취소 시 조준선 숨김.
        /// DOTween 으로 현재 길이 → 0 축소 후 비활성화.
        /// </summary>
        public void Hide()
        {
            DOTween.Kill(this);

            float startLen = _currentLength;
            DOTween.To(
                    () => _currentLength,
                    x => { _currentLength = x; UpdateLinePositions(); },
                    0f,
                    _hideDuration)
                .SetEase(Ease.InQuart)
                .SetTarget(this)
                .OnComplete(() => _lineRenderer.enabled = false);
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// LineRenderer 위치 갱신.
        /// 시작점: 이 오브젝트의 월드 위치.
        /// 끝점: 시작점 + direction * currentLength.
        /// </summary>
        private void UpdateLinePositions()
        {
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)(_direction * _currentLength);

            _lineRenderer.SetPosition(0, start);
            _lineRenderer.SetPosition(1, end);
        }

        /// <summary>
        /// 차징 비율에 따른 색상 적용.
        /// 0~0.5 : _colorMin → _colorMid
        /// 0.5~1 : _colorMid → _colorMax
        /// </summary>
        private void ApplyColor(float ratio)
        {
            Color color = ratio < 0.5f
                ? Color.Lerp(_colorMin, _colorMid, ratio * 2f)
                : Color.Lerp(_colorMid, _colorMax, (ratio - 0.5f) * 2f);

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color * 0.6f; // 끝으로 갈수록 어둡게
        }

        /// <summary>
        /// 최대 차징 도달 시 Player 오브젝트 Punch 진동.
        /// 시위를 최대로 당긴 떨림 표현.
        /// </summary>
        private void FireMaxChargePunch()
        {
            if (_playerTransform == null) return;

            // 발사 방향 반대로 당겨지는 느낌
            Vector3 punchDir = -(Vector3)_direction * _maxChargePunchStrength;

            _playerTransform.DOPunchPosition(
                    punchDir,
                    duration: 0.3f,
                    vibrato: 8,
                    elasticity: 0.5f)
                .SetEase(Ease.OutQuart);
        }
    }
}