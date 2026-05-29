// ============================================================
// EnemyKnightChargeAttack.cs  v1.6
// 기사형 차징 돌진 — OnFlipped 구독 + 봉인 취소 + Groggy 연동
//
// [v1.6 변경]
//   ① OnFlipped 구독으로 FlipHitbox 자체 처리
//       EnemyAI.FlipAttackHitboxes() 로 직접 호출받던 방식 제거.
//       Start() 에서 _enemyAI.OnFlipped += FlipHitbox 구독.
//       EnemyAI 는 이 스크립트의 존재를 알 필요 없음.
//       → 추후 Enemy 타입 추가 시 EnemyAI 수정 불필요.
//
//   ② 카운트다운 중 Dash 봉인 감지 → 즉시 취소 + Groggy 진입
//       매 프레임 _sealComponent.IsSealedAction(SealType.Dash) 체크.
//       봉인 감지 시 LineRenderer/텍스트 정리 후 _enemyAI.EnterGroggy() 호출.
//       돌진 없이 Groggy 로 직행 → 플레이어 Lock 공략 타이밍 제공.
//
//   ③ 벽 충돌 시 Groggy 진입
//       기존: break 로 루프 종료 → OnAttackFinished → Chase 복귀.
//       변경: _enemyAI.EnterGroggy() 직접 호출 → yield break.
//             OnAttackFinished 미발행 — Groggy 가 Chase 복귀 담당.
//
// [v1.5 변경]
//   FlipHitbox() 외부 API + _originalChargeHitboxLocalX 캐싱.
//
// [v1.4 변경]
//   DOTween velocity → MovePosition 코루틴 교체.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 차징 돌진 공격. (v1.6)
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
        /// ★ 본체 CapsuleCollider2D 연결 금지.
        /// </summary>
        [Tooltip("돌진 전용 Trigger 콜라이더. 미연결 시 Raycast 전용. 본체 Collider 연결 금지.")]
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

        [Header("── 잔상 (선택) ──────────────────────")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private EnemyAI _enemyAI;
        private EnemySealComponent _sealComponent;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 히트박스 방향 캐시
        // ──────────────────────────────────────────

        /// <summary>
        /// _chargeHitbox 초기 localPosition.x 절댓값.
        /// Awake 에서 캐싱. FlipHitbox 에서 방향 × 이 값으로 반전.
        /// </summary>
        private float _originalChargeHitboxLocalX;

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
            _sealComponent = GetComponent<EnemySealComponent>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            if (_countdownText != null)
                _countdownText.enabled = false;

            if (_chargeHitbox != null)
            {
                _originalChargeHitboxLocalX =
                    Mathf.Abs(_chargeHitbox.transform.localPosition.x);
                _chargeHitbox.enabled = false;
            }
        }

        private void Start()
        {
            // ★ EnemyAI.OnFlipped 구독 — 방향 전환 시 히트박스 자체 반전
            if (_enemyAI != null)
                _enemyAI.OnFlipped += FlipHitbox;
        }

        private void OnDestroy()
        {
            if (_enemyAI != null)
                _enemyAI.OnFlipped -= FlipHitbox;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> DataSO 주입. EnemyAI.Start() 에서 호출. </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        /// <summary>
        /// 돌진 히트박스 localPosition.x 반전.
        /// EnemyAI.OnFlipped 이벤트 수신 시 자동 호출.
        /// _chargeHitbox 가 null 이면 무시.
        /// </summary>
        private void FlipHitbox(float dir)
        {
            if (_chargeHitbox == null) return;
            Vector3 pos = _chargeHitbox.transform.localPosition;
            _chargeHitbox.transform.localPosition = new Vector3(
                _originalChargeHitboxLocalX * dir, pos.y, pos.z);
        }

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null) yield break;

            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float maxLength = _data.chargeSpeed * _data.chargeDuration;
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
            bool sealCancelled = false;

            while (elapsed < _countdownDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _countdownDuration);

                // ★ Dash 봉인 감지 → 즉시 취소 + Groggy 진입
                if (_sealComponent != null && _sealComponent.IsSealedAction(SealType.Dash))
                {
                    Debug.Log("[KnightCharge] Dash 봉인 감지 → 취소 + Groggy");
                    sealCancelled = true;
                    break;
                }

                if (_countdownText != null)
                    _countdownText.text = Mathf.CeilToInt(_countdownDuration - elapsed + 1f).ToString();

                if (_spriteRenderer != null)
                    _spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0f, 1f), t);

                if (!_lineLocked)
                {
                    float searchLength = maxLength * t;
                    float hitLength = ScanForObstacle(facingDir, searchLength);

                    if (hitLength < searchLength - 0.1f)
                    {
                        _confirmedLength = hitLength;
                        _lineLocked = true;
                        Debug.Log($"[KnightCharge] 장애물 → 거리 고정: {hitLength:F2}");
                    }
                    else
                    {
                        _confirmedLength = searchLength;
                    }
                }

                UpdateLineRenderer(facingDir, _confirmedLength, t);
                yield return null;
            }

            // 정리
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            if (_countdownText != null) _countdownText.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.white;

            // 봉인 취소 → Groggy 직행 (OnAttackFinished 미발행)
            if (sealCancelled)
            {
                _enemyAI?.EnterGroggy();
                yield break;
            }

            if (_confirmedLength < 0.3f)
            {
                Debug.Log("[KnightCharge] 확정 거리 너무 짧음 → 취소");
                yield break;
            }

            // ────────────────────────────────
            // ② Charge — MovePosition 코루틴
            // ────────────────────────────────
            if (_trailRenderer != null) _trailRenderer.emitting = true;
            _hitTargets.Clear();

            Vector2 startPos = _rigid2D.position;
            Vector2 targetPos = startPos + new Vector2(facingDir * _confirmedLength, 0f);
            float speed = _data.chargeSpeed;

            Debug.Log($"[KnightCharge] 돌진 시작 — 거리:{_confirmedLength:F2} 속도:{speed}");

            bool wallHit = false;

            while (true)
            {
                yield return new WaitForFixedUpdate();

                Vector2 current = _rigid2D.position;
                float remaining = Vector2.Distance(current, targetPos);

                if (remaining < 0.05f)
                {
                    _rigid2D.MovePosition(targetPos);
                    Debug.Log("[KnightCharge] 목표 도달 → 종료");
                    break;
                }

                float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
                Vector2 nextPos = current + new Vector2(facingDir * step, 0f);

                // 벽 충돌 → Groggy 직행 (OnAttackFinished 미발행)
                if (HitWall(facingDir, step + 0.05f))
                {
                    Debug.Log("[KnightCharge] 벽 충돌 → Groggy 진입");
                    wallHit = true;
                    break;
                }

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

            if (wallHit)
            {
                // 벽 충돌 시 직접 Groggy 진입 (OnAttackFinished 발행 안 함)
                _enemyAI?.EnterGroggy();
                yield break;
            }

            // 정상 종료 시 짧은 딜레이 후 OnAttackFinished 발행 → EnemyAI.HandleChargeAttackFinished → Groggy
            yield return new WaitForSeconds(0.15f);
        }

        // ══════════════════════════════════════════════════════
        // LineRenderer
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
        // 장애물 스캔
        // ══════════════════════════════════════════════════════

        private float ScanForObstacle(float facingDir, float searchLength)
        {
            if (_data == null || searchLength <= 0.01f) return searchLength;

            Vector3 rayOrigin = transform.position + Vector3.up * _rayOriginHeight;

            RaycastHit2D wallHit = Physics2D.Raycast(
                rayOrigin, new Vector2(facingDir, 0f), searchLength, _data.groundLayer);
            if (wallHit.collider != null)
                return Mathf.Max(0f, wallHit.distance - 0.15f);

            Vector3 endPt = rayOrigin + new Vector3(facingDir * searchLength, 0f, 0f);
            if (Physics2D.Raycast(endPt, Vector2.down, 2.0f, _data.groundLayer).collider == null)
                return FindCliffEdge(rayOrigin, facingDir, searchLength);

            return searchLength;
        }

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
        // 돌진 중 충돌 감지
        // ══════════════════════════════════════════════════════

        private bool HitWall(float facingDir, float dist)
        {
            if (_data == null) return false;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            return Physics2D.Raycast(origin, new Vector2(facingDir, 0f),
                dist, _data.groundLayer).collider != null;
        }

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
                    _data.chargeDamage,
                    new Vector2(facingDir, 0.1f).normalized,
                    AttackType.Combo1));
                Debug.Log($"[KnightCharge] 플레이어 피격: {_data.chargeDamage}");
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
            float length = _data.chargeSpeed * _data.chargeDuration;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawRay(origin, new Vector3(dir * length, 0f, 0f));
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _data.chargeDetectRange);
        }
    }
}