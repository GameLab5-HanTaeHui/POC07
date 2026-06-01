// ============================================================
// BossKnight.cs  v1.2
// 봉인된 기사 보스 루트 컴포넌트
//
// [v1.2 변경 — EnemyBossBase 상속으로 전환]
//   : EnemyBase  →  : EnemyBossBase
//
//   제거:
//     private bool _isPhaseInvincible  → EnemyBossBase.protected 로 이전
//     public override float HpRatio    → EnemyBossBase 가 자체 처리
//     _settings null 우회 처리         → EnemyBossBase 에 _settings 없음
//     base.Awake() 우회 주석           → EnemyBossBase.Awake() 정상 호출
//
//   추가:
//     abstract 프로퍼티 override 4개
//       BossMaxHp            → _bossData.maxHp
//       BossKnockbackForce   → _bossData.knockbackForce
//       BossKnockbackDecay   → _bossData.knockbackDecay
//       BossIFrameDuration   → _bossData.iFrameDuration
//
//   변경:
//     TakeDamage  : base.TakeDamage → 직접 내부 로직 유지
//                   (EnemyBossBase.TakeDamage 가 _isPhaseInvincible 체크 포함)
//     Awake       : _currentHp 직접 대입 → InitializeHp() 호출로 교체
//     Die         : base.Die() 정상 호출 (우회 없음)
//     OnBossDied  : Die 완료 후 추가 로직 (EnemyBossBase 확장점)
//
// [상속 구조]
//   MonoBehaviour
//     └── EnemyBossBase  ← IDamageable
//           └── BossKnight
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 봉인된 기사 보스 루트 컴포넌트. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 흐름]
    ///   IDamageable.TakeDamage(info)
    ///     → EnemyBossBase._isPhaseInvincible 체크 (Phase 전환 중 무적)
    ///     → EnemyBossBase._isInvincible 체크 (iFrame)
    ///     → BossKnight.TakeDamage(override) 진입
    ///         → 딜타임 상태 → base.TakeDamage() (HP 감소)
    ///         → 자물쇠 전부 해제 → base.TakeDamage() (HP 감소)
    ///         → 미해제 → 무시
    ///     → OnDamaged() → Phase 전환 체크
    ///
    /// [Phase 전환 흐름]
    ///   BossPhaseManager.TryTransition(nextPhase)
    ///     → BossKnight.EnterPhaseTransition(nextPhase)
    ///       → _isPhaseInvincible = true  (EnemyBossBase 필드)
    ///       → TriggerShockwave()
    ///       → Phase 전환 애니메이션 대기
    ///       → Phase3 진입 시 RestoreFullHp() (EnemyBossBase API)
    ///       → InitializePhase(nextPhase)
    ///       → _isPhaseInvincible = false
    ///       → BossKnightAI.ExitPhaseTransition()
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(BossKnightAI))]
    [RequireComponent(typeof(BossPhaseManager))]
    [RequireComponent(typeof(BossCounterSystem))]
    [RequireComponent(typeof(BossShockwave))]
    [RequireComponent(typeof(BossExecutionHandler))]
    [RequireComponent(typeof(BossCoreLock))]
    public class BossKnight : EnemyBossBase
    {
        // ──────────────────────────────────────────
        // Inspector — DataSO (유일한 연결 지점)
        // ──────────────────────────────────────────

        [Header("── 보스 DataSO (필수) ──────────────────────")]

        /// <summary>
        /// 보스 전용 수치 SO.
        /// ★ Inspector 연결 지점은 이 필드 하나.
        /// 모든 서브 컴포넌트는 Initialize() 에서 주입받음.
        /// </summary>
        [Tooltip("BossKnightDataSO. 필수 연결. 이 컴포넌트에만 연결.")]
        [SerializeField] private BossKnightDataSO _bossData;

        [Header("── 부위 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// 전체 부위 BossPartComponent 목록.
        /// Inspector 에서 순서대로 연결.
        /// Phase 전환 시 전부 Initialize() 호출.
        /// </summary>
        [Tooltip("모든 부위 BossPartComponent. Phase 전환 시 전부 초기화.")]
        [SerializeField] private List<BossPartComponent> _allParts = new();

        [Header("── Phase별 패턴 컴포넌트 ──────────────────────")]

        /// <summary> Phase 1 패턴 목록. </summary>
        [Tooltip("Phase 1 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase1Patterns = new();

        /// <summary> Phase 2 패턴 목록. </summary>
        [Tooltip("Phase 2 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase2Patterns = new();

        /// <summary> Phase 3 패턴 목록. </summary>
        [Tooltip("Phase 3 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase3Patterns = new();

        [Header("── Phase별 전용 오브젝트 ──────────────────────")]

        /// <summary> Phase 1 전용 오브젝트 목록. </summary>
        [Tooltip("Phase 1 전용 오브젝트. Phase 1 에서만 활성.")]
        [SerializeField] private List<GameObject> _phase1Objects = new();

        /// <summary> Phase 2 전용 오브젝트 목록. </summary>
        [Tooltip("Phase 2 전용 오브젝트.")]
        [SerializeField] private List<GameObject> _phase2Objects = new();

        /// <summary> Phase 3 전용 오브젝트 목록. </summary>
        [Tooltip("Phase 3 전용 오브젝트.")]
        [SerializeField] private List<GameObject> _phase3Objects = new();

        // ──────────────────────────────────────────
        // 서브 컴포넌트 참조 (Awake 자동 취득)
        // ──────────────────────────────────────────

        private BossKnightAI _ai;
        private BossPhaseManager _phaseManager;
        private BossCounterSystem _counterSystem;
        private BossShockwave _shockwave;
        private BossExecutionHandler _executionHandler;
        private BossCoreLock _coreLock;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 Phase. </summary>
        private BossPhase _currentPhase = BossPhase.Phase1;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// Phase 전환 완료 시 발행.
        /// BossKnightAI / UI 등이 구독.
        /// </summary>
        public event Action<BossPhase> OnPhaseChanged;

        // ──────────────────────────────────────────
        // 프로퍼티 — EnemyBossBase abstract 구현
        // ──────────────────────────────────────────

        /// <summary>
        /// 보스 최대 체력. BossKnightDataSO.maxHp 반환.
        /// EnemyBossBase 의 HpRatio / InitializeHp 가 이 값을 사용.
        /// </summary>
        protected override float BossMaxHp
            => _bossData != null ? _bossData.maxHp : 1f;

        /// <summary>
        /// 넉백 초기 속도. BossKnightDataSO.knockbackForce 반환.
        /// </summary>
        protected override float BossKnockbackForce
            => _bossData != null ? _bossData.knockbackForce : 0f;

        /// <summary>
        /// 넉백 감속 비율. BossKnightDataSO.knockbackDecay 반환.
        /// </summary>
        protected override float BossKnockbackDecay
            => _bossData != null ? _bossData.knockbackDecay : 0.8f;

        /// <summary>
        /// iFrame 지속 시간. BossKnightDataSO.iFrameDuration 반환.
        /// </summary>
        protected override float BossIFrameDuration
            => _bossData != null ? _bossData.iFrameDuration : 0.2f;

        // ──────────────────────────────────────────
        // 프로퍼티 — BossKnight 전용
        // ──────────────────────────────────────────

        /// <summary> 현재 Phase. </summary>
        public BossPhase CurrentPhase => _currentPhase;

        /// <summary> 보스 DataSO. 서브 컴포넌트 참조용. </summary>
        public BossKnightDataSO BossData => _bossData;

        /// <summary>
        /// Phase 전환 중 무적 여부 외부 읽기.
        /// EnemyBossBase._isPhaseInvincible 래핑.
        /// </summary>
        public bool IsPhaseInvincible => _isPhaseInvincible;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트 자동 취득 + HP 초기화.
        /// base.Awake() 로 EnemyBossBase 초기화 수행.
        /// </summary>
        protected override void Awake()
        {
            // EnemyBossBase.Awake() — Rigidbody2D / SpriteRenderer 취득
            base.Awake();

            if (_bossData == null)
            {
                Debug.LogError("[BossKnight] BossKnightDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // EnemyBossBase 제공 API — BossMaxHp 기반으로 _currentHp 초기화
            InitializeHp();

            // 서브 컴포넌트 자동 취득
            _ai = GetComponent<BossKnightAI>();
            _phaseManager = GetComponent<BossPhaseManager>();
            _counterSystem = GetComponent<BossCounterSystem>();
            _shockwave = GetComponent<BossShockwave>();
            _executionHandler = GetComponent<BossExecutionHandler>();
            _coreLock = GetComponent<BossCoreLock>();
        }

        private void Start()
        {
            // 서브 컴포넌트 초기화 및 참조 주입
            _ai.Initialize(this, _bossData, _counterSystem);
            _ai.RegisterPatterns(_phase1Patterns, _phase2Patterns, _phase3Patterns);
            _ai.OnGroggyEnter += _executionHandler.OnGroggyEnter;
            _ai.OnGroggyExit += _executionHandler.OnGroggyExit;
            _ai.OnDilTimeEnter += () => { }; // 추후 UI 연결
            _ai.OnDilTimeExit += () => { };

            _phaseManager.Initialize(this, _bossData);

            _counterSystem.Initialize(this, _ai, _bossData);
            _counterSystem.RegisterPatterns(_phase1Patterns, _phase2Patterns, _phase3Patterns);

            _shockwave.Initialize(_bossData);

            _executionHandler.Initialize(this, _ai, _bossData);

            _coreLock.Initialize(this, _ai, _bossData);
            _coreLock.RegisterArmParts(
                _allParts.Find(p => p.PartType == BossPartType.ArmL),
                _allParts.Find(p => p.PartType == BossPartType.ArmR));

            // 모든 패턴 초기화
            InitializeAllPatterns();

            // Phase 1 시작
            InitializePhase(BossPhase.Phase1);

            // 플레이어 Transform 탐색
            var player = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (player.Length > 0)
                _ai.SetPlayer(player[0].transform);

            // BossPartComponent 이벤트 구독
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                part.OnPartUnlocked += HandlePartUnlocked;
            }
        }

        private void OnDestroy()
        {
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                part.OnPartUnlocked -= HandlePartUnlocked;
            }
        }

        // ══════════════════════════════════════════════════════
        // IDamageable — TakeDamage override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 피격 처리.
        ///
        /// [분기]
        ///   딜타임 상태      → base.TakeDamage() (HP 감소)
        ///   자물쇠 전부 해제 → base.TakeDamage() (HP 감소)
        ///   그 외            → 무시
        ///
        /// [_isPhaseInvincible / iFrame 체크]
        ///   EnemyBossBase.TakeDamage() 에서 진입 전에 처리.
        ///   이 override 는 조건 충족 시만 호출됨.
        /// </summary>
        public override void TakeDamage(DamageInfo info)
        {
            // 딜타임 상태 → 코어에 직접 피해 허용
            if (_ai.IsDilTime)
            {
                base.TakeDamage(info);
                return;
            }

            // 자물쇠 전부 해제 → 본체 피격 허용
            if (IsAllLocksCleared())
            {
                base.TakeDamage(info);
            }
            else
            {
                Debug.Log("[BossKnight] 본체 피격 → 자물쇠 미해제, 무시");
            }
        }

        // ══════════════════════════════════════════════════════
        // EnemyBossBase 확장점 override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TakeDamage 처리 후 Phase 전환 체크.
        /// EnemyBossBase.TakeDamage() 내부에서 호출.
        /// </summary>
        protected override void OnDamaged(DamageInfo info)
        {
            _phaseManager.CheckPhaseTransition(HpRatio);
        }

        /// <summary>
        /// 보스 사망 처리.
        /// Phase 3 에서만 실제 Die() 진행.
        /// Phase 1/2 는 PhaseManager 가 HP 회복으로 대신 처리.
        ///
        /// [EnemyBossBase.Die() 흐름]
        ///   _isDead = true → StopAllCoroutines → OnDead 이벤트 → OnBossDied()
        /// </summary>
        protected override void Die()
        {
            if (_currentPhase != BossPhase.Phase3) return;
            base.Die();
        }

        /// <summary>
        /// Die() 완료 후 보스 전용 마무리.
        /// EnemyBossBase.OnBossDied() 확장점.
        /// </summary>
        protected override void OnBossDied()
        {
            Debug.Log("[BossKnight] 보스 처치 완료!");
            // 추후 처형 연출 / 엔딩 이벤트 발행
        }

        // ══════════════════════════════════════════════════════
        // Phase 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Phase 전환 시 호출.
        /// 자물쇠 초기화 + 오브젝트 활성화 + AI 패턴 전환.
        /// </summary>
        public void InitializePhase(BossPhase phase)
        {
            _currentPhase = phase;

            SetPhaseObjects(phase);

            foreach (var part in _allParts)
                part?.Initialize(phase);

            _ai.SwitchPhase(phase);
            _coreLock.DeactivateCore();

            Debug.Log($"[BossKnight] Phase 초기화 완료 → {phase}");
        }

        /// <summary>
        /// Phase 별 오브젝트 활성/비활성 처리.
        /// </summary>
        private void SetPhaseObjects(BossPhase phase)
        {
            foreach (var obj in _phase1Objects) obj?.SetActive(false);
            foreach (var obj in _phase2Objects) obj?.SetActive(false);
            foreach (var obj in _phase3Objects) obj?.SetActive(false);

            var activeList = phase switch
            {
                BossPhase.Phase1 => _phase1Objects,
                BossPhase.Phase2 => _phase2Objects,
                BossPhase.Phase3 => _phase3Objects,
                _ => _phase1Objects,
            };

            foreach (var obj in activeList) obj?.SetActive(true);
        }

        // ══════════════════════════════════════════════════════
        // Phase 전환 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Phase 전환 실행. BossPhaseManager 에서 호출.
        /// </summary>
        public void EnterPhaseTransition(BossPhase nextPhase)
        {
            StartCoroutine(PhaseTransitionRoutine(nextPhase));
        }

        /// <summary>
        /// Phase 전환 코루틴.
        ///
        /// [흐름]
        ///   _isPhaseInvincible = true (EnemyBossBase 필드)
        ///   → 충격파 발동
        ///   → 전환 애니메이션 대기 (추후 Animator 이벤트로 교체)
        ///   → Phase3 진입 시 RestoreFullHp() (EnemyBossBase API)
        ///   → InitializePhase()
        ///   → _isPhaseInvincible = false
        ///   → AI 복귀 + 이벤트 발행
        /// </summary>
        private IEnumerator PhaseTransitionRoutine(BossPhase nextPhase)
        {
            _isPhaseInvincible = true;
            _ai.EnterPhaseTransition();

            TriggerShockwave();

            // Phase 전환 연출 대기 (추후 Animator 이벤트 대체)
            yield return new WaitForSeconds(2.0f);

            // Phase 3 진입 시 HP 완전 회복 — EnemyBossBase 제공 API
            if (nextPhase == BossPhase.Phase3)
            {
                RestoreFullHp();
                Debug.Log("[BossKnight] Phase 3 진입 — HP 100% 회복");
            }

            InitializePhase(nextPhase);

            _isPhaseInvincible = false;
            _ai.ExitPhaseTransition();

            OnPhaseChanged?.Invoke(nextPhase);
            Debug.Log($"[BossKnight] Phase 전환 완료 → {nextPhase}");
        }

        // ══════════════════════════════════════════════════════
        // 패턴 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 모든 Phase 패턴 컴포넌트 초기화 및 이벤트 구독.
        /// Start() 에서 1회 호출.
        /// </summary>
        private void InitializeAllPatterns()
        {
            var allPatterns = new List<BossPatternBase>();
            allPatterns.AddRange(_phase1Patterns);
            allPatterns.AddRange(_phase2Patterns);
            allPatterns.AddRange(_phase3Patterns);

            foreach (var pattern in allPatterns)
            {
                if (pattern == null) continue;
                pattern.Initialize(_bossData, _ai);
                pattern.OnPatternGroggy += () => _ai.EnterGroggy();
            }
        }

        // ══════════════════════════════════════════════════════
        // 공용 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 발동. BossShockwave 에 위임.
        /// Phase 전환 / Groggy 회복 / DilTime 종료 시 호출.
        /// </summary>
        public void TriggerShockwave()
        {
            _shockwave?.Trigger(transform.position);
        }

        /// <summary>
        /// 그로기 진입. BossKnightAI 에 위임.
        /// 외부(패턴, 카운터) 에서 직접 호출 가능.
        /// </summary>
        public void EnterGroggy(float duration = -1f)
        {
            _ai.EnterGroggy(duration);
        }

        /// <summary>
        /// 딜타임 진입. BossCoreLock 이 조건 확인 후 호출.
        /// </summary>
        public void EnterDilTime(float duration = -1f)
        {
            _ai.EnterDilTime(duration);
        }

        // ══════════════════════════════════════════════════════
        // 내부 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 Phase 의 모든 부위 자물쇠 해제 여부 확인.
        /// TakeDamage 분기 조건에 사용.
        /// </summary>
        private bool IsAllLocksCleared()
        {
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                if (part.PartType == BossPartType.Core) continue;
                if (!part.IsCurrentPhaseActive(_currentPhase)) continue;
                if (!part.IsUnlocked) return false;
            }
            return true;
        }

        /// <summary>
        /// BossPartComponent.OnPartUnlocked 이벤트 수신.
        /// 코어 활성 조건 체크 위임.
        /// </summary>
        private void HandlePartUnlocked(BossPartType partType)
        {
            Debug.Log($"[BossKnight] 부위 해제 → {partType}");
            _coreLock.CheckCoreActivation();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        /// <summary>
        /// Scene 뷰 보스 상태 + 충격파 범위 시각화.
        /// EnemyBossBase.OnDrawGizmosSelected() 기본 HP 표시에 추가.
        /// </summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (_bossData == null) return;

            // 충격파 범위
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            UnityEditor.Handles.DrawWireDisc(
                transform.position, Vector3.forward, _bossData.shockwaveRadius);

            // Phase + 무적 상태 표시
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 4.0f,
                $"[BOSS] {_currentPhase}" +
                (_isPhaseInvincible ? "  [Phase 무적]" : ""));
        }
#endif
    }
}