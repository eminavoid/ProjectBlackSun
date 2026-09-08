using UnityEngine;

namespace Zeke.UI
{
    public class UIElement : MonoBehaviour
    {
        [SerializeField] private UIWindow window;

        public UIWindow Window => window;

        [field: Space]

        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public Component Element { get; private set; }

        public T GetElement<T>() where T : Component
        {
            return (T)Element;
        }

        private void Reset()
        {
            window = GetComponentInParent<UIWindow>();
        }

        public void Bind(UIWindow owner, string elementName, Component element)
        {
            window = owner;
            Name = elementName;
            Element = element;
            if (window != null) window.Add(this);
        }

        private void Awake()
        {
            if (window == null) window = GetComponentInParent<UIWindow>();
            if (window != null) window.Add(this);
        }
    }
}