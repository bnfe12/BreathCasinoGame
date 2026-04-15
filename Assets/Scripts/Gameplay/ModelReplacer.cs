using UnityEngine;

#pragma warning disable CS0649
namespace BreathCasino.Gameplay
{
    /// <summary>
    /// Заменяет blockout-примитивы на финальные 3D модели.
    /// Использование: прикрепить к GameObject с примитивом, назначить prefab модели.
    /// </summary>
    public class ModelReplacer : MonoBehaviour
    {
        [Header("Model Settings")]
        [Tooltip("Prefab финальной модели для замены примитива")]
        [SerializeField] private GameObject modelPrefab;
        
        [Tooltip("Автоматически заменить при старте игры")]
        [SerializeField] private bool replaceOnStart = false;
        
        [Tooltip("Сохранить масштаб blockout-объекта")]
        [SerializeField] private bool keepBlockoutScale = true;
        
        [Tooltip("Offset позиции модели относительно blockout")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        
        [Tooltip("Offset поворота модели")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        private GameObject _instantiatedModel;
        private MeshRenderer _blockoutRenderer;

        private void Start()
        {
            if (replaceOnStart && modelPrefab != null)
            {
                ReplaceWithModel();
            }
        }

        /// <summary>
        /// Заменяет blockout на модель
        /// </summary>
        public void ReplaceWithModel()
        {
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[ModelReplacer] No model prefab assigned on {gameObject.name}");
                return;
            }

            // Удаляем старую модель если есть
            if (_instantiatedModel != null)
            {
                DestroyImmediate(_instantiatedModel);
            }

            // Создаем модель
            _instantiatedModel = Instantiate(modelPrefab, transform);
            _instantiatedModel.name = modelPrefab.name;
            _instantiatedModel.transform.localPosition = positionOffset;
            _instantiatedModel.transform.localRotation = Quaternion.Euler(rotationOffset);
            
            if (keepBlockoutScale)
            {
                _instantiatedModel.transform.localScale = Vector3.one;
            }

            // Скрываем blockout
            HideBlockout();
            
            Debug.Log($"[ModelReplacer] Replaced {gameObject.name} with {modelPrefab.name}");
        }

        /// <summary>
        /// Возвращает blockout (для тестирования)
        /// </summary>
        public void RestoreBlockout()
        {
            if (_instantiatedModel != null)
            {
                DestroyImmediate(_instantiatedModel);
                _instantiatedModel = null;
            }

            ShowBlockout();
            Debug.Log($"[ModelReplacer] Restored blockout on {gameObject.name}");
        }

        private void HideBlockout()
        {
            if (_blockoutRenderer == null)
            {
                _blockoutRenderer = GetComponent<MeshRenderer>();
            }

            if (_blockoutRenderer != null)
            {
                _blockoutRenderer.enabled = false;
            }
        }

        private void ShowBlockout()
        {
            if (_blockoutRenderer == null)
            {
                _blockoutRenderer = GetComponent<MeshRenderer>();
            }

            if (_blockoutRenderer != null)
            {
                _blockoutRenderer.enabled = true;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Replace with Model")]
        private void EditorReplaceWithModel()
        {
            ReplaceWithModel();
        }

        [ContextMenu("Restore Blockout")]
        private void EditorRestoreBlockout()
        {
            RestoreBlockout();
        }
#endif
    }
}
#pragma warning restore CS0649
