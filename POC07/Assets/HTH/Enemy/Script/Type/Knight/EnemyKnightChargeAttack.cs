// ============================================================
// EnemyKnightChargeAttack.cs  v1.4
// 기사형 차징 돌진 — MovePosition 코루틴 방식
//
// [v1.4 변경]
//   ① DOTween velocity 제어 방식 제거
//       linearVelocity 는 구조체. DOTween 람다 setter 로 복사본만 수정.
//       실제 Rigidbody 에 반영이 안 되어 돌진 불가 현상 발생.
//       → MovePosition 을 매 FixedUpdate 단위로 호출하는 코루틴으로 교체.
//       → 물리 충돌 유지 + 확실한 이동 보장.
//
//   ② ChargeHitbox 전용 콜라이더 분리
//       GetComponent<Collider2D>() 로 본체 CapsuleCollider2D 가 잡히면
//       CheckChargeHitWall() 에서 바닥을 즉시 감지 → 돌진 즉시 종료.
//       → _chargeHitbox 가 null 이면 별도 Trigger 콜라이더 없이
//         Raycast 만으로 충돌 감지하도록 변경.
//
//   ③ 돌진 이동 구현
//       MovePosition 코루틴으로 매 FixedUpdate 단위 이동.
//       이동 속도 = chargeSpeed (units/s).
//       매 스텝마다 플레이어/벽 감지 후 충돌 시 즉시 종료.
//       목표 위치 도달 시 자동 종료.
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
    /// 기사형 차징 돌진 공격. (v1.4)
    ///
    /// ────────────────────────────────────────────────────
    /// [Hierarchy 설정]
    ///   Enemy_Knight
    ///   ├── [EnemyKnightChargeAttack]
    ///   │     _chargeHitbox = (선택) 별도 Trigger BoxCollider2D
    ///   ├── ChargeWarningLine
    ///   │     └── [LineRenderer]  positionCount=2
    ///   └── (선택) TrailRenderer
    ///
    /// [돌진 이동 방식]
    ///   MovePosition 코루틴.
    ///   목표 위치 = startPos + facingDir * _confirmedLength.
    ///   매 FixedUpdate 단위로 chargeSpeed × dt 씩 이동.
    ///   충돌 감지는 Raycast 전용 (본체 콜라이더 미사용).
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyKnightChargeAttack : EnemyAttackBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 (선택) ──────────────────────")]

        /// <summary>
        /// 돌진 피격 전용 Trigger Collider2D.
        /// 미연결 시 Raycast 만으로 플레이어 피격 판정.
        /// 연결 시 더 넓은 판정 가능.
        /// ★ 본체 CapsuleCollider2D 를 절대 연결하지 말 것.
        /// </summary>
        [Tooltip("돌진 전용 Trigger 콜라이더. 미연결 시 Raycast 만으로 판정. " +
                 "본체 CapsuleCollider2D 연결 금지.")]
        [SerializeField] private Collider2D _chargeHitbox;

        [Header("── 경고 비주얼 ──────────────────────")]

        [Tooltip("돌진 예고 LineRenderer. 자식 오브젝트에 부착 후 연결.")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Tooltip("카운트다운 TMP. 선택 연결.")]
        [SerializeField] private TMPro.TextMeshPro _countdownText;

        [Tooltip("카운트다운 시간 (초).")]
        [Min(0.5f)]
        [SerializeField] private float _countdownDuration = 3f;

        [Tooltip("경고선 시작 색상.")]
        [SerializeField] private Color _warningColorStart = new Color(1f, 1f, 0f, 0.4f);

        [Tooltip("경고선 끝 색상.")]
        [SerializeField] private Color _warningColorEnd = new Color(1f, 0.1f, 0.1f, 1f);

        [Tooltip("Raycast 시작 높이 오프셋 (발 위 기준).")]
        [Range(0f, 1f)]
        [SerializeField] private float _rayOriginHeight = 0.3f;

        [Header("── 차징 수치 ──────────────────────")]

        /// <summary>
        /// 돌진 이동 속도 (units/s).
        /// 추격 속도보다 훨씬 빠르게 설정해야 위협감 있음.
        /// </summary>
        [Tooltip("돌진 속도. 권장: 12~18.")]
        [Min(1f)]
        [SerializeField] private float _chargeSpeed = 14f;

        /// <summary>
        /// 돌진 최대 지속 시간 (초).
        /// 벽 충돌 or 이 시간 초과 시 종료 → Groggy 진입.
        /// </summary>
        [Tooltip("돌진 최대 지속 시간. 권장: 0.6~1.2.")]
        [Min(0.1f)]
        [SerializeField] private float _chargeDuration = 0.8f;

        /// <summary>
        /// 돌진 시 플레이어에게 가하는 피해량.
        /// </summary>
        [Tooltip("돌진 피해량. 권장: 15~30.")]
        [Min(0f)]
        [SerializeField] private float _chargeDamage = 20f;

        /// <summary>
        /// 차징 재사용 대기 시간 (초).
        /// EnemyAI.OnEnterAttack() 에서 TryAttack(_chargeCooldown) 으로 전달.
        /// </summary>
        [Tooltip("차징 재사용 대기. 권장: 4~8.")]
        [Min(0.1f)]
        [SerializeField] private float _chargeCooldown = 5f;

        [Header("── 잔상 (선택) ──────────────────────")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private EnemyAI _enemyAI;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private SealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 버퍼
        // ──────────────────────────────────────────

        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _sealComponent = GetComponent<SealComponent>();

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            if (_countdownText != null)
                _countdownText.enabled = false;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        public void SetData(EnemyDataSO data) => _data = data;

        public float ChargeCooldown => _chargeCooldown;

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null) yield break;

            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float maxLength = _chargeSpeed * _chargeDuration;

            float _confirmedLength = 0f;
            bool _lineLocked = false;

            // ────────────────────────────────
            // ① Countdown — LineRenderer 점차 증가
            // ────────────────────────────────
            _rigid2D.linearVelocity = Vector2.zero;

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                UpdateLineRenderer(facingDir, 0f, 0f);
            }

            if (_countdownText != null) _countdownText.enabled = true;

            float elapsed = 0f;

            while (elapsed < _countdownDuration)
            {
                if (_sealComponent != null && _sealComponent.IsSealedAction(SealType.Dash))
                {
                    if (_lineRenderer != null) _lineRenderer.enabled = false;
                    if (_countdownText != null) _countdownText.enabled = false;
                    if (_spriteRenderer != null) _spriteRenderer.color = Color.white;
                    _confirmedLength = 0f;
                    _enemyAI?.EnterGroggy();
                    Debug.Log("[KnightCharge] 카운트다운 중 Dash 봉인 → 취소, Groggy 진입");
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _countdownDuration);

                if (_countdownText != null)
                    _countdownText.text = Mathf.CeilToInt(_countdownDuration - elapsed + 1f).ToString();

                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0f, 1f), t);

                // 선 고정 전이면 매 프레임 장애물 탐지
                if (!_lineLocked)
                {
                    float searchLength = maxLength * t;
                    float hitLength = ScanForObstacle(facingDir, searchLength);

                    if (hitLength < searchLength - 0.1f)
                    {
                        _confirmedLength = hitLength;
                        _lineLocked = true;
                        Debug.Log($"[KnightCharge] 장애물 → 돌진 거리 고정: {hitLength:F2}");
                    }
                    else
                    {
                        _confirmedLength = searchLength;
                    }
                }

                UpdateLineRenderer(facingDir, _confirmedLength, t);
                yield return null;
            }

            // 카운트다운 종료 정리
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            if (_countdownText != null) _countdownText.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

            if (_confirmedLength < 0.3f)
            {
                Debug.Log("[KnightCharge] 확정 거리 너무 짧음 → 돌진 취소");
                yield break;
            }

            // ────────────────────────────────
            // ② Charge — MovePosition 코루틴
            // ────────────────────────────────
            if (_trailRenderer != null) _trailRenderer.emitting = true;
            _hitTargets.Clear();

            Vector2 startPos = _rigid2D.position;
            Vector2 targetPos = startPos + new Vector2(facingDir * _confirmedLength, 0f);
            float speed = _chargeSpeed;

            Debug.Log($"[KnightCharge] 돌진 시작 — 거리:{_confirmedLength:F2} 속도:{speed}");

            while (true)
            {
                yield return new WaitForFixedUpdate();

                Vector2 current = _rigid2D.position;
                float remaining = Vector2.Distance(current, targetPos);

                // 목표 도달
                if (remaining < 0.05f)
                {
                    _rigid2D.MovePosition(targetPos);
                    Debug.Log("[KnightCharge] 목표 위치 도달 → 종료");
                    break;
                }

                // 이번 스텝 이동 거리
                float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
                Vector2 nextPos = current + new Vector2(facingDir * step, 0f);

                // 벽 Raycast 감지
                if (HitWall(facingDir, step + 0.05f))
                {
                    Debug.Log("[KnightCharge] 벽 감지 → 종료");
                    break;
                }

                // 플레이어 Raycast 감지
                if (HitPlayer(facingDir, step + 0.15f))
                    break;

                _rigid2D.MovePosition(nextPos);
            }

            // ────────────────────────────────
            // ③ 종료
            // ────────────────────────────────
            _rigid2D.linearVelocity = Vector2.zero;
            if (_trailRenderer != null) _trailRenderer.emitting = false;
            _hitTargets.Clear();

            yield return new WaitForSeconds(0.15f);
        }

        // ══════════════════════════════════════════════════════
        // LineRenderer 제어
        // ══════════════════════════════════════════════════════

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
        // 장애물 스캔 (카운트다운 중)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// searchLength 거리까지 벽/낭떠러지 감지.
        /// 장애물 있으면 그 거리, 없으면 searchLength 반환.
        /// </summary>
        private float ScanForObstacle(float facingDir, float searchLength)
        {
            if (_data == null || searchLength <= 0.01f) return searchLength;

            Vector3 rayOrigin = transform.position + Vector3.up * _rayOriginHeight;

            // 수평 벽 감지
            RaycastHit2D wallHit = Physics2D.Raycast(
                rayOrigin, new Vector2(facingDir, 0f), searchLength, _data.groundLayer);
            if (wallHit.collider != null)
                return Mathf.Max(0f, wallHit.distance - 0.15f);

            // 낭떠러지 감지 — 끝 지점 아래 바닥 확인
            Vector3 endPt = rayOrigin + new Vector3(facingDir * searchLength, 0f, 0f);
            if (Physics2D.Raycast(endPt, Vector2.down, 2.0f, _data.groundLayer).collider == null)
                return FindCliffEdge(rayOrigin, facingDir, searchLength);

            return searchLength;
        }

        /// <summary>
        /// 낭떠러지 직전 안전 거리를 이진 탐색으로 계산.
        /// </summary>
        private float FindCliffEdge(Vector3 rayOrigin, float facingDir, float maxDist)
        {
            float lo = 0f, hi = maxDist;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 pt = rayOrigin + new Vector3(facingDir * mid, 0f, 0f);
                if (Physics2D.Raycast(pt, Vector2.down, 2.0f, _data.groundLayer).collider != null)
                    lo = mid;
                else
                    hi = mid;
            }
            return Mathf.Max(0f, lo - 0.1f);
        }

        // ══════════════════════════════════════════════════════
        // 돌진 중 충돌 감지 (Raycast 전용)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전방 dist 거리 Raycast — 벽 감지.
        /// </summary>
        private bool HitWall(float facingDir, float dist)
        {
            if (_data == null) return false;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            return Physics2D.Raycast(origin, new Vector2(facingDir, 0f),
                dist, _data.groundLayer).collider != null;
        }

        /// <summary>
        /// 전방 dist 거리 Raycast — 플레이어 감지 후 TakeDamage 호출.
        /// </summary>
        private bool HitPlayer(float facingDir, float dist)
        {
            if (_data == null) return false;

            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            RaycastHit2D hit = Physics2D.Raycast(
                origin, new Vector2(facingDir, 0f), dist, _data.attackHitLayer);

            if (hit.collider == null) return false;
            if (_hitTargets.Contains(hit.collider)) return false;

            if (hit.collider.TryGetComponent<IDamageable>(out var dmg))
            {
                _hitTargets.Add(hit.collider);
                dmg.TakeDamage(new DamageInfo(
                    transform.position,
                    _chargeDamage,
                    new Vector2(facingDir, 0.1f).normalized,
                    AttackType.Combo1));
                Debug.Log($"[KnightCharge] 플레이어 피격: {_chargeDamage}");
                return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;
            float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float length = _chargeSpeed * _chargeDuration;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawRay(origin, new Vector3(dir * length, 0f, 0f));

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _data.chargeDetectRange);
        }
    }
}