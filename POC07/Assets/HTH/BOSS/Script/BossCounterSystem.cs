// ============================================================
// BossCounterSystem.cs  v1.0
// 검 무식 / 대타 출동 통합 관리
//
// [역할]
//   봉인 투사체 감지 → 상태 판단 → 반격 패턴 실행.
//   _isCounterActive 플래그로 중복 방지.
//
// [발동 우선순위]
//   그로기 / 딜타임 / PhaseTransition → 반격 불가
//   _isCounterActive = true → 반격 불가
//   패턴 시전 중 + Phase 3 검 패턴 → 대타 출동 우선
//   패턴 시전 중 + Phase 3 주먹 패턴 → 대타 출동만
//   패턴 예고 중 + 봉인 가능 패턴 + Phase 1/2 → 검 무식 + 패턴 일시 중지
//   전투 대기 중 → 검 무식
//
// [후방 봉인 처리]
//   봉인 투사체가 보스 후방에서 날아오는 경우
//   → 즉시 180도 회전 후 검 무식 실행 (0.2초 딜레이)
//
// [초기 쿨타임]
//   전투 시작 후 counterInitialCooldown 동안 반격 불가.
//   플레이어 초반 불쾌감 방지.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static KEY.BossKnightAI;

namespace KEY
{
    /// <summary>
    /// 검 무식 / 대타 출동 통합 관리 컴포넌트. (v1.0)
    /// </summary>
    public class BossCounterSystem : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 대타 출동 주먹 파트 ──────────────────────")]

        /// <summary>
        /// 대타 출동에 사용할 주먹 파트 목록.
        /// Phase 3 Hand2L / Hand2R BossPartComponent 연결.
        /// </summary>
        [Tooltip("대타 출동용 주먹 파트 (Hand2L / Hand2R).")]
        [SerializeField] private List<BossPartComponent> _interceptHands = new();

        [Header("── 검 무식 이펙트 ──────────────────────")]

        [Tooltip("검 무식 파티클.")]
        [SerializeField] private ParticleSystem _parryEffect;

        [Header("── 후방 감지 ──────────────────────")]

        /// <summary>
        /// 보스 후방 판단 임계값 (0~1).
        /// 봉인 투사체와 보스 정면 방향의 dot product 가
        /// 이 값 이하이면 후방으로 판단.
        /// </summary>
        [Tooltip("후방 판단 dot product 임계값. 권장: -0.3~0.")]
        [Range(-1f, 0f)]
        [SerializeField] private float _rearDetectThreshold = -0.2f;

        [Tooltip("후방 봉인 감지 시 회전 딜레이 (초).")]
        [Min(0f)]
        [SerializeField] private float _rearTurnDelay = 0.2f;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private BossKnight _boss;
        private BossKnightAI _ai;
        private BossKnightDataSO _data;

        private List<BossPatternBase> _phase1Patterns = new();
        private List<BossPatternBase> _phase2Patterns = new();
        private List<BossPatternBase> _phase3Patterns = new();

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 반격 패턴 실행 중 여부.
        /// true 시 새 봉인 투사체 감지 무시.
        /// </summary>
        private bool _isCounterActive;
        private float _counterCooldownTimer;
        private float _counterInitialTimer;
        private bool _initialCooldownDone;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        public void Initialize(BossKnight boss, BossKnightAI ai, BossKnightDataSO data)
        {
            _boss = boss;
            _ai = ai;
            _data = data;

            _counterInitialTimer = data?.counterInitialCooldown ?? 12.0f;
            _initialCooldownDone = false;
        }

