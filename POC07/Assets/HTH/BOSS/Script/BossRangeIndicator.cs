// ============================================================
// BossRangeIndicator.cs  v1.0
// 패턴 예상 범위 시각화 컴포넌트
//
// [역할]
//   보스 패턴 Warning 단계에서 예상 피해 범위를 시각화.
//   BossPatternBase._rangeIndicator 에 연결.
//   BossKnightDataSO.rangeIndicatorEnabled 로 전역 on/off.
//
// [지원 형태]
//   Line     : LineRenderer 기반 직선 범위 (돌진 패턴)
//   Circle   : SpriteRenderer 원형 범위 (원형 베기)
//   Donut    : 내부 빈 원형 범위 (도넛 베기)
//   Sector   : 부채꼴 범위 (횡베기)
//   Custom   : SpriteRenderer 직접 제어
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 패턴 예상 범위 시각화 컴포넌트. (v1.0)
    /// </summary>
    public class BossRangeIndicator : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        //[Header("── 형태 설정 ──────────────────────")]
        public enum IndicatorType
        {
            /// <summary> LineRenderer 직선. 돌진 패턴. </summary>
            Line,
            /// <summary> 원형 SpriteRenderer. </summary>
            Circle,
            /// <summary> 도넛 형태. 내부 비어 있음. </summary>
            Donut,
            /// <summary> 부채꼴. 횡베기 패턴. </summary>
            Sector,
            /// <summary> 직접 SpriteRenderer 제어. </summary>
            Custom,
        }

        [Tooltip("범위 표시 형태.")]
        [SerializeField] private IndicatorType _indicatorType = IndicatorType.Line;

        [Header("── 컴포넌트 연결 ──────────────────────")]

        [Tooltip("Line / Sector 형태 전용.")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Tooltip("Circle / Donut / Custom 형태 전용.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("── 색상 ──────────────────────")]

        [Tooltip("범위 표시 색상.")]
        [SerializeField] private Color _indicatorColor = new Color(1f, 0.2f, 0.2f, 0.4f);

        [Header("── Line 형태 설정 ──────────────────────")]

        [Tooltip("직선 길이 (units).")]
        [Min(0f)]
        [SerializeField] private float _lineLength = 10f;

        [Tooltip("직선 폭 (units).")]
        [Min(0f)]
        [SerializeField] private float _lineWidth = 0.5f;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isVisible;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            SetVisible(false);

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.startWidth = _lineWidth;
                _lineRenderer.endWidth = _lineWidth;
                _lineRenderer.startColor = _indicatorColor;
                _lineRenderer.endColor = _indicatorColor;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.color = _indicatorColor;
        }

        // ══════════════════════════════════════════════════════
        // 공용 API (BossPatternBase 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 범위 표시 on/off.
        /// BossPatternBase.ShowRangeIndicator() 에서 호출.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (_lineRenderer != null) _lineRenderer.enabled = visible;
            if (_spriteRenderer != null) _spriteRenderer.enabled = visible;
        }

        /// <summary>
        /// Line 형태 길이 업데이트.
        /// 카운트다운 중 점진적으로 늘어나는 연출에 사용.
        /// </summary>
        public void UpdateLineLength(float length, float facingDir)
        {
            if (_lineRenderer == null) return;

            Vector3 origin = transform.position;
            Vector3 end = origin + new Vector3(facingDir * length, 0f, 0f);

            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, end);
        }

        /// <summary>
        /// 색상 업데이트 (경고 강도 변화 등).
        /// </summary>
        public void UpdateColor(Color color)
        {
            _indicatorColor = color;
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = color;
                _lineRenderer.endColor = color;
            }
            if (_spriteRenderer != null)
                _spriteRenderer.color = color;
        }

        /// <summary>
        /// Circle 형태 반지름 업데이트.
        /// </summary>
        public void UpdateCircleRadius(float radius)
        {
            if (_spriteRenderer == null) return;
            _spriteRenderer.transform.localScale = Vector3.one * radius * 2f;
        }
    }
}