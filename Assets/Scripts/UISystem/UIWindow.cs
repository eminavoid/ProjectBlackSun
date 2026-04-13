using System.Collections.Generic;
using UnityEngine;

namespace Zeke.UI
{
    public class UIWindow : MonoBehaviour
    {
        private readonly Dictionary<string, UIElement> elements = new Dictionary<string, UIElement>();

        public UIElement TryGetElement(string name)
        {
            if (elements.TryGetValue(name, out UIElement uiElement))
            {
                return uiElement;
            }

            return null;
        }

        public T TryGetElement<T>(string name) where T : Component
        {
            if (elements.TryGetValue(name, out UIElement uiElement))
            {
                return uiElement.GetElement<T>();
            }

            return null;
        }

        public void Add(UIElement element)
        {
            if (elements.ContainsKey(element.Name))
            {
                Debug.LogWarning($"Duplicate '{element.Name}' UIElement naming:", element);
            }

            elements.Add(element.Name, element);
        }

        public void DestroyWindow()
        {
            Destroy(gameObject);
        }
    }
}