        public void RegisterPatterns(
            List<BossPatternBase> p1,
            List<BossPatternBase> p2,
            List<BossPatternBase> p3)
        {
            _phase1Patterns = p1;
            _phase2Patterns = p2;
            _phase3Patterns = p3;
        }

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Update()
        {
            if (!_initialCooldownDone)
            {
                _counterInitialTimer -= Time.deltaTime;
                if (_counterInitialTimer <= 0f)
                    _initialCooldownDone = true;
            }

            if (_counterCooldownTimer > 0f)
                _counterCooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 봉인 투사체 감지 (SealProjectile 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 투사체 감지 시 호출.
        /// SealProjectile.OnTriggerEnter2D 에서 보스와 충돌 직전에 호출.
        /// 반격 발동 여부 결정 후 처리.
        /// </summary>
        public bool TryCounter(SealProjectile projectile)
        {
            if (projectile == null) return false;

            // 반격 불가 조건 체크
            if (!CanCounter()) return false;

            BossAIState state = _ai.CurrentState;
            BossPatternBase cp = _ai.CurrentPattern;

            // 그로기 / 딜타임 → 반격 불가 (투사체 그냥 적중)
            if (state == BossKnightAI.BossAIState.Groggy ||
                state == BossKnightAI.BossAIState.DilTime ||
                state == BossKnightAI.BossAIState.PhaseTransition)
                return false;

            // 후방 감지 여부
            bool isRear = IsRearProjectile(projectile.transform.position);

            // 현재 패턴 없음 (전투 대기 중) → 검 무식
            if (cp == null || state == BossKnightAI.BossAIState.Idle)
            {
                StartCoroutine(ExecuteParry(projectile, isRear));
                return true;
            }

            // 패턴 예고 중
            if (state == BossKnightAI.BossAIState.Warning)
            {
                if (!cp.CanInterruptDuringWarning) return false;

                BossPatternSealResult result = cp.OnSealHit(
                    isDuringWarning: true,
                    isDuringActive: false);

                return HandleSealResult(result, projectile, isRear, cp);
            }

            // 패턴 시전 중
            if (state == BossKnightAI.BossAIState.Active)
            {
                if (!cp.CanInterruptDuringActive) return false;

                BossPatternSealResult result = cp.OnSealHit(
                    isDuringWarning: false,
                    isDuringActive: true);

                return HandleSealResult(result, projectile, isRear, cp);
            }

            return false;
        }

        // ══════════════════════════════════════════════════════
        // 결과 처리
        // ══════════════════════════════════════════════════════

        private bool HandleSealResult(
            BossPatternSealResult result,
            SealProjectile projectile,
            bool isRear,
            BossPatternBase pattern)
        {
            switch (result)
            {
                case BossPatternSealResult.Absorbed:
                    return false; // 투사체 그냥 적중

                case BossPatternSealResult.Interrupted:
                    // 패턴 중단 + 그로기 → OnPatternGroggy 이벤트로 처리됨
                    return false; // 투사체 적중 허용

                case BossPatternSealResult.RequestParry:
                    StartCoroutine(ExecuteParry(projectile, isRear));
                    return true;

                case BossPatternSealResult.RequestIntercept:
                    // 대타 출동 시도 → 실패 시 검 무식
                    if (!TryIntercept(projectile))
                        StartCoroutine(ExecuteParry(projectile, isRear));
                    return true;

                default:
                    return false;
            }
        }

        // ══════════════════════════════════════════════════════
        // 검 무식 (Parry)
        // ══════════════════════════════════════════════════════

        private IEnumerator ExecuteParry(SealProjectile projectile, bool isRear)
        {
            _isCounterActive = true;
            _ai.EnterCounter();

            // 후방 봉인 → 회전 딜레이
            if (isRear)
            {
                yield return new WaitForSeconds(_rearTurnDelay);
                // AI 가 즉시 방향 전환 (TurnTowardProjectile)
                TurnTowardProjectile(projectile.transform.position);
            }

            // 검 무식 이펙트
            if (_parryEffect != null)
                _parryEffect.Play();

            // 투사체 소멸 처리
            projectile.DestroyProjectile();

            // 쿨타임 시작
            float cooldown = _boss.CurrentPhase == BossPhase.Phase3
                ? (_data?.counterCooldownPhase3 ?? 30f)
                : (_data?.counterCooldownPhase2 ?? 60f);
            _counterCooldownTimer = cooldown;

            yield return new WaitForSeconds(0.3f); // 검 무식 후딜레이

            _ai.ExitCounter();
            _isCounterActive = false;

            Debug.Log($"[BossCounterSystem] 검 무식 완료 (후방:{isRear})");
        }

        // ══════════════════════════════════════════════════════
        // 대타 출동 (Intercept)
        // ══════════════════════════════════════════════════════

        private bool TryIntercept(SealProjectile projectile)
        {
            // 가용 주먹 탐색 (봉인되지 않은 Hand2L / Hand2R)
            BossPartComponent availableHand = null;
            float minDist = float.MaxValue;

            foreach (var hand in _interceptHands)
            {
                if (hand == null || !hand.IsActive || hand.IsLocked) continue;

                float dist = Vector3.Distance(
                    hand.transform.position,
                    projectile.transform.position);

                if (dist < minDist)
                {
                    minDist = dist;
                    availableHand = hand;
                }
            }

            if (availableHand == null) return false; // 가용 주먹 없음

            StartCoroutine(ExecuteIntercept(projectile, availableHand));
            return true;
        }

        private IEnumerator ExecuteIntercept(
            SealProjectile projectile,
            BossPartComponent hand)
        {
            _isCounterActive = true;

            // 주먹 이동 (Transform.position 직접 이동 — 추후 DOTween 으로 교체)
            Vector3 originalPos = hand.transform.position;
            Vector3 projectilePos = projectile.transform.position;

            float elapsed = 0f;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                hand.transform.position = Vector3.Lerp(originalPos, projectilePos, t);
                yield return null;
            }

            // 주먹에 봉인 적용 (ForceUnlock 대신 ReLock 반대 — 봉인 상태로)
            hand.ForceUnlock(); // 봉인 상태 = IsLocked = true → 이미 잠겨있으면 패턴 제외

            // 투사체 소멸
            projectile.DestroyProjectile();

            // 주먹 원위치 복귀
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                hand.transform.position = Vector3.Lerp(projectilePos, originalPos, t);
                yield return null;
            }
            hand.transform.position = originalPos;

            _isCounterActive = false;
            Debug.Log($"[BossCounterSystem] 대타 출동 완료 ({hand.PartType})");
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        private bool CanCounter()
        {
            if (!_initialCooldownDone) return false;
            if (_isCounterActive) return false;
            if (_counterCooldownTimer > 0f) return false;
            if (_boss.IsPhaseInvincible) return false;
            return true;
        }

        /// <summary>
        /// 봉인 투사체가 보스 후방에서 날아오는지 판단.
        /// 보스 정면 방향과 투사체 방향의 dot product 로 판단.
        /// </summary>
        private bool IsRearProjectile(Vector3 projectilePos)
        {
            Vector3 toProjectile = (projectilePos - transform.position).normalized;
            Vector3 facingDir = new Vector3(_ai.FacingDirection, 0f, 0f);
            float dot = Vector3.Dot(facingDir, toProjectile);
            return dot < _rearDetectThreshold;
        }

        private void TurnTowardProjectile(Vector3 projectilePos)
        {
            // BossKnightAI 가 TurnTowardPlayerImmediate 를 가지고 있으므로
            // 투사체 방향으로 즉시 전환 (투사체 = 플레이어 방향과 동일)
            _ai.TurnTowardPlayerImmediate();
        }
    }
}