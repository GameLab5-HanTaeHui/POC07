// ============================================================
// EnemyKnightChargeAttack.cs  v1.3
// 기사형 차징 돌진 — LineRenderer 점차 증가 + 실제 돌진 거리 계산
//
// [v1.3 변경]
//
//   [문제 1] LineRenderer 가 점차 늘어나지 않음
//     → 카운트다운 진행률(0~1)에 따라 LineRenderer 끝점을 매 프레임 갱신.
//        t=0 → 길이 0 / t=1 → 최대 길이(or 장애물까지).
//
//   [문제 2] CheckCliff() 가 돌진 직전에도 체크되어 즉시 취소됨
//     → CheckCliff() 를 카운트다운 중 취소 조건으로 사용하지 않음.
//        대신 LineRenderer 를 늘려가면서 Ray 로 장애물(벽/땅끝) 감지.
//        감지된 거리까지만 LineRenderer 를 그리고 멈춤.
//        카운트다운 완료 시 그 확정 거리로만 돌진.
//
//   [LineRenderer 점증 + 장애물 감지 흐름]
//     매 프레임:
//       1. 현재 진행률 t = elapsed / countdownDuration
//       2. 이 프레임의 "탐색 거리" = maxLength * t (점차 증가)
//       3. 해당 거리까지 Raycast (벽 감지) + 낭떠러지 확인
//       4. 장애물 있으면 → _confirmedLength = 장애물 거리로 고정 + 선 멈춤
//          장애물 없으면 → _confirmedLength = 현재 탐색 거리 갱신 + 선 늘림
//     카운트다운 종료 후 _confirmedLength 만큼 돌진
//
//   [낭떠러지 감지 방식 변경]
//     기존: EnemySensor.CheckCliff() (발 앞 고정 오프셋)
//     변경: _confirmedLength 거리 앞까지 바닥 Ray 를 쏘아 공중인지 확인.
//           돌진 끝 지점 아래에 지면이 없으면 그 지점에서 선이 멈춤.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 차징 돌진 공격. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [LineRenderer 점증 알고리즘]
    ///   매 프레임 탐색 거리를 늘려가며 Raycast 로 장애물 확인.
    ///   장애물 발견 시 해당 거리에서 선이 멈추고 _confirmedLength 고정.
    ///   카운트다운 완료 후 _confirmedLength 만큼만 돌진.
    ///   → 실제 이동 가능 거리를 미리 시각화.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyKnightChargeAttack : EnemyAttackBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 ──────────────────────")]
        [Tooltip("돌진 히트박스. 미연결 시 본체 Collider2D 자동 탐색.")]
        [SerializeField] private Collider2D _chargeHitbox;

        [Header("── 경고 비주얼 ──────────────────────")]
        [Tooltip("돌진 예고 LineRenderer. 자식 오브젝트에 부착 후 연결.")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Tooltip("카운트다운 TMP. 선택 연결.")]
        [SerializeField] private TMPro.TextMeshPro _countdownText;

        [Tooltip("카운트다운 시간 (초).")]
        [Min(0.5f)]
        [SerializeField] private float _countdownDuration = 3f;

        [Tooltip("경고선 시작 색상 (카운트다운 초기).")]
        [SerializeField] private Color _warningColorStart = new Color(1f, 1f, 0f, 0.4f);

        [Tooltip("경고선 끝 색상 (카운트다운 완료).")]
        [SerializeField] private Color _warningColorEnd = new Color(1f, 0.1f, 0.1f, 1f);

        [Tooltip("돌진 DOTween 가속 시간 (초). 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _chargeAccelTime = 0.12f;

        [Tooltip("장애물 Raycast 시작 높이 오프셋. 발 위치 기준.")]
        [Range(0f, 1f)]
        [SerializeField] private float _rayOriginHeight = 0.3f;

        [Header("── 잔상 (선택) ──────────────────────")]
        [Tooltip("돌진 잔상 TrailRenderer.")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private EnemyAI _enemyAI;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 버퍼
        // ──────────────────────────────────────────

        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();
        private readonly RaycastHit2D[] _castResults = new RaycastHit2D[4];

        // ──────────────────────────────────────────
        // DOTween 핸들
        // ──────────────────────────────────────────

        private Tweener _chargeTween;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_chargeHitbox == null)
                _chargeHitbox = GetComponent<Collider2D>();

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            if (_countdownText != null)
                _countdownText.enabled = false;
        }

        private void OnDestroy()
        {
            _chargeTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        public void SetData(EnemyDataSO data) => _data = data;

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 차징 돌진 전체 시퀀스.
        ///
        /// [흐름]
        ///   ① Countdown: LineRenderer 점차 증가 + 매 프레임 장애물 Ray 감지
        ///                _confirmedLength 가 장애물에서 멈추면 선도 멈춤
        ///   ② 확정 거리  : _confirmedLength 가 0 이면 돌진 취소
        ///   ③ Charge    : _confirmedLength 만큼 DOTween 돌진
        ///   ④ 종료      : 정리
        /// </summary>
        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null) yield break;

            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float maxLength = _data.chargeSpeed * _data.chargeDuration;

            // 카운트다운 중 확정된 실제 돌진 거리
            float _confirmedLength = 0f;
            // 장애물에 막혀 선이 고정됐는지 여부
            bool _lineLocked = false;

            // ────────────────────────────────
            // ① Countdown — LineRenderer 점차 증가
            // ────────────────────────────────
            _rigid2D.linearVelocity = Vector2.zero;

            // LineRenderer 활성화 (길이 0 부터 시작)
            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                UpdateLineRenderer(facingDir, 0f, 0f); // 길이 0, 진행률 0
            }

            if (_countdownText != null) _countdownText.enabled = true;

            float elapsed = 0f;

            while (elapsed < _countdownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _countdownDuration);

                // 카운트다운 텍스트 갱신
                if (_countdownText != null)
                    _countdownText.text = Mathf.CeilToInt(_countdownDuration - elapsed + 1f)
                                              .ToString();

                // 스프라이트 흰→주황 변화
                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0f, 1f), t);

                // 선이 아직 고정되지 않았으면 매 프레임 장애물 탐지
                if (!_lineLocked)
                {
                    float searchLength = maxLength * t; // 이번 프레임의 탐색 거리
                    float hitLength = ScanForObstacle(facingDir, searchLength);

                    if (hitLength < searchLength)
                    {
                        // 장애물 발견 → 선을 그 지점에서 고정
                        _confirmedLength = hitLength;
                        _lineLocked = true;
                        Debug.Log($"[KnightCharge] 장애물 감지 → 돌진 거리 고정: {hitLength:F2}");
                    }
                    else
                    {
                        // 장애물 없음 → 탐색 거리까지 선 연장
                        _confirmedLength = searchLength;
                    }
                }

                // LineRenderer 갱신 (고정됐으면 같은 길이 유지, 색상만 변화)
                UpdateLineRenderer(facingDir, _confirmedLength, t);

                yield return null;
            }

            // 정리
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            if (_countdownText != null) _countdownText.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

            // 확정 거리가 너무 짧으면 돌진 취소
            if (_confirmedLength < 0.5f)
            {
                Debug.Log("[KnightCharge] 확정 거리 너무 짧음 → 돌진 취소");
                yield break;
            }

            // ────────────────────────────────
            // ② Charge — DOTween 돌진
            //    _confirmedLength 만큼만 이동
            // ────────────────────────────────
            if (_trailRenderer != null) _trailRenderer.emitting = true;
            _hitTargets.Clear();

            // 돌진 종료 목표 위치
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + new Vector3(facingDir * _confirmedLength, 0f, 0f);

            // DOTween.To 로 velocity.x 가속 → 일정 속도 유지
            float targetVelocityX = facingDir * _data.chargeSpeed;
            _chargeTween = DOTween.To(
                () => _rigid2D.linearVelocity.x,
                x => _rigid2D.linearVelocity = new Vector2(x, _rigid2D.linearVelocity.y),
                targetVelocityX,
                _chargeAccelTime)
                .SetEase(Ease.OutQuart);

            // 목표 거리에 도달하거나 충돌할 때까지 루프
            bool reachedTarget = false;
            float chargeElapsed = 0f;

            while (chargeElapsed < _data.chargeDuration && !reachedTarget)
            {
                yield return new WaitForFixedUpdate();
                chargeElapsed += Time.fixedDeltaTime;

                // 플레이어 충돌 감지
                if (CheckChargeHitPlayer(facingDir))
                    break;

                // 벽 충돌 감지
                if (CheckChargeHitWall(facingDir))
                    break;

                // 목표 위치 도달 여부 체크
                float traveled = Mathf.Abs(transform.position.x - startPos.x);
                if (traveled >= _confirmedLength - 0.1f)
                {
                    reachedTarget = true;
                    Debug.Log("[KnightCharge] 목표 거리 도달 → 돌진 종료");
                }
            }

            // ────────────────────────────────
            // ③ 종료
            // ────────────────────────────────
            _chargeTween?.Kill();
            _rigid2D.linearVelocity = Vector2.zero;

            if (_trailRenderer != null) _trailRenderer.emitting = false;
            _hitTargets.Clear();

            yield return new WaitForSeconds(0.15f);
        }

        // ══════════════════════════════════════════════════════
        // LineRenderer 제어
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// LineRenderer 를 현재 확정 길이와 진행률 색상으로 갱신.
        /// </summary>
        private void UpdateLineRenderer(float facingDir, float length, float t)
        {
            if (_lineRenderer == null) return;

            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            Vector3 end = origin + new Vector3(facingDir * length, 0f, 0f);

            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, end);

            Color c = Color.Lerp(_warningColorStart, _warningColorEnd, t);
            _lineRenderer.startColor = c;
            _lineRenderer.endColor = c;
        }

        // ══════════════════════════════════════════════════════
        // 장애물 탐지 (카운트다운 중)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 돌진 방향으로 searchLength 거리까지 Raycast 로 장애물을 탐지.
        /// 장애물이 있으면 그 거리를, 없으면 searchLength 를 반환.
        ///
        /// [탐지 대상]
        ///   ① 벽 (groundLayer) — 수평 Ray
        ///   ② 낭떠러지 — searchLength 지점 아래 바닥 Ray
        ///      바닥이 없으면 → 낭떠러지이므로 그 지점에서 멈춤
        ///
        /// [왜 CheckCliff() 대신 이 방식인가]
        ///   EnemySensor.CheckCliff() 는 발 앞 고정 오프셋만 체크.
        ///   돌진 방향 전체를 스캔하지 않아 먼 거리 낭떠러지를 못 잡음.
        ///   이 방식은 searchLength 끝 지점 아래를 직접 체크하므로
        ///   실제 돌진 도착 지점에 바닥이 있는지 정확히 확인 가능.
        /// </summary>
        private float ScanForObstacle(float facingDir, float searchLength)
        {
            if (_data == null || searchLength <= 0f) return searchLength;

            Vector3 rayOrigin = transform.position + Vector3.up * _rayOriginHeight;
            Vector2 rayDir = new Vector2(facingDir, 0f);

            // ① 수평 벽 감지
            RaycastHit2D wallHit = Physics2D.Raycast(
                rayOrigin, rayDir, searchLength, _data.groundLayer);

            if (wallHit.collider != null)
            {
                // 벽까지의 거리 반환 (벽 바로 앞에서 멈추도록 약간 빼기)
                return Mathf.Max(0f, wallHit.distance - 0.2f);
            }

            // ② 낭떠러지 감지 — searchLength 끝 지점 아래 바닥 확인
            Vector3 endPoint = rayOrigin + new Vector3(facingDir * searchLength, 0f, 0f);
            RaycastHit2D groundHit = Physics2D.Raycast(
                endPoint, Vector2.down, 2.0f, _data.groundLayer);

            if (groundHit.collider == null)
            {
                // 바닥 없음 → 낭떠러지
                // 이진 탐색으로 실제 바닥 끝 위치를 찾음
                return FindCliffEdge(rayOrigin, facingDir, searchLength);
            }

            return searchLength;
        }

        /// <summary>
        /// 낭떠러지 직전까지의 안전 거리를 이진 탐색으로 계산.
        /// 바닥이 있는 최대 거리를 반환.
        /// </summary>
        private float FindCliffEdge(Vector3 rayOrigin, float facingDir, float maxDist)
        {
            float lo = 0f;
            float hi = maxDist;
            int iter = 5; // 이진 탐색 횟수

            for (int i = 0; i < iter; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 midPt = rayOrigin + new Vector3(facingDir * mid, 0f, 0f);

                RaycastHit2D hit = Physics2D.Raycast(midPt, Vector2.down, 2.0f, _data.groundLayer);
                if (hit.collider != null)
                    lo = mid; // 바닥 있음 → 더 멀리 탐색
                else
                    hi = mid; // 바닥 없음 → 더 가까이 탐색
            }

            return Mathf.Max(0f, lo - 0.1f); // 안전 마진
        }

        // ══════════════════════════════════════════════════════
        // 돌진 중 충돌 감지
        // ══════════════════════════════════════════════════════

        private bool CheckChargeHitPlayer(float facingDir)
        {
            if (_chargeHitbox == null || _data == null) return false;

            _overlapBuffer.Clear();
            _chargeHitbox.Overlap(
                new ContactFilter2D
                {
                    useTriggers = true,
                    useLayerMask = true,
                    layerMask = _data.attackHitLayer
                },
                _overlapBuffer);

            foreach (var col in _overlapBuffer)
            {
                if (_hitTargets.Contains(col)) continue;
                if (col.TryGetComponent<IDamageable>(out var dmg))
                {
                    _hitTargets.Add(col);
                    dmg.TakeDamage(new DamageInfo(
                        transform.position,
                        _data.chargeDamage,
                        new Vector2(facingDir, 0.1f).normalized,
                        AttackType.Combo1));
                    Debug.Log($"[KnightCharge] 돌진 피격: {_data.chargeDamage}");
                    return true;
                }
            }
            return false;
        }

        private bool CheckChargeHitWall(float facingDir)
        {
            if (_chargeHitbox == null || _data == null) return false;
            int count = _chargeHitbox.Cast(
                new Vector2(facingDir, 0f),
                new ContactFilter2D
                {
                    useTriggers = false,
                    useLayerMask = true,
                    layerMask = _data.groundLayer
                },
                _castResults, 0.1f);

            if (count > 0) { Debug.Log("[KnightCharge] 벽 충돌 → 종료"); return true; }
            return false;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float length = _data.chargeSpeed * _data.chargeDuration;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;

            // 최대 돌진 거리 (반투명 주황)
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
            Gizmos.DrawRay(origin, new Vector3(dir * length, 0f, 0f));

            // 차징 감지 범위 (반투명 노란)
            if (_data != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, _data.chargeDetectRange);
            }
        }
    }
}