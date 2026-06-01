// ============================================================
// BossExecutionHandler.cs  v1.0
// A키 홀드 처형 처리 컴포넌트
//
// [역할]
//   그로기 상태에서 플레이어가 A키를 홀드하면 처형 실행.
//   처형 완료 → 부위 잠금 / 해제.
//   그로기 회복(종료) 시 처형 강제 중단 + 충격파.
//
// [처형 흐름]
//   1. 그로기 진입 → OnGroggyEnter() 호출 → 입력 감지 시작
//   2. 플레이어가 부위 범위 내 진입 + A키 홀드 시작
//   3. 홀드 진행 바 표시 (추후 UI 연결)
//   4. holdDuration 채움 → ForceUnlock() or ReLock() 실행
//   5. 그로기 종료 → OnGroggyExit() → 입력 감지 중지
//   6. 그로기 중 회복 시 → 처형 강제 중단 + 충격파
//
// [InputManager 연동]
//   InputManager.Instance 의 Attack 버튼(A키) Hold 상태 폴링.
//   기존 PlayerWeaponBase.HandleAttackInput 과 충돌 방지를
//   위해 보스 처형 전용 상태 플래그 사용.
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
    /// A키 홀드 처형 처리 컴포넌트. (v1.0)
    /// </summary>
    public class BossExecutionHandler : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 처형 이펙트 ──────────────────────")]

        [Tooltip("처형 완료 파티클.")]
        [SerializeField] private ParticleSystem _executionEffect;

        [Tooltip("처형 진행 중 루프 파티클.")]
        [SerializeField] private ParticleSystem _executionProgressEffect;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private BossKnight _boss;
        private BossKnightAI _ai;
        private BossKnightDataSO _data;

        private Transform _playerTransform;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isGroggy;
        private bool _isExecuting;
        private float _holdTimer;
        private BossPartComponent _targetPart;
        private Coroutine _executionCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary> 처형 강제 중단 시 발행. </summary>
        public event Action OnExecutionInterrupted;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        public void Initialize(
            BossKnight boss,
            BossKnightAI ai,
            BossKnightDataSO data)
        {
            _boss = boss;
            _ai = ai;
            _data = data;

            // 플레이어 탐색
            var player = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (player.Length > 0)
                _playerTransform = player[0].transform;
        }

        // ══════════════════════════════════════════════════════
        // 그로기 이벤트 수신 (BossKnightAI 에서 구독)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 진입 시 호출. BossKnightAI.OnGroggyEnter 구독.
        /// 처형 입력 감지 시작.
        /// </summary>
        public void OnGroggyEnter()
        {
            _isGroggy = true;
            _holdTimer = 0f;
            _targetPart = null;

            Debug.Log("[BossExecutionHandler] 그로기 진입 — 처형 입력 감지 시작");
        }

        /// <summary>
        /// 그로기 종료 시 호출. BossKnightAI.OnGroggyExit 구독.
        /// 처형 진행 중이면 강제 중단 + 충격파.
        /// </summary>
        public void OnGroggyExit()
        {
            _isGroggy = false;

            if (_isExecuting)
            {
                InterruptExecution();
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 입력 감지 (Update)
        // ══════════════════════════════════════════════════════

        private void Update()
        {
            if (!_isGroggy) return;
            if (_isExecuting) return;
            if (_playerTransform == null) return;
            if (InputManager.Instance == null) return;

            // 처형 가능한 부위 탐색
            BossPartComponent nearPart = FindNearestExecutablePart();
            if (nearPart == null)
            {
                _holdTimer = 0f;
                _targetPart = null;
                return;
            }

            // A키 홀드 체크
            bool isHolding = InputManager.Instance.IsAttackHeld;

            if (isHolding)
            {
                if (_targetPart != nearPart)
                {
                    _targetPart = nearPart;
                    _holdTimer = 0f;
                }

                _holdTimer += Time.deltaTime;

                // 홀드 진행 표시 (추후 UI 연결)
                float progress = Mathf.Clamp01(_holdTimer / (_data?.executionHoldDuration ?? 1.5f));
                // TODO: UI 처형 게이지 업데이트

                if (_holdTimer >= (_data?.executionHoldDuration ?? 1.5f))
                {
                    _executionCoroutine = StartCoroutine(ExecuteRoutine(_targetPart));
                }
            }
            else
            {
                _holdTimer = 0f;
                _targetPart = null;
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 실행
        // ══════════════════════════════════════════════════════

        private IEnumerator ExecuteRoutine(BossPartComponent part)
        {
            _isExecuting = true;
            _holdTimer = 0f;

            float holdDuration = _data?.executionHoldDuration ?? 1.5f;

            // 플레이어 자동 이동 (부위 위치로)
            yield return StartCoroutine(MovePlayerToPart(part));

            // 처형 이펙트
            if (_executionEffect != null)
            {
                _executionEffect.transform.position = part.transform.position;
                _executionEffect.Play();
            }

            // 잠금 / 해제 실행
            if (part.IsUnlocked)
            {
                // 해제된 부위 → 재잠금
                part.ReLock();
                Debug.Log($"[BossExecutionHandler] 처형 완료 → {part.PartType} 재잠금");
            }
            else
            {
                // 잠긴 부위 → 해제
                part.ForceUnlock();
                Debug.Log($"[BossExecutionHandler] 처형 완료 → {part.PartType} 해제");
            }

            yield return new WaitForSeconds(0.3f);

            _isExecuting = false;
            _targetPart = null;
        }

        /// <summary>
        /// 플레이어를 부위 위치로 자동 이동.
        /// 추후 DOTween 으로 교체 가능.
        /// </summary>
        private IEnumerator MovePlayerToPart(BossPartComponent part)
        {
            if (_playerTransform == null) yield break;

            Vector3 start = _playerTransform.position;
            Vector3 target = part.transform.position;
            float speed = 15f;
            float elapsed = 0f;
            float dist = Vector3.Distance(start, target);
            float duration = dist / speed;

            while (elapsed < duration)
            {
                if (!_isGroggy) yield break; // 그로기 해제되면 중단

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _playerTransform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 강제 중단
        // ══════════════════════════════════════════════════════

        private void InterruptExecution()
        {
            if (_executionCoroutine != null)
            {
                StopCoroutine(_executionCoroutine);
                _executionCoroutine = null;
            }

            _isExecuting = false;
            _holdTimer = 0f;
            _targetPart = null;

            // 충격파 발동
            _boss.TriggerShockwave();
            OnExecutionInterrupted?.Invoke();

            Debug.Log("[BossExecutionHandler] 처형 강제 중단 + 충격파");
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 주변에서 처형 가능한 가장 가까운 부위 탐색.
        /// 그로기 상태 + 활성 부위 + 처형 범위 내 조건.
        /// </summary>
        private BossPartComponent FindNearestExecutablePart()
        {
            if (_boss == null || _playerTransform == null) return null;

            float minDist = float.MaxValue;
            BossPartComponent nearest = null;

            // BossKnight 의 _allParts 를 직접 접근하는 대신
            // GetComponentsInChildren 으로 탐색
            var parts = _boss.GetComponentsInChildren<BossPartComponent>();

            float defaultRange = _data?.executionRange ?? 2.0f;

            foreach (var part in parts)
            {
                if (part == null) continue;
                if (!part.IsActive) continue;

                float range = part.ExecutionRange(defaultRange);
                float dist = Vector3.Distance(
                    _playerTransform.position,
                    part.transform.position);

                if (dist <= range && dist < minDist)
                {
                    minDist = dist;
                    nearest = part;
                }
            }

            return nearest;
        }
    }
}