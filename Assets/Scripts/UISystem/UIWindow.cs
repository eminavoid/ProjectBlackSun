using System.Collections.Generic;
using UnityEngine;

namespace Zeke.UI
{
    public class UIWindow : MonoBehaviour
    {
        private readonly Dictionary<string, UIElement> elements = new Dictionary<string, UIElement>();

        public UIElement TryGetElement(string name)
        {
            if (TryGetCachedElement(name, out UIElement uiElement))
            {
                return uiElement;
            }

            RegisterChildElements();
            TryGetCachedElement(name, out uiElement);
            return uiElement;
        }

        public T TryGetElement<T>(string name) where T : Component
        {
            UIElement uiElement = TryGetElement(name);
            return uiElement != null ? uiElement.GetElement<T>() : null;
        }

        private bool TryGetCachedElement(string name, out UIElement uiElement)
        {
            return elements.TryGetValue(name, out uiElement);
        }

        private void RegisterChildElements()
        {
            UIElement[] childElements = GetComponentsInChildren<UIElement>(true);
            for (int i = 0; i < childElements.Length; i++)
            {
                UIElement element = childElements[i];
                if (element == null || element.Window != this) continue;
                elements[element.Name] = element;
            }
        }

        public void Add(UIElement element)
        {
            if (elements.ContainsKey(element.Name))
            {
                Debug.LogWarning($"Duplicate '{element.Name}' UIElement naming:", element);
            }

            elements[element.Name] = element;
        }

        public void DestroyWindow()
        {
            Destroy(gameObject);
        }
    }
}
