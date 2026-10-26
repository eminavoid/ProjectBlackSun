using UnityEngine;

namespace Zeke.UI
{
    [DefaultExecutionOrder(-1)]
    public class UIElement : MonoBehaviour
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public Component Element { get; private set; }

        [SerializeField, HideInInspector] private UIWindow window;

        public T GetElement<T>() where T : Component
        {
            return (T)Element;
        }

        private void OnValidate()
        {
            GetParentWindow();
        }

        private void OnTransformParentChanged()
        {
            UnregisterElement();
            GetParentWindow();
            RegisterElement();
        }

        private void Awake()
        {
            if (window == null)
            {
                GetParentWindow();
            }

            if (!window.Contains(this))
            {
                RegisterElement();
            }
        }

        private void OnDestroy()
        {
            UnregisterElement();
        }

        private void GetParentWindow()
        {
            window = GetComponentInParent<UIWindow>(true);
        }

        private void RegisterElement()
        {
            if (window == null) return;
            window.Add(this);
        }

        private void UnregisterElement()
        {
            if (window == null) return;
            window.Remove(this);
        }
    }
}