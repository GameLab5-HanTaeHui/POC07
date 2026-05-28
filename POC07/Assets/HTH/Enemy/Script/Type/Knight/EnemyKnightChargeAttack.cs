// ============================================================
// EnemyKnightChargeAttack.cs  v1.5
// 기사형 차징 돌진 — ChargeHitbox FlipHitbox 추가
//
// [v1.5 변경]
//   ChargeHitbox FlipHitbox() 외부 API 추가.
//     → EnemyAI.FlipAttackHitboxes() 에서 호출 가능.
//     → 방향 전환 시 _chargeHitbox localPosition.x 반전.
//     → _originalChargeHitboxLocalX 필드 추가 (Awake 캐싱).
//     → _chargeHitbox 가 null 이면 무시 (Raycast 전용 모드).
//
// [v1.4 변경]
//   DOTween velocity 제어 → MovePosition 코루틴 교체.
//   ChargeHitbox 전용 콜라이더 분리.
//   ScanForObstacle 이진탐색.
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
    /// 기사형 차징 돌진 공격. (v1.5)
    ///
    /// ────────────────────────────────────────────────────
    /// [Hierarchy 설정]
    ///   Enemy_Knight
    ///   ├── [EnemyKnightChargeAttack]
    ///   │     _chargeHitbox = ChargeHitbox/BoxCollider2D (선택)
    ///   ├── ChargeWarningLine
    ///   │     └── [LineRenderer]
    ///   └── (선택) TrailRenderer
    ///
    /// [FlipHitbox 호출 시점]
    ///   EnemyAI.Flip() / UpdateChaseDirection()
    ///     → FlipAttackHitboxes(dir)
    ///         → EnemyKnightAttack.FlipHitbox(dir)
    ///         → EnemyKnightChargeAttack.FlipHitbox(dir)  ← v1.5 추가
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
        /// ★ 본체 CapsuleCollider2D 연결 금지.
        /// </summary>
        [Tooltip("돌진 전용 Trigger 콜라이더. 미연결 시 Raycast 전용. " +
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

        [Header("── 잔상 (선택) ──────────────────────")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private EnemyAI _enemyAI;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 히트박스 방향 캐시 (v1.5 추가)
        // ──────────────────────────────────────────

        /// <summary>
        /// _chargeHitbox 초기 localPosition.x 절댓값.
        /// Awake 에서 캐싱. FlipHitbox 에서 방향 × 이 값으로 반전.
        /// _chargeHitbox 가 null 이면 0 으로 유지.
        /// </summary>
        private float _originalChargeHitboxLocalX;

        // ──────────────────────────────────────────
        // 버퍼
        // ──────────────────────────────────────────

        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> ScanForObstacle 에서 확정된 돌진 가능 거리. </summary>
        private float _confirmedLength;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.enabled = false;
            }

            if (_countdownText != null)
                _countdownText.enabled = false;

            // ★ ChargeHitbox localPosition.x 절댓값 캐싱 (v1.5)
            if (_chargeHitbox != null)
            {
                _originalChargeHitboxLocalX =
                    Mathf.Abs(_chargeHitbox.transform.localPosition.x);
                _chargeHitbox.enabled = false;
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> DataSO 주입. EnemyAI.Start() 에서 호출. </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        /// <summary>
        /// 돌진 히트박스 localPosition.x 를 방향에 맞게 반전. (v1.5 추가)
        /// EnemyAI.FlipAttackHitboxes() 에서 방향 전환 시 호출.
        /// _chargeHitbox 가 null 이면 무시.
        ///
        /// [EnemyKnightAttack.FlipHitbox 와 동일한 패턴]
        ///   _originalChargeHitboxLocalX × dir → localPosition.x 갱신.
        /// </summary>
        /// <param name="dir">+1 = 오른쪽, -1 = 왼쪽</param>
        public void FlipHitbox(float dir)
        {
            if (_chargeHitbox == null) return;

            Vector3 pos = _chargeHitbox.transform.localPosition;
            _chargeHitbox.transform.localPosition = new Vector3(
                _originalChargeHitboxLocalX * dir,
                pos.y,
                pos.z);
        }

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null) yield break;

            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;

            // ① 카운트다운 + 경고선 점증
            yield return StartCoroutine(CountdownRoutine(facingDir));

            // 돌진 가능 거리 미달 → 취소
            if (_confirmedLength < 0.3f)
            {
                HideWarning();
                yield break;
            }

            HideWarning();

            // ② 돌진 실행
            yield return StartCoroutine(ChargeRoutine(facingDir));
        }

        // ══════════════════════════════════════════════════════
        // 카운트다운 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator CountdownRoutine(float facingDir)
        {
            _confirmedLength = 0f;
            float elapsed = 0f;

            if (_lineRenderer != null)
                _lineRenderer.enabled = true;

            while (elapsed < _countdownDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _countdownDuration;

                // 장애물 감지 → 돌진 가능 거리 확정
                float scannedLength = ScanForObstacle(facingDir);
                if (scannedLength < _data.chargeDetectRange)
                    _confirmedLength = scannedLength;
                else
                    _confirmedLength = _data.chargeDetectRange;

                // 경고선 업데이트
                UpdateWarningLine(facingDir, t);

                // 카운트다운 텍스트
                if (_countdownText != null)
                {
                    float remaining = Mathf.Ceil(_countdownDuration - elapsed);
                    _countdownText.enabled = true;
                    _countdownText.text = remaining.ToString("0");
                }

                yield return null;
            }

            if (_countdownText != null)
                _countdownText.enabled = false;
        }

        // ══════════════════════════════════════════════════════
        // 돌진 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator ChargeRoutine(float facingDir)
        {
            if (_trailRenderer != null) _trailRenderer.emitting = true;
            if (_chargeHitbox != null) _chargeHitbox.enabled = true;

            _hitTargets.Clear();

            Vector2 startPos = _rigid2D.position;
            Vector2 targetPos = startPos + new Vector2(facingDir * _confirmedLength, 0f);
            float chargeSpeed = _data.chargeSpeed;

            while (Vector2.Distance(_rigid2D.position, targetPos) > 0.05f)
            {
                // 벽 / 낭떠러지 감지
                if (CheckChargeHitWall(facingDir)) break;

                // 플레이어 피격 감지
                CheckChargeHitPlayer();

                // 이동
                Vector2 nextPos = Vector2.MoveTowards(
                    _rigid2D.position,
                    targetPos,
                    chargeSpeed * Time.fixedDeltaTime);

                _rigid2D.MovePosition(nextPos);
                yield return new WaitForFixedUpdate();
            }

            // 종료 처리
            _rigid2D.linearVelocity = Vector2.zero;

            if (_chargeHitbox != null) _chargeHitbox.enabled = false;
            if (_trailRenderer != null) _trailRenderer.emitting = false;

            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 장애물 스캔 (이진탐색)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 방향으로 돌진 가능한 최대 거리를 이진탐색으로 계산.
        /// 벽 수평 Ray + 낭떠러지 하향 Ray.
        /// </summary>
        private float ScanForObstacle(float dir)
        {
            float maxRange = _data.chargeDetectRange;
            float lo = 0f, hi = maxRange;

            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector2 testPos = _rigid2D.position + new Vector2(dir * mid, _rayOriginHeight);

                bool wallHit = Physics2D.Raycast(
                    testPos, new Vector2(dir, 0f), 0.2f,
                    _data.groundLayer).collider != null;

                bool cliffHit = !Physics2D.Raycast(
                    testPos, Vector2.down, 1.5f,
                    _data.groundLayer).collider;

                if (wallHit || cliffHit) hi = mid;
                else lo = mid;
            }

            return lo;
        }

        // ══════════════════════════════════════════════════════
        // 충돌 감지
        // ══════════════════════════════════════════════════════

        private bool CheckChargeHitWall(float dir)
        {
            Vector2 origin = _rigid2D.position + Vector2.up * _rayOriginHeight;
            return Physics2D.Raycast(
                origin,
                new Vector2(dir, 0f),
                0.3f,
                _data.groundLayer).collider != null;
        }

        private void CheckChargeHitPlayer()
        {
            if (_chargeHitbox == null) return;

            _overlapBuffer.Clear();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_data.attackHitLayer);
            filter.useTriggers = true;

            _chargeHitbox.Overlap(filter, _overlapBuffer);

            foreach (var col in _overlapBuffer)
            {
                if (_hitTargets.Contains(col)) continue;
                if (!col.TryGetComponent<IDamageable>(out var dmg)) continue;

                _hitTargets.Add(col);

                float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
                var info = new DamageInfo(
                    attackerPosition: transform.position,
                    amount: _data.chargeDamage,
                    direction: new Vector2(dir, 0.1f).normalized,
                    attackType: AttackType.Combo1
                );
                dmg.TakeDamage(info);
                Debug.Log($"[ChargeAttack] 돌진 피격: {_data.chargeDamage}");
            }
        }

        // ══════════════════════════════════════════════════════
        // 경고선 보조
        // ══════════════════════════════════════════════════════

        private void UpdateWarningLine(float dir, float t)
        {
            if (_lineRenderer == null) return;

            Vector3 start = transform.position;
            Vector3 end = start + new Vector3(dir * _confirmedLength, 0f, 0f);

            _lineRenderer.SetPosition(0, start);
            _lineRenderer.SetPosition(1, end);

            Color color = Color.Lerp(_warningColorStart, _warningColorEnd, t);
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }

        private void HideWarning()
        {
            if (_lineRenderer != null) _lineRenderer.enabled = false;
            if (_countdownText != null) _countdownText.enabled = false;
        }
    }
}