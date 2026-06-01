// ============================================================
// BossCoreLock.cs  v1.1
// 보스 코어 자물쇠 — 활성 조건 감지 + 딜타임 관리
//
// [v1.1 변경 — IsGroggy 조건 제거]
//
//   [기존 v1.0 문제]
//     CheckCoreActivation() 에서 if (!_ai.IsGroggy) return 조건 존재
//     → 그로기 상태에서만 코어 활성 가능
//     → 봉인 투사체로 팔을 봉인해도 그로기가 아니면 코어 미활성
//
//   [기획 의도]
//     코어 활성 조건: 왼팔 + 오른팔 동시 봉인 상태
//     → 그로기 조건 없음
//     → 양팔이 봉인된 순간 즉시 코어 활성화
//     → 이후 A키 홀드 처형으로 코어 해제 → 딜타임 진입
//
//   [수정 내용]
//     CheckCoreActivation() 에서 if (!_ai.IsGroggy) return 제거
//     → 양팔 봉인 상태가 되는 즉시 코어 활성화
//
//   [이벤트 구독 방식 유지]
//     OnPartReLocked (팔 재잠금) → CheckCoreActivation()
//     OnPartUnlocked (팔 해제)   → CheckCoreActivation()
//     → 상태 변화 시 즉시 재체크
//
// [역할]
//   왼팔 + 오른팔 동시 봉인 시 코어 활성화.
//   코어 활성 후 A키 홀드 처형 → 딜타임 진입.
//   딜타임 종료 → 자동 코어 봉인 + 충격파.
//
// [활성 조건]
//   _armL.IsLocked && _armL.IsActive
//   && _armR.IsLocked && _armR.IsActive
//   → 이벤트 구독으로 상태 변화 시 즉시 체크.
//   조건 충족 시 ActivateCore() 호출.
//   조건 해제 시 DeactivateCore() 호출.
//
// [주먹 팔 제외]
//   Phase 3 의 Hand2L / Hand2R 은 코어 조건에 포함하지 않음.
//   왼팔(ArmL) + 오른팔(ArmR) 2개만 체크.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 보스 코어 자물쇠 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [코어 활성 흐름]
    ///   왼팔 봉인 OR 오른팔 봉인
    ///     → OnPartReLocked 이벤트 발행
    ///       → CheckCoreActivation() 호출
    ///         → 양팔 모두 봉인 상태? → ActivateCore()
    ///           → 코어 오브젝트 표시 + Collider ON
    ///           → BossExecutionHandler 처형 가능
    ///             → A키 홀드 처형 → BossCoreLock.EnterDilTime()
    ///
    /// [코어 비활성 흐름]
    ///   팔 해제 (OnPartUnlocked)
    ///     → CheckCoreActivation()
    ///       → 조건 미충족 → DeactivateCore()
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class BossCoreLock : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 코어 오브젝트 ──────────────────────")]

        /// <summary>
        /// 코어 GameObject.
        /// 기본 비활성 → 활성 조건 충족 시 활성화.
        /// </summary>
        [Tooltip("코어 오브젝트. 기본 비활성 상태.")]
        [SerializeField] private GameObject _coreObject;

        /// <summary>
        /// 코어의 LockComponent.
        /// 활성화 시 A키 홀드 처형 대상.
        /// </summary>
        [Tooltip("코어의 LockComponent.")]
        [SerializeField] private LockComponent _coreLockComponent;

        /// <summary>
        /// 코어 Collider2D.
        /// 활성 시에만 ON.
        /// </summary>
        [Tooltip("코어 Collider2D. 활성 시에만 ON.")]
        [SerializeField] private Collider2D _coreCollider;

        [Header("── 코어 활성 이펙트 ──────────────────────")]

        /// <summary> 코어 활성 파티클. </summary>
        [Tooltip("코어 활성 파티클.")]
        [SerializeField] private ParticleSystem _activateEffect;

        // ──────────────────────────────────────────
        // 참조 (Initialize() 에서 주입)
        // ──────────────────────────────────────────

        private BossKnight _boss;
        private BossKnightAI _ai;
        private BossKnightDataSO _data;
        private BossPartComponent _armL;
        private BossPartComponent _armR;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isCoreActive;
        private bool _isDilTimeActive;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary> 코어 활성화 시 발행. BossExecutionHandler 가 구독. </summary>
        public event Action OnCoreActivated;

        /// <summary> 코어 비활성화 시 발행. </summary>
        public event Action OnCoreDeactivated;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        public bool IsCoreActive => _isCoreActive;
        public bool IsDilTimeActive => _isDilTimeActive;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. BossKnight.Start() 에서 호출.
        /// </summary>
        public void Initialize(BossKnight boss, BossKnightAI ai, BossKnightDataSO data)
        {
            _boss = boss;
            _ai = ai;
            _data = data;
        }

        /// <summary>
        /// 팔 BossPartComponent 등록 및 이벤트 구독.
        /// BossKnight.Start() 에서 호출.
        ///
        /// [구독 이벤트]
        ///   OnPartReLocked : 팔 재잠금 → 양팔 동시 봉인 조건 체크
        ///   OnPartUnlocked : 팔 해제   → 코어 비활성 조건 체크
        /// </summary>
        public void RegisterArmParts(BossPartComponent armL, BossPartComponent armR)
        {
            _armL = armL;
            _armR = armR;

            if (_armL != null)
            {
                _armL.OnPartReLocked += _ => CheckCoreActivation();
                _armL.OnPartUnlocked += _ => CheckCoreActivation();
            }
            if (_armR != null)
            {
                _armR.OnPartReLocked += _ => CheckCoreActivation();
                _armR.OnPartUnlocked += _ => CheckCoreActivation();
            }
        }

        // ══════════════════════════════════════════════════════
        // 코어 활성 조건 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코어 활성 조건 체크.
        /// RegisterArmParts 이벤트 구독에서 자동 호출.
        ///
        /// [v1.1 수정]
        ///   IsGroggy 조건 제거.
        ///   양팔이 봉인된 순간 즉시 활성화.
        ///   기획: 코어 활성 조건 = 왼팔 + 오른팔 동시 봉인 상태 (그로기 무관)
        ///
        /// [조건]
        ///   왼팔 IsLocked && IsActive
        ///   오른팔 IsLocked && IsActive
        ///   딜타임 중 아님
        /// </summary>
        public void CheckCoreActivation()
        {
            if (_isDilTimeActive) return;

            bool bothArmsLocked =
                _armL != null && _armL.IsLocked && _armL.IsActive &&
                _armR != null && _armR.IsLocked && _armR.IsActive;

            if (bothArmsLocked && !_isCoreActive)
                ActivateCore();
            else if (!bothArmsLocked && _isCoreActive)
                DeactivateCore();
        }

        // ══════════════════════════════════════════════════════
        // 코어 활성 / 비활성
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코어 활성화.
        /// 오브젝트 표시 + Collider ON + 이펙트 재생.
        /// OnCoreActivated 이벤트 발행 → BossExecutionHandler 처형 가능.
        /// </summary>
        public void ActivateCore()
        {
            if (_isCoreActive) return;
            _isCoreActive = true;

            if (_coreObject != null) _coreObject.SetActive(true);
            if (_coreCollider != null) _coreCollider.enabled = true;
            if (_coreLockComponent != null) _coreLockComponent.ResetLock();
            if (_activateEffect != null) _activateEffect.Play();

            OnCoreActivated?.Invoke();
            Debug.Log("[BossCoreLock] 코어 활성화 — A키 홀드 처형 가능");
        }

        /// <summary>
        /// 코어 비활성화.
        /// 오브젝트 숨김 + Collider OFF.
        /// Phase 초기화 또는 팔 해제 시 호출.
        /// </summary>
        public void DeactivateCore()
        {
            if (!_isCoreActive) return;
            _isCoreActive = false;

            if (_coreObject != null) _coreObject.SetActive(false);
            if (_coreCollider != null) _coreCollider.enabled = false;
            if (_activateEffect != null) _activateEffect.Stop();

            OnCoreDeactivated?.Invoke();
            Debug.Log("[BossCoreLock] 코어 비활성화");
        }

        // ══════════════════════════════════════════════════════
        // 딜타임
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 딜타임 진입.
        /// BossExecutionHandler 가 코어 처형 완료 시 호출.
        ///
        /// [흐름]
        ///   딜타임 진입 → 보스 완전 정지
        ///   코어에 피해 적용 가능 (직접 공격)
        ///   dilTimeDuration 후 → 자동 코어 봉인 + 충격파 + 전투 복귀
        /// </summary>
        public void EnterDilTime()
        {
            if (_isDilTimeActive) return;
            _isDilTimeActive = true;

            float duration = _data?.dilTimeDuration ?? 7.0f;
            _boss.EnterDilTime(duration);

            StartCoroutine(DilTimeRoutine(duration));
            Debug.Log($"[BossCoreLock] 딜타임 진입 ({duration:F1}초)");
        }

        /// <summary>
        /// 딜타임 코루틴.
        /// duration 경과 후 자동 종료.
        /// </summary>
        private IEnumerator DilTimeRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ExitDilTime();
        }

        /// <summary>
        /// 딜타임 종료.
        /// 자동 코어 봉인 + 충격파 발동.
        /// </summary>
        private void ExitDilTime()
        {
            _isDilTimeActive = false;

            DeactivateCore();
            _boss?.TriggerShockwave();

            Debug.Log("[BossCoreLock] 딜타임 종료 → 코어 자동 봉인 + 충격파");
        }
    }
}