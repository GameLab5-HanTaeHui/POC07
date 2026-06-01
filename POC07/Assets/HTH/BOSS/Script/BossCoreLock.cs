// ============================================================
// BossCoreLock.cs  v1.0
// 보스 코어 자물쇠 — 활성 조건 감지 + 딜타임 관리
//
// [역할]
//   왼팔 + 오른팔 동시 봉인 시 코어 활성화.
//   코어 활성 후 A키 홀드 처형 → 딜타임 진입.
//   딜타임 종료 → 자동 코어 봉인 + 충격파.
//
// [활성 조건]
//   _armL.IsLocked && _armR.IsLocked
//   → 매 프레임 Update() 에서 체크.
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
    /// 보스 코어 자물쇠 컴포넌트. (v1.0)
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
        /// 활성화 시 A키 홀드 처형 대상이 됨.
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

        public void Initialize(BossKnight boss, BossKnightAI ai, BossKnightDataSO data)
        {
            _boss = boss;
            _ai = ai;
            _data = data;
        }

        public void RegisterArmParts(BossPartComponent armL, BossPartComponent armR)
        {
            _armL = armL;
            _armR = armR;

            // 팔 해제/재잠금 이벤트 구독 → 코어 조건 재체크
            if (_armL != null)
            {
                _armL.OnPartUnlocked += _ => CheckCoreActivation();
                _armL.OnPartReLocked += _ => CheckCoreActivation();
            }
            if (_armR != null)
            {
                _armR.OnPartUnlocked += _ => CheckCoreActivation();
                _armR.OnPartReLocked += _ => CheckCoreActivation();
            }
        }

        // ══════════════════════════════════════════════════════
        // 코어 활성 조건 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코어 활성 조건 체크.
        /// BossKnight / BossPartComponent 이벤트에서 호출.
        /// 왼팔 + 오른팔 동시 봉인 → 코어 활성.
        /// </summary>
        public void CheckCoreActivation()
        {
            if (_ai == null) return;
            if (!_ai.IsGroggy) return; // 그로기 상태에서만 활성 가능
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
        /// BossExecutionHandler 에 처형 가능 신호 전달.
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

        private IEnumerator DilTimeRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ExitDilTime();
        }

        /// <summary>
        /// 딜타임 종료.
        /// 자동 코어 봉인 + 충격파.
        /// </summary>
        private void ExitDilTime()
        {
            _isDilTimeActive = false;

            // 자동 코어 봉인
            DeactivateCore();

            // 충격파
            _boss.TriggerShockwave();

            Debug.Log("[BossCoreLock] 딜타임 종료 → 코어 자동 봉인 + 충격파");
        }
    }
}