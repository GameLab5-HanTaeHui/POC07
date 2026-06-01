// ============================================================
// BossKnight.cs  v1.0
// 봉인된 기사 보스 루트 컴포넌트
//
// [역할]
//   보스 시스템의 중심점.
//   모든 보스 서브 컴포넌트를 초기화하고 참조를 주입.
//   Phase 전환 / 충격파 / 무적 상태 관리.
//   EnemyBase 상속으로 IDamageable 구현.
//
// [EnemyKnight 와의 차이]
//   EnemyKnight : 단일 Phase, 자물쇠 해제 = 사망 가능
//   BossKnight  : 3 Phase, Phase 전환 시 HP 회복
//                 자물쇠 해제 = 약점 노출 + 딜타임 구조
//                 TakeDamage 에 Phase 전환 체크 추가
//
// [DataSO 참조 구조]
//   BossKnight._bossData (Inspector 연결 — 유일한 연결 지점)
//   → Initialize() 에서 모든 서브 컴포넌트에 주입
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
    /// 봉인된 기사 보스 루트 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 흐름]
    ///   IDamageable.TakeDamage(info)
    ///     → _isPhaseInvincible 체크 (Phase 전환 중 무적)
    ///     → BossPartComponent 피격 여부 판단
    ///       → 자물쇠 미해제 부위 → 무시
    ///       → 자물쇠 해제 부위 / 코어 딜타임 → base.TakeDamage()
    ///     → HP 임계값 도달 → BossPhaseManager.TryTransition()
    ///
    /// [Phase 전환 흐름]
    ///   BossPhaseManager.TryTransition(nextPhase)
    ///     → BossKnight.EnterPhaseTransition(nextPhase)
    ///       → _isPhaseInvincible = true
    ///       → TriggerShockwave()
    ///       → Phase 전환 애니메이션 재생
    ///       → Phase3 진입 시 HP 회복
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
    public class BossKnight : EnemyBase
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

        [Tooltip("Phase 1 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase1Patterns = new();

        [Tooltip("Phase 2 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase2Patterns = new();

        [Tooltip("Phase 3 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _phase3Patterns = new();

        [Header("── Phase별 전용 오브젝트 ──────────────────────")]

        [Tooltip("Phase 1 전용 오브젝트 목록. Phase 1 에서만 활성.")]
        [SerializeField] private List<GameObject> _phase1Objects = new();

        [Tooltip("Phase 2 전용 오브젝트 목록.")]
        [SerializeField] private List<GameObject> _phase2Objects = new();

        [Tooltip("Phase 3 전용 오브젝트 목록.")]
        [SerializeField] private List<GameObject> _phase3Objects = new();

        // ──────────────────────────────────────────
        // 서브 컴포넌트 참조 (자동 취득)
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

        /// <summary>
        /// Phase 전환 중 무적 여부.
        /// true 상태에서 TakeDamage 는 완전 무시.
        /// </summary>
        private bool _isPhaseInvincible;

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
        // 프로퍼티
        // ──────────────────────────────────────────

        public BossPhase CurrentPhase => _currentPhase;
        public BossKnightDataSO BossData => _bossData;
        public bool IsPhaseInvincible => _isPhaseInvincible;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            // EnemyBase.Awake() — Rigidbody2D / SpriteRenderer 취득
            // BossKnight 는 _bossData 를 _settings 대신 사용하므로
            // EnemyBase._settings 는 null 허용 (BossKnight 에서 override TakeDamage)
            base.Awake();

            _ai = GetComponent<BossKnightAI>();
            _phaseManager = GetComponent<BossPhaseManager>();
            _counterSystem = GetComponent<BossCounterSystem>();
            _shockwave = GetComponent<BossShockwave>();
            _executionHandler = GetComponent<BossExecutionHandler>();
            _coreLock = GetComponent<BossCoreLock>();

            if (_bossData == null)
            {
                Debug.LogError("[BossKnight] BossKnightDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // 체력 초기화 (EnemyBase._settings 대신 _bossData 사용)
            _currentHp = _bossData.maxHp;
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
                // 패턴 그로기 이벤트 구독
                pattern.OnPatternGroggy += () => _ai.EnterGroggy();
            }
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

            // Phase별 오브젝트 활성/비활성
            SetPhaseObjects(phase);

            // 모든 자물쇠 초기화
            foreach (var part in _allParts)
                part?.Initialize(phase);

            // AI 패턴 목록 전환
            _ai.SwitchPhase(phase);

            // 코어 비활성
            _coreLock.DeactivateCore();

            Debug.Log($"[BossKnight] Phase 초기화 완료 → {phase}");
        }

        private void SetPhaseObjects(BossPhase phase)
        {
            // 전부 비활성 후 해당 Phase 오브젝트만 활성
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
        // TakeDamage override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 피격 처리.
        ///
        /// [분기]
        ///   Phase 전환 중 무적 → 완전 무시
        ///   딜타임 상태 → 코어 직접 피격 허용 (base.TakeDamage)
        ///   자물쇠 미해제 부위 피격 → 무시 (BossPartComponent 단에서 처리)
        ///   자물쇠 전부 해제 → base.TakeDamage() (HP 감소)
        ///   → HP 임계값 도달 → BossPhaseManager.TryTransition()
        /// </summary>
        public override void TakeDamage(DamageInfo info)
        {
            // Phase 전환 중 무적
            if (_isPhaseInvincible) return;

            // 딜타임 상태 → 코어에 직접 피해 허용
            if (_ai.IsDilTime)
            {
                base.TakeDamage(info);
                return;
            }

            // 일반 피격은 BossPartComponent / LockComponent 단에서
            // PlayerWeaponHitboxManager 가 레이어로 분기 처리
            // (Enemy 레이어 직접 타격 시)
            // 자물쇠가 전부 해제된 경우만 base 호출
            if (IsAllLocksCleared())
            {
                base.TakeDamage(info);
            }
            else
            {
                Debug.Log("[BossKnight] 본체 피격 → 자물쇠 미해제, 무시");
            }
        }

        /// <summary>
        /// HP 변화 후 Phase 전환 조건 체크.
        /// EnemyBase.TakeDamage() 에서 OnDamaged() 호출.
        /// </summary>
        protected override void OnDamaged(DamageInfo info)
        {
            _phaseManager.CheckPhaseTransition(HpRatio);
        }

        /// <summary>
        /// 보스 사망 처리 — Phase 3 에서만 실제 사망.
        /// Phase 1/2 는 PhaseManager 가 처리 (HP 회복).
        /// </summary>
        protected override void Die()
        {
            if (_currentPhase != BossPhase.Phase3) return;
            base.Die();
            Debug.Log("[BossKnight] 보스 처치!");
        }

        // ══════════════════════════════════════════════════════
        // Phase 전환 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Phase 전환 실행. BossPhaseManager 에서 호출.
        /// </summary>
        public void EnterPhaseTransition(BossPhase nextPhase)
        {
            StartCoroutine(PhaseTransitionRoutine(nextPhase));
        }

        private IEnumerator PhaseTransitionRoutine(BossPhase nextPhase)
        {
            // 무적 + AI 전환 상태
            _isPhaseInvincible = true;
            _ai.EnterPhaseTransition();

            // 충격파
            TriggerShockwave();

            // Phase 전환 애니메이션 대기 (추후 Animator 이벤트로 교체)
            yield return new WaitForSeconds(2.0f);

            // Phase 3 진입 시 HP 회복
            if (nextPhase == BossPhase.Phase3)
            {
                _currentHp = _bossData.maxHp;
                Debug.Log("[BossKnight] Phase 3 진입 — HP 100% 회복");
            }

            // Phase 초기화
            InitializePhase(nextPhase);

            // 무적 해제
            _isPhaseInvincible = false;
            _ai.ExitPhaseTransition();

            OnPhaseChanged?.Invoke(nextPhase);
            Debug.Log($"[BossKnight] Phase 전환 완료 → {nextPhase}");
        }

        // ══════════════════════════════════════════════════════
        // 공용 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 충격파 발동.
        /// BossShockwave 에 위임.
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

        private bool IsAllLocksCleared()
        {
            foreach (var part in _allParts)
            {
                if (part == null) continue;
                if (!part.IsCurrentPhaseActive(_currentPhase)) continue;
                if (!part.IsUnlocked) return false;
            }
            return true;
        }

        private void HandlePartUnlocked(BossPartType partType)
        {
            Debug.Log($"[BossKnight] 부위 해제 → {partType}");
            _coreLock.CheckCoreActivation();
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
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (_bossData == null) return;

            // 충격파 범위
            UnityEditor.Handles.color = new Color(1f, 0.3f, 0.3f, 0.2f);
            UnityEditor.Handles.DrawWireDisc(
                transform.position, Vector3.forward, _bossData.shockwaveRadius);

            // Phase 표시
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 4.0f,
                $"[BOSS] {_currentPhase}  HP:{_currentHp:F0}/{_bossData.maxHp:F0}" +
                (_isPhaseInvincible ? " [무적]" : ""));
        }
#endif
    }
}