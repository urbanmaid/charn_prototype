using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Installed by MenuSceneManager so existing binary scenes need no migration.
[DefaultExecutionOrder(-100)]
public sealed class MenuControllerNavigation : MonoBehaviour
{
    private sealed class Panel
    {
        public Transform root;
        public GameObject previous;
        public Action close;
    }

    private static MenuControllerNavigation instance;
    private readonly List<Panel> panels = new List<Panel>();
    private readonly List<Selectable> candidates = new List<Selectable>();
    private RectTransform indicator;
    private bool controllerActive;
    private GameObject lastSelected;
    private GameObject pendingRestore;

    private void Awake()
    {
        instance = this;
        controllerActive = Gamepad.current != null;
    }

    private void Start()
    {
        EventSystem system = EventSystem.current;
        if (system == null)
            system = new GameObject("Menu EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        // Only one module may dispatch navigation/submit for a given press.
        foreach (BaseInputModule module in system.GetComponents<BaseInputModule>())
            if (!(module is InputSystemUIInputModule)) module.enabled = false;

        InputSystemUIInputModule input = system.GetComponent<InputSystemUIInputModule>();
        if (input == null) input = system.gameObject.AddComponent<InputSystemUIInputModule>();
        input.enabled = true;
        input.AssignDefaultActions();
        input.deselectOnBackgroundClick = false;
        system.sendNavigationEvents = true;
        RefreshFocus();
    }

    public static bool HasControllerInput()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f ||
                pad.dpad.ReadValue().sqrMagnitude > 0.25f ||
                pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame)
                return true;
        }
        return false;
    }

    public static void SetPanel(Transform root, bool visible, Action close = null)
    {
        if (instance == null || root == null) return;
        int index = instance.panels.FindIndex(panel => panel.root == root);
        if (visible)
        {
            if (index >= 0) return;
            instance.panels.Add(new Panel
            {
                root = root,
                previous = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject,
                close = close
            });
            instance.pendingRestore = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        else if (index >= 0)
        {
            Panel panel = instance.panels[index];
            instance.panels.RemoveAt(index);
            instance.pendingRestore = panel.previous;
        }
        // Run after all visibility changes in the caller (LateUpdate).
    }

    private void Update()
    {
        if (HasControllerInput()) controllerActive = true;
        else if (Mouse.current != null &&
                 (Mouse.current.delta.ReadValue().sqrMagnitude > 1f || Mouse.current.leftButton.wasPressedThisFrame))
            controllerActive = false;

        if (!controllerActive) return;
        RefreshFocus();
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.buttonEast.wasPressedThisFrame && panels.Count > 0)
            {
                GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                if (selected != null && ExecuteEvents.GetEventHandler<ICancelHandler>(selected) != null) break;
                panels[panels.Count - 1].close?.Invoke();
                break;
            }
        }
    }

    private void LateUpdate()
    {
        RefreshFocus();
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (indicator != null) indicator.gameObject.SetActive(false);
        if (!controllerActive || selected == null || !IsAvailable(selected.GetComponent<Selectable>())) return;
        if (lastSelected != selected)
        {
            ScrollIntoView(selected);
            lastSelected = selected;
        }
        if (indicator == null) CreateIndicator();
        indicator.SetParent(selected.transform, false);
        indicator.SetAsLastSibling();
        indicator.anchorMin = Vector2.zero;
        indicator.anchorMax = Vector2.one;
        indicator.offsetMin = new Vector2(-4f, -4f);
        indicator.offsetMax = new Vector2(4f, 4f);
        indicator.gameObject.SetActive(true);
    }

    private void RefreshFocus()
    {
        EventSystem system = EventSystem.current;
        if (system == null) return;
        candidates.Clear();
        Transform root = panels.Count == 0 ? null : panels[panels.Count - 1].root;
        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (selectable.gameObject.scene != gameObject.scene || !IsAvailable(selectable)) continue;
            if (root != null && !selectable.transform.IsChildOf(root)) continue;
            candidates.Add(selectable);
        }

        // Explicit links restrict navigation to the current panel, including dynamically created rows.
        for (int i = 0; i < candidates.Count; i++)
        {
            Selectable item = candidates[i];
            Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = FindNeighbour(item, Vector2.up);
            navigation.selectOnDown = FindNeighbour(item, Vector2.down);
            navigation.selectOnLeft = FindNeighbour(item, Vector2.left);
            navigation.selectOnRight = FindNeighbour(item, Vector2.right);
            item.navigation = navigation;
        }

        if (pendingRestore != null)
        {
            Selectable previous = pendingRestore.GetComponent<Selectable>();
            if (candidates.Contains(previous)) system.SetSelectedGameObject(pendingRestore);
            pendingRestore = null;
        }
        if (!controllerActive) return;
        Selectable current = system.currentSelectedGameObject == null
            ? null : system.currentSelectedGameObject.GetComponent<Selectable>();
        if (!candidates.Contains(current))
            system.SetSelectedGameObject(candidates.Count == 0 ? null : candidates[0].gameObject);
    }

    private Selectable FindNeighbour(Selectable from, Vector2 direction)
    {
        Vector3 origin = ((RectTransform)from.transform).TransformPoint(((RectTransform)from.transform).rect.center);
        Selectable best = null;
        float bestScore = float.NegativeInfinity;
        foreach (Selectable candidate in candidates)
        {
            if (candidate == from) continue;
            RectTransform rect = (RectTransform)candidate.transform;
            Vector2 delta = rect.TransformPoint(rect.rect.center) - origin;
            float forward = Vector2.Dot(delta, direction);
            if (forward <= 0.001f) continue;
            float score = forward / delta.sqrMagnitude;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    private static bool IsAvailable(Selectable selectable)
    {
        if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable()) return false;
        foreach (CanvasGroup group in selectable.GetComponentsInParent<CanvasGroup>())
        {
            if (group.alpha <= 0.001f || !group.interactable) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }

    private void CreateIndicator()
    {
        indicator = new GameObject("Controller Focus", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        indicator.GetComponent<CanvasGroup>().blocksRaycasts = false;
        for (int edge = 0; edge < 4; edge++)
        {
            Image image = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(indicator, false);
            image.color = new Color(1f, 0.85f, 0.15f, 1f);
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            bool horizontal = edge < 2;
            float side = edge % 2;
            rect.anchorMin = horizontal ? new Vector2(0f, side) : new Vector2(side, 0f);
            rect.anchorMax = horizontal ? new Vector2(1f, side) : new Vector2(side, 1f);
            rect.sizeDelta = horizontal ? new Vector2(0f, 3f) : new Vector2(3f, 0f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    private static void ScrollIntoView(GameObject selected)
    {
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || !selected.transform.IsChildOf(scroll.content)) return;
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        Rect rect = viewport.rect;
        Vector2 offset = Vector2.zero;
        if (scroll.vertical)
            offset.y = bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y :
                bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : 0f;
        if (scroll.horizontal)
            offset.x = bounds.min.x < rect.xMin ? rect.xMin - bounds.min.x :
                bounds.max.x > rect.xMax ? rect.xMax - bounds.max.x : 0f;
        scroll.StopMovement();
        scroll.content.anchoredPosition += (Vector2)scroll.content.parent.InverseTransformVector(viewport.TransformVector(offset));
    }

    private void OnDestroy()
    {
        if (indicator != null) Destroy(indicator.gameObject);
        if (instance == this) instance = null;
    }
}
