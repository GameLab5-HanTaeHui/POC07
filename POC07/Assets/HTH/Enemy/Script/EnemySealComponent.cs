// ============================================================
// EnemySealComponent.cs  v1.0
// 적 봉인 상태 관리 컴포넌트
//
// [역할]
//   SealProjectile 이 적에 명중했을 때 봉인을 수신·관리.
//   EnemyAI 가 행동 실행 직전 IsSealedAction() 을 체크하여
//   봉인된 행동이면 스킵.
//
// [봉인 적용 흐름]
//   SealProjectile.OnTriggerEnter2D()
//     → EnemySealComponent.ApplySeal(SealDataSO)
//         → _activeSeals 딕셔너리에 (SealType, 잔여시간) 등록
//         → 최대 봉인 수(maxSealCount) 초과 시 가장 오래된 봉인 제거
//         → OnSealApplied 이벤트 발행
//
// [봉인 해제 흐름]
//   Update() 에서 잔여시간 감산
//     → 0 이하 시 해당 SealType 제거
//     → OnSealRemoved 이벤트 발행
//
// [EnemyAI 연동]
//   EnemyAI 가 행동 실행 직전:
//     if (IsSealedAction(SealType.Dash)) return; // 돌진 스킵
//   Guard 봉인은 EnemyKnight.TakeDamage 에서 체크:
//     if (IsSealedAction(SealType.Guard)) → 방패 무시하고 피격 처리
//
// [중복 봉인 규칙]
//   같은 SealType 재명중 → 타이머 리셋 (스택 없음)
//   다른 SealType 은 maxSealCount 까지 동시 적용
//   maxSealCount 초과 시 타이머가 가장 적게 남은 봉인 제거 후 추가
//
// [비주얼 피드백]
//   봉인 활성 중 SpriteRenderer 를 sealColor 로 주기적 깜빡임
//   봉인 오버레이 SpriteRenderer 활성화 (자식 오브젝트)
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
    /// 적 봉인 상태 관리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [부착 위치]
    ///   EnemyBase 를 가진 오브젝트와 같은 게임오브젝트에 부착.
    ///   EnemyKnight, EnemyDummy 등 모든 적에 공통 사용 가능.
    ///
    /// [EnemyAI 에서 사용 예시]
    ///   // 돌진 행동 실행 전
    ///   if (_sealComponent != null && _sealComponent.IsSealedAction(SealType.Dash))
    ///       return; // 봉인됨 → 돌진 스킵
    ///
    /// [EnemyKnight 에서 Guard 봉인 체크 예시]
    ///   bool guardSealed = _sealComponent != null
    ///       && _sealComponent.IsSealedAction(SealType.Guard);
    ///   if (!guardSealed && IsFrontAttack(info)) return; // 방패 막힘
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemySealComponent : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 비주얼 연결 ──────────────────────")]

        /// <summary>
        /// 봉인 상태 오버레이 SpriteRenderer.
        /// 적 위에 자물쇠 아이콘을 표시하는 자식 오브젝트.
        /// 미연결 시 오버레이 표시 생략.
        /// </summary>
        [Tooltip("봉인 오버레이 스프라이트 렌더러. 적 위 자물쇠 아이콘. 미연결 시 생략.")]
        [SerializeField] private SpriteRenderer _overlayRenderer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 봉인 플래시에 사용할 본체 SpriteRenderer.
        /// Awake 에서 같은 오브젝트의 SpriteRenderer 자동 취득.
        /// </summary>
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 봉인 상태 — 핵심 데이터
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 활성 봉인 목록.
        /// Key: SealType / Value: 잔여 지속 시간 (초).
        /// Update() 에서 매 프레임 감산, 0 이하 시 제거.
        ///
        /// [Dictionary 선택 이유]
        ///   같은 SealType 중복 방지가 자연스럽게 처리됨.
        ///   IsSealedAction(SealType) 검색이 O(1).
        /// </summary>
        private readonly Dictionary<SealType, float> _activeSeals
            = new Dictionary<SealType, float>();

        /// <summary>
        /// 봉인 적용 순서 추적 큐.
        /// maxSealCount 초과 시 가장 먼저 적용된 타입을 제거.
        ///
        /// [큐 vs 딕셔너리 역할 분리]
        ///   딕셔너리: SealType → 잔여시간 (빠른 조회)
        ///   큐: 적용 순서 기억 (오래된 봉인 제거)
        ///   같은 SealType 재적용 시 큐에서 해당 항목 제거 후 재삽입.
        /// </summary>
        private readonly Queue<SealType> _sealOrder
            = new Queue<SealType>();

        // ──────────────────────────────────────────
        // 내부 상태 — 비주얼
        // ──────────────────────────────────────────

        /// <summary> 현재 실행 중인 플래시 코루틴. </summary>
        private Coroutine _flashCoroutine;

        /// <summary> 봉인 비활성 시 원래 스프라이트 색상. </summary>
        private Color _originalColor;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 봉인이 새로 적용될 때 발행.
        /// 파라미터: 적용된 SealType.
        /// UI 아이콘 표시, 사운드 재생 등에서 구독.
        /// </summary>
        public event System.Action<SealType> OnSealApplied;

        /// <summary>
        /// 봉인이 해제될 때 발행.
        /// 파라미터: 해제된 SealType.
        /// UI 아이콘 제거, 해제 이펙트 등에서 구독.
        /// </summary>
        public event System.Action<SealType> OnSealRemoved;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 봉인이 하나라도 활성 중인지 여부.
        /// EnemyBase 의 피격 플래시와 충돌 방지에 사용.
        /// </summary>
        public bool HasAnySeal => _activeSeals.Count > 0;

        /// <summary>
        /// 현재 활성 봉인 수.
        /// </summary>
        public int SealCount => _activeSeals.Count;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트 참조 취득 및 초기 색상 캐싱.
        /// </summary>
        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;

            // 오버레이는 기본 비활성
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;
        }

        /// <summary>
        /// 매 프레임 봉인 타이머 감산 및 만료된 봉인 해제.
        /// </summary>
        private void Update()
        {
            if (_activeSeals.Count == 0) return;

            // 만료된 봉인 수집 (foreach 중 딕셔너리 수정 불가)
            _expiredBuffer.Clear();

            foreach (var pair in _activeSeals)
            {
                float remaining = pair.Value - Time.deltaTime;
                if (remaining <= 0f)
                    _expiredBuffer.Add(pair.Key);
                else
                    _activeSeals[pair.Key] = remaining;
            }

            // 만료 봉인 제거
            foreach (SealType expired in _expiredBuffer)
                RemoveSeal(expired);
        }

        /// <summary>
        /// 만료 봉인 임시 버퍼. GC 방지를 위해 필드로 선언.
        /// </summary>
        private readonly List<SealType> _expiredBuffer = new List<SealType>();

        /// <summary>
        /// 오브젝트 파괴 시 모든 봉인 해제 및 색상 복원.
        /// </summary>
        private void OnDestroy()
        {
            ForceReleaseAll();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — SealProjectile 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인을 적용한다.
        /// SealProjectile 이 적 명중 시 호출.
        ///
        /// [같은 타입 재적용]
        ///   기존 타이머를 sealDuration 으로 리셋. (스택 없음)
        ///   큐에서 해당 타입 제거 후 재삽입.
        ///
        /// [maxSealCount 초과 시]
        ///   큐에서 가장 오래된 SealType 을 꺼내 제거 후 새 봉인 추가.
        /// </summary>
        /// <param name="data">적용할 봉인 데이터 SO</param>
        public void ApplySeal(SealDataSO data)
        {
            if (data == null) return;

            SealType type = data.sealType;

            // 같은 타입 재적용 → 타이머 리셋
            if (_activeSeals.ContainsKey(type))
            {
                _activeSeals[type] = data.sealDuration;
                RefreshSealOrder(type);

                Debug.Log($"[EnemySealComponent] 봉인 타이머 리셋: {type} ({data.sealDuration:F1}초)");
                return;
            }

            // maxSealCount 초과 시 가장 오래된 봉인 제거
            while (_sealOrder.Count >= data.maxSealCount && _sealOrder.Count > 0)
            {
                SealType oldest = _sealOrder.Dequeue();
                RemoveSeal(oldest);
            }

            // 새 봉인 등록
            _activeSeals[type] = data.sealDuration;
            _sealOrder.Enqueue(type);

            // 비주얼 피드백 시작
            StartSealVisual(data);

            OnSealApplied?.Invoke(type);

            Debug.Log($"[EnemySealComponent] 봉인 적용: {type} ({data.sealDuration:F1}초) " +
                      $"현재 봉인 수: {_activeSeals.Count}");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — EnemyAI / EnemyKnight 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 봉인 타입이 현재 활성 중인지 확인.
        ///
        /// [EnemyAI 사용 예시]
        ///   if (IsSealedAction(SealType.Dash)) return;
        ///   ExecuteDash();
        ///
        /// [EnemyKnight Guard 체크 예시]
        ///   bool guardBroken = IsSealedAction(SealType.Guard);
        ///   if (!guardBroken &amp;&amp; IsFrontAttack(info)) return;
        /// </summary>
        /// <param name="sealType">확인할 봉인 타입</param>
        /// <returns>해당 타입 봉인이 활성 중이면 true</returns>
        public bool IsSealedAction(SealType sealType)
        {
            return _activeSeals.ContainsKey(sealType);
        }

        /// <summary>
        /// 특정 봉인 타입의 잔여 시간 반환.
        /// 활성 중이 아니면 0 반환.
        /// UI 타이머 바 표시에 사용.
        /// </summary>
        /// <param name="sealType">조회할 봉인 타입</param>
        /// <returns>잔여 시간 (초). 봉인 없으면 0.</returns>
        public float GetRemainingTime(SealType sealType)
        {
            return _activeSeals.TryGetValue(sealType, out float t) ? t : 0f;
        }

        /// <summary>
        /// 모든 봉인을 즉시 강제 해제.
        /// 자물쇠 해제 완료, 적 사망 등 특수 상황에서 호출.
        /// </summary>
        public void ForceReleaseAll()
        {
            var types = new List<SealType>(_activeSeals.Keys);
            foreach (SealType t in types)
                RemoveSeal(t);

            _sealOrder.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 봉인 제거
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 타입 봉인 제거.
        /// 타이머 만료 or 강제 해제 시 호출.
        /// </summary>
        /// <param name="sealType">제거할 봉인 타입</param>
        private void RemoveSeal(SealType sealType)
        {
            if (!_activeSeals.ContainsKey(sealType)) return;

            _activeSeals.Remove(sealType);
            RemoveFromOrder(sealType);

            // 남은 봉인 없으면 비주얼 종료
            if (_activeSeals.Count == 0)
                StopSealVisual();

            OnSealRemoved?.Invoke(sealType);

            Debug.Log($"[EnemySealComponent] 봉인 해제: {sealType} " +
                      $"남은 봉인: {_activeSeals.Count}");
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 큐 관리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 큐에서 특정 SealType 을 제거하고 뒤에 다시 삽입.
        /// 같은 타입 재적용 시 우선순위 갱신에 사용.
        /// </summary>
        private void RefreshSealOrder(SealType sealType)
        {
            RemoveFromOrder(sealType);
            _sealOrder.Enqueue(sealType);
        }

        /// <summary>
        /// 큐에서 특정 SealType 항목 제거.
        /// Queue 는 중간 제거 불가 → 전체 재구성.
        /// </summary>
        private void RemoveFromOrder(SealType sealType)
        {
            int count = _sealOrder.Count;
            for (int i = 0; i < count; i++)
            {
                SealType t = _sealOrder.Dequeue();
                if (t != sealType)
                    _sealOrder.Enqueue(t);
            }
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 비주얼 피드백
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 비주얼 시작.
        /// 오버레이 활성화 + 색상 플래시 코루틴 시작.
        /// </summary>
        private void StartSealVisual(SealDataSO data)
        {
            // 오버레이 스프라이트 적용
            if (_overlayRenderer != null)
            {
                if (data.sealOverlaySprite != null)
                    _overlayRenderer.sprite = data.sealOverlaySprite;

                _overlayRenderer.color = data.sealColor;
                _overlayRenderer.enabled = true;
            }

            // 기존 플래시 코루틴 중단 후 재시작
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            if (_spriteRenderer != null)
                _flashCoroutine = StartCoroutine(SealFlashRoutine(data));
        }

        /// <summary>
        /// 봉인 비주얼 종료.
        /// 오버레이 비활성화 + 색상 원복.
        /// </summary>
        private void StopSealVisual()
        {
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.color = _originalColor;
        }

        /// <summary>
        /// 봉인 색상 깜빡임 코루틴.
        /// 봉인 활성 중 sealColor ↔ 원래 색상 교대.
        /// 봉인이 모두 해제되면 자동 종료.
        ///
        /// [EnemyBase.HitFlashRoutine 과의 충돌 방지]
        ///   봉인 플래시는 interval 이 길어 (0.3~0.6초)
        ///   피격 플래시 (0.07초) 와 겹쳐도 시각적으로 구분 가능.
        ///   추후 EnemyBase 와 통합 고려 가능.
        /// </summary>
        private IEnumerator SealFlashRoutine(SealDataSO data)
        {
            float interval = data.sealFlashInterval;

            while (_activeSeals.Count > 0)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.color = data.sealColor;

                yield return new WaitForSeconds(interval);

                if (_spriteRenderer != null)
                    _spriteRenderer.color = _originalColor;

                yield return new WaitForSeconds(interval);
            }

            // 루프 종료 후 색상 원복 보장
            if (_spriteRenderer != null)
                _spriteRenderer.color = _originalColor;
        }

        // ══════════════════════════════════════════════════════
        // 디버그
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_activeSeals.Count == 0) return;

            UnityEditor.Handles.color = Color.blue;
            int i = 0;
            foreach (var pair in _activeSeals)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * (1.8f + i * 0.3f),
                    $"[봉인] {pair.Key} : {pair.Value:F1}초");
                i++;
            }
        }
#endif
    }
}