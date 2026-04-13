using UnityEngine;

namespace Zeke.UI
{
    public class UIElement : MonoBehaviour
    {
        [SerializeField] private UIWindow window;

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

        private void Awake()
        {
            window.Add(this);
        }
    }
}