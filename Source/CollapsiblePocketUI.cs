using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TAMPS
{
    internal static class CollapsiblePocketUI
    {
        private const string MarkerName =
            "TAMPS_CollapseMarker";

        private static readonly Dictionary<int, bool>
            CollapsedByWidget =
                new Dictionary<int, bool>();

        private static readonly Dictionary<int, GameObject>
            HiddenByPocketUI =
                new Dictionary<int, GameObject>();

        private static readonly Dictionary<int, string>
            LastLog =
                new Dictionary<int, string>();

        private static bool enabled;
        private static bool collapsedByDefault;

        internal static void Configure(
            Settings settings)
        {
            enabled =
                settings != null &&
                settings.EnableCollapsiblePocketUI;

            collapsedByDefault =
                settings != null &&
                settings.PocketUICollapsedByDefault;
        }

        internal static void RestoreAllTrackedRows(
            string reason)
        {
            List<GameObject> rows =
                new List<GameObject>(
                    HiddenByPocketUI.Values);

            HiddenByPocketUI.Clear();

            int restored =
                0;

            for (int i = 0;
                 i < rows.Count;
                 i++)
            {
                GameObject row =
                    rows[i];

                if (row == null)
                {
                    continue;
                }

                if (!row.activeSelf)
                {
                    row.SetActive(
                        true);
                }

                restored++;
            }

            if (restored > 0)
            {
                PocketRegistry.LogInfo(
                    "Collapsible pocket UI restored " +
                    restored +
                    " tracked ammo row(s) before " +
                    reason +
                    ".");
            }
        }

        internal static void Refresh(
            object widget,
            string reason)
        {
            if (!enabled)
            {
                return;
            }

            Component component =
                widget as Component;

            if (component == null ||
                component.gameObject == null)
            {
                return;
            }

            Apply(
                widget,
                reason + " immediate");

            PocketCollapseRefreshRunner runner =
                component.gameObject.GetComponent<
                    PocketCollapseRefreshRunner>();

            if (runner == null)
            {
                runner =
                    component.gameObject.AddComponent<
                        PocketCollapseRefreshRunner>();
            }

            runner.Schedule(
                widget,
                reason);
        }

        internal static void Toggle(
            object widget,
            string source)
        {
            Component component =
                widget as Component;

            if (!enabled ||
                component == null)
            {
                return;
            }

            int key =
                component.GetInstanceID();

            bool current =
                GetCollapsed(
                    component);

            CollapsedByWidget[key] =
                !current;

            PocketRegistry.LogInfo(
                "Collapsible pocket UI click: " +
                PocketReflection.GetWidgetLocation(
                    widget) +
                " source=" +
                source +
                " -> " +
                (!current
                    ? "collapsed"
                    : "expanded"));

            Apply(
                widget,
                "toggle");
        }

        internal static void Apply(
            object widget,
            string reason)
        {
            if (!enabled)
            {
                return;
            }

            Component widgetComponent =
                widget as Component;

            if (widgetComponent == null)
            {
                return;
            }

            string location =
                PocketReflection.GetWidgetLocation(
                    widget);

            if (!String.Equals(
                    location,
                    "LeftTorso",
                    StringComparison.Ordinal) &&
                !String.Equals(
                    location,
                    "RightTorso",
                    StringComparison.Ordinal))
            {
                return;
            }

            IEnumerable localInventory =
                PocketReflection.GetFieldValue(
                    widget,
                    "localInventory") as IEnumerable;

            if (localInventory == null)
            {
                return;
            }

            GameObject moduleRow =
                null;

            object moduleItem =
                null;

            List<object> containedItems =
                new List<object>();

            List<GameObject> containedRows =
                new List<GameObject>();

            List<GameObject> allRows =
                new List<GameObject>();

            foreach (object item in localInventory)
            {
                Component itemComponent =
                    item as Component;

                if (itemComponent == null ||
                    itemComponent.gameObject == null)
                {
                    continue;
                }

                GameObject row =
                    itemComponent.gameObject;

                allRows.Add(
                    row);

                object componentRef =
                    PocketReflection.GetMemberValue(
                        item,
                        "ComponentRef");

                if (componentRef == null)
                {
                    componentRef =
                        PocketReflection.GetMemberValue(
                            item,
                            "componentRef");
                }

                string id =
                    PocketReflection.GetComponentId(
                        componentRef);

                bool isModule =
                    String.Equals(
                        id,
                        PocketRuntime.GearId,
                        StringComparison.OrdinalIgnoreCase);

                SetMarkerActive(
                    row,
                    isModule);

                if (isModule)
                {
                    moduleRow =
                        row;

                    moduleItem =
                        item;
                }

                if (PocketRegistry.IsContained(
                        componentRef))
                {
                    containedItems.Add(
                        item);

                    containedRows.Add(
                        row);
                }
                else
                {
                    RestoreIfHidden(
                        row);
                }
            }

            if (moduleRow == null)
            {
                for (int i = 0;
                     i < allRows.Count;
                     i++)
                {
                    RestoreIfHidden(
                        allRows[i]);
                }

                Log(
                    widgetComponent,
                    location +
                    " [" +
                    reason +
                    "]: no module row; restored hidden rows.");
                return;
            }

            SeatPocketBlockInInventory(
                localInventory as IList,
                moduleItem,
                containedItems,
                location,
                reason);

            NormalizeModuleRowVisualState(
                moduleRow,
                location,
                reason);

            NormalizePocketRowLayout(
                moduleRow,
                location,
                reason);

            bool collapsed =
                GetCollapsed(
                    widgetComponent);

            for (int i = 0;
                 i < containedRows.Count;
                 i++)
            {
                SetPocketRowVisible(
                    containedRows[i],
                    !collapsed);
            }

            PocketCollapseMarker marker =
                EnsureMarker(
                    moduleRow,
                    moduleItem,
                    widget);

            if (marker != null)
            {
                marker.SetState(
                    collapsed,
                    containedRows.Count);
            }

            NormalizePocketRowLayout(
                moduleRow,
                location,
                reason + " post-visibility");

            NormalizeModuleRowVisualState(
                moduleRow,
                location,
                reason + " post-visibility");

            RefreshLayout(
                moduleRow);

            Log(
                widgetComponent,
                location +
                " [" +
                reason +
                "]: " +
                (collapsed
                    ? "collapsed"
                    : "expanded") +
                ", containedRows=" +
                containedRows.Count +
                ".");
        }

        private static bool GetCollapsed(
            Component widget)
        {
            bool value;

            if (CollapsedByWidget.TryGetValue(
                    widget.GetInstanceID(),
                    out value))
            {
                return value;
            }

            value =
                collapsedByDefault;

            CollapsedByWidget[
                widget.GetInstanceID()] =
                    value;

            return value;
        }

        private static void SeatPocketBlockInInventory(
            IList localInventory,
            object moduleItem,
            List<object> containedItems,
            string location,
            string reason)
        {
            if (localInventory == null ||
                localInventory.IsReadOnly ||
                localInventory.IsFixedSize ||
                moduleItem == null)
            {
                return;
            }

            int containedCount =
                containedItems == null
                    ? 0
                    : containedItems.Count;

            int blockStart =
                localInventory.Count -
                containedCount -
                1;

            bool alreadySeated =
                blockStart >= 0 &&
                System.Object.ReferenceEquals(
                    localInventory[blockStart],
                    moduleItem);

            if (alreadySeated)
            {
                for (int i = 0;
                     i < containedCount;
                     i++)
                {
                    if (!System.Object.ReferenceEquals(
                            localInventory[
                                blockStart +
                                1 +
                                i],
                            containedItems[i]))
                    {
                        alreadySeated =
                            false;

                        break;
                    }
                }
            }

            if (alreadySeated)
            {
                return;
            }

            try
            {
                // One atomic ordering decision:
                // normal equipment -> module -> its contained AmmoBoxes.
                //
                // Do not alter Transform sibling indices here. BTA/DynamicSlots
                // owns the visual hierarchy and rebuilds it from localInventory.
                for (int i = 0;
                     i < containedCount;
                     i++)
                {
                    object containedItem =
                        containedItems[i];

                    int currentIndex =
                        localInventory.IndexOf(
                            containedItem);

                    if (currentIndex >= 0)
                    {
                        localInventory.RemoveAt(
                            currentIndex);
                    }
                }

                int moduleIndex =
                    localInventory.IndexOf(
                        moduleItem);

                if (moduleIndex >= 0)
                {
                    localInventory.RemoveAt(
                        moduleIndex);
                }

                localInventory.Add(
                    moduleItem);

                for (int i = 0;
                     i < containedCount;
                     i++)
                {
                    localInventory.Add(
                        containedItems[i]);
                }

                PocketRegistry.LogInfo(
                    "Unified pocket inventory order [" +
                    reason +
                    "] " +
                    location +
                    ": module+contained block seated, containedRows=" +
                    containedCount +
                    ". Native DynamicSlots owns sibling ordering.");
            }
            catch (Exception exception)
            {
                PocketRegistry.LogInfo(
                    "Unified pocket inventory ordering failed: " +
                    exception.Message);
            }
        }

        private static void NormalizeModuleRowVisualState(
            GameObject moduleRow,
            string location,
            string reason)
        {
            if (moduleRow == null)
            {
                return;
            }

            int changed =
                0;

            CanvasGroup[] groups =
                moduleRow.GetComponentsInChildren<
                    CanvasGroup>(
                    true);

            for (int i = 0;
                 i < groups.Length;
                 i++)
            {
                CanvasGroup group =
                    groups[i];

                if (group != null &&
                    group.alpha < 0.99f)
                {
                    group.alpha =
                        1f;

                    changed++;
                }
            }

            Text[] texts =
                moduleRow.GetComponentsInChildren<
                    Text>(
                    true);

            for (int i = 0;
                 i < texts.Length;
                 i++)
            {
                Text text =
                    texts[i];

                if (text == null)
                {
                    continue;
                }

                Color color =
                    text.color;

                if (color.a < 0.99f)
                {
                    color.a =
                        1f;

                    text.color =
                        color;

                    changed++;
                }
            }

            if (changed > 0)
            {
                PocketRegistry.LogInfo(
                    "Side module pooled visual reset [" +
                    reason +
                    "] " +
                    location +
                    ": changed=" +
                    changed +
                    ".");
            }
        }

        private static void NormalizePocketRowLayout(
            GameObject moduleRow,
            string location,
            string reason)
        {
            if (moduleRow == null)
            {
                return;
            }

            RectTransform moduleRect =
                moduleRow.transform as
                    RectTransform;

            RectTransform parentRect =
                moduleRow.transform.parent as
                    RectTransform;

            if (moduleRect == null ||
                parentRect == null)
            {
                return;
            }

            float moduleHeight =
                MeasureModuleVisualHeight(
                    moduleRect);

            Vector2 moduleSize =
                moduleRect.sizeDelta;

            if (Math.Abs(
                    moduleSize.y -
                    moduleHeight) >
                0.1f)
            {
                moduleSize.y =
                    moduleHeight;

                moduleRect.sizeDelta =
                    moduleSize;
            }

            List<RectTransform> rows =
                new List<RectTransform>();

            for (int i = 0;
                 i < parentRect.childCount;
                 i++)
            {
                Transform child =
                    parentRect.GetChild(
                        i);

                RectTransform rect =
                    child as RectTransform;

                if (rect == null ||
                    !child.gameObject.activeSelf)
                {
                    continue;
                }

                // Only the direct MechLab component rows use the same prefab name.
                // This avoids touching unrelated decoration or background children.
                if (!child.name.StartsWith(
                        "uixPrfPanl_ML_component-Element",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rows.Add(
                    rect);
            }

            if (rows.Count == 0)
            {
                return;
            }

            float topY =
                rows[0].anchoredPosition.y;

            float canonicalX =
                rows[0].anchoredPosition.x;

            for (int i = 1;
                 i < rows.Count;
                 i++)
            {
                topY =
                    Math.Max(
                        topY,
                        rows[i].anchoredPosition.y);

                if (Math.Abs(
                        canonicalX) >
                    100f &&
                    Math.Abs(
                        rows[i].anchoredPosition.x) <=
                    100f)
                {
                    canonicalX =
                        rows[i].anchoredPosition.x;
                }
            }

            float cursorY =
                topY;

            int changed =
                0;

            for (int i = 0;
                 i < rows.Count;
                 i++)
            {
                RectTransform row =
                    rows[i];

                float height =
                    Math.Abs(
                        row.sizeDelta.y);

                if (row == moduleRect)
                {
                    height =
                        moduleHeight;
                }

                if (height < 1f)
                {
                    height =
                        32f;
                }

                Vector2 anchored =
                    row.anchoredPosition;

                if (Math.Abs(
                        anchored.x -
                        canonicalX) >
                    0.1f ||
                    Math.Abs(
                        anchored.y -
                        cursorY) >
                    0.1f)
                {
                    anchored.x =
                        canonicalX;

                    anchored.y =
                        cursorY;

                    row.anchoredPosition =
                        anchored;

                    changed++;
                }

                cursorY -=
                    height;
            }

            if (changed > 0)
            {
                PocketRegistry.LogInfo(
                    "Pocket manual reflow [" +
                    reason +
                    "] " +
                    location +
                    ": rows=" +
                    rows.Count +
                    ", moduleHeight=" +
                    moduleHeight +
                    ", changed=" +
                    changed +
                    ".");
            }
        }

        private static float MeasureModuleVisualHeight(
            RectTransform moduleRect)
        {
            // The module is a single visible 32 px header.
            //
            // DynamicSlots may expose active descendants below that header.
            // Measuring those descendants produced a false 64 px outer row
            // and therefore the persistent empty static slot.
            //
            // The two contained AmmoBoxes are independent sibling rows, so
            // they must never be included in the module's outer height.
            return 32f;
        }

        private static bool IsInsideCollapseMarker(
            Transform transform)
        {
            Transform current =
                transform;

            while (current != null)
            {
                if (String.Equals(
                        current.name,
                        MarkerName,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }

        private static void SetPocketRowVisible(
            GameObject row,
            bool visible)
        {
            if (row == null)
            {
                return;
            }

            int id =
                row.GetInstanceID();

            if (visible)
            {
                HiddenByPocketUI.Remove(
                    id);

                if (!row.activeSelf)
                {
                    row.SetActive(
                        true);
                }

                return;
            }

            HiddenByPocketUI[id] =
                row;

            if (row.activeSelf)
            {
                row.SetActive(
                    false);
            }
        }

        private static void RestoreIfHidden(
            GameObject row)
        {
            if (row == null)
            {
                return;
            }

            int id =
                row.GetInstanceID();

            if (!HiddenByPocketUI.Remove(
                    id))
            {
                return;
            }

            if (!row.activeSelf)
            {
                row.SetActive(
                    true);
            }
        }

        private static void SetMarkerActive(
            GameObject row,
            bool active)
        {
            if (row == null)
            {
                return;
            }

            Transform marker =
                row.transform.Find(
                    MarkerName);

            if (marker != null &&
                marker.gameObject.activeSelf !=
                    active)
            {
                marker.gameObject.SetActive(
                    active);
            }
        }

        private static PocketCollapseMarker EnsureMarker(
            GameObject moduleRow,
            object moduleSlotElement,
            object widget)
        {
            if (moduleRow == null)
            {
                return null;
            }

            Transform existing =
                moduleRow.transform.Find(
                    MarkerName);

            GameObject markerObject =
                existing == null
                    ? null
                    : existing.gameObject;

            if (markerObject == null)
            {
                markerObject =
                    new GameObject(
                        MarkerName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(CanvasGroup),
                        typeof(Button),
                        typeof(PocketCollapseMarker));

                markerObject.transform.SetParent(
                    moduleRow.transform,
                    false);

                markerObject.transform.SetAsLastSibling();

                RectTransform rect =
                    markerObject.GetComponent<
                        RectTransform>();

                rect.anchorMin =
                    new Vector2(
                        1f,
                        0.5f);

                rect.anchorMax =
                    new Vector2(
                        1f,
                        0.5f);

                rect.pivot =
                    new Vector2(
                        1f,
                        0.5f);

                rect.sizeDelta =
                    new Vector2(
                        28f,
                        26f);

                rect.anchoredPosition =
                    new Vector2(
                        -2f,
                        0f);

                Image background =
                    markerObject.GetComponent<
                        Image>();

                background.color =
                    new Color(
                        0.05f,
                        0.05f,
                        0.05f,
                        0.88f);

                background.raycastTarget =
                    true;

                CanvasGroup markerCanvasGroup =
                    markerObject.GetComponent<
                        CanvasGroup>();

                markerCanvasGroup.alpha =
                    1f;

                markerCanvasGroup.interactable =
                    true;

                markerCanvasGroup.blocksRaycasts =
                    true;

                markerCanvasGroup.ignoreParentGroups =
                    true;

                Button button =
                    markerObject.GetComponent<
                        Button>();

                button.transition =
                    Selectable.Transition.None;

                button.targetGraphic =
                    background;

                CreateLine(
                    markerObject.transform,
                    "Horizontal",
                    new Vector2(
                        14f,
                        3f));

                CreateLine(
                    markerObject.transform,
                    "Vertical",
                    new Vector2(
                        3f,
                        14f));

                CreateMarkerStatusCell(
                    markerObject.transform,
                    "AmmoCell1",
                    new Vector2(
                        -5f,
                        -9f));

                CreateMarkerStatusCell(
                    markerObject.transform,
                    "AmmoCell2",
                    new Vector2(
                        5f,
                        -9f));
            }

            markerObject.SetActive(
                true);

            PocketCollapseMarker marker =
                markerObject.GetComponent<
                    PocketCollapseMarker>();

            if (marker != null)
            {
                marker.Initialize(
                    moduleSlotElement,
                    widget);
            }

            return marker;
        }

        private static void CreateMarkerStatusCell(
            Transform parent,
            string name,
            Vector2 position)
        {
            GameObject cellObject =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            cellObject.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                cellObject.GetComponent<
                    RectTransform>();

            rect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.sizeDelta =
                new Vector2(
                    6f,
                    4f);

            rect.anchoredPosition =
                position;

            Image image =
                cellObject.GetComponent<
                    Image>();

            image.color =
                new Color(
                    0.24f,
                    0.24f,
                    0.24f,
                    0.92f);

            image.raycastTarget =
                false;
        }

        private static void CreateLine(
            Transform parent,
            string name,
            Vector2 size)
        {
            GameObject line =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            line.transform.SetParent(
                parent,
                false);

            RectTransform rect =
                line.GetComponent<
                    RectTransform>();

            rect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            rect.sizeDelta =
                size;

            rect.anchoredPosition =
                Vector2.zero;

            Image image =
                line.GetComponent<
                    Image>();

            image.color =
                new Color(
                    0.95f,
                    0.95f,
                    0.95f,
                    1f);

            image.raycastTarget =
                false;
        }

        private static void RefreshLayout(
            GameObject moduleRow)
        {
            // Intentionally empty.
            //
            // BTA manages the MechLab row positions itself. Calling
            // LayoutRebuilder.ForceRebuildLayoutImmediate or adding a
            // LayoutElement to the zero-slot module row can move it to the
            // top of the location list and place it behind other equipment.
            // SetActive changes are allowed to settle through BTA's normal
            // refresh cycle instead.
        }

        private static void Log(
            Component widget,
            string message)
        {
            int id =
                widget.GetInstanceID();

            string previous;

            if (LastLog.TryGetValue(
                    id,
                    out previous) &&
                String.Equals(
                    previous,
                    message,
                    StringComparison.Ordinal))
            {
                return;
            }

            LastLog[id] =
                message;

            PocketRegistry.LogInfo(
                "Collapsible pocket UI: " +
                message);
        }
    }

    internal sealed class PocketCollapseMarker :
        MonoBehaviour
    {
        private object slotElement;
        private object widget;
        private Button button;
        private Image background;
        private GameObject verticalLine;
        private Image ammoCell1;
        private Image ammoCell2;
        private bool listenerAttached;
        private bool destroying;

        internal void Initialize(
            object targetSlotElement,
            object targetWidget)
        {
            slotElement =
                targetSlotElement;

            widget =
                targetWidget;

            if (button == null)
            {
                button =
                    GetComponent<
                        Button>();
            }

            if (background == null)
            {
                background =
                    GetComponent<
                        Image>();
            }

            Transform vertical =
                transform.Find(
                    "Vertical");

            verticalLine =
                vertical == null
                    ? null
                    : vertical.gameObject;

            Transform firstCell =
                transform.Find(
                    "AmmoCell1");

            Transform secondCell =
                transform.Find(
                    "AmmoCell2");

            ammoCell1 =
                firstCell == null
                    ? null
                    : firstCell.GetComponent<
                        Image>();

            ammoCell2 =
                secondCell == null
                    ? null
                    : secondCell.GetComponent<
                        Image>();

            if (button != null &&
                !listenerAttached)
            {
                button.onClick.AddListener(
                    HandleClick);

                listenerAttached =
                    true;

                PocketRegistry.LogInfo(
                    "Collapsible pocket UI: marker Button listener attached.");
            }

            ValidateOwner();
        }

        private void LateUpdate()
        {
            ValidateOwner();
        }

        private void ValidateOwner()
        {
            if (destroying)
            {
                return;
            }

            if (slotElement == null ||
                !IsStillPocketModule())
            {
                destroying =
                    true;

                PocketRegistry.LogInfo(
                    "Collapsible pocket UI: removing marker from a pooled non-module row.");

                Destroy(
                    gameObject);
            }
        }

        private bool IsStillPocketModule()
        {
            object componentRef =
                PocketReflection.GetMemberValue(
                    slotElement,
                    "ComponentRef");

            if (componentRef == null)
            {
                componentRef =
                    PocketReflection.GetMemberValue(
                        slotElement,
                        "componentRef");
            }

            return String.Equals(
                PocketReflection.GetComponentId(
                    componentRef),
                PocketRuntime.GearId,
                StringComparison.OrdinalIgnoreCase);
        }

        private void HandleClick()
        {
            if (!IsStillPocketModule())
            {
                ValidateOwner();
                return;
            }

            PocketRegistry.LogInfo(
                "Collapsible pocket UI click: marker Button.onClick");

            CollapsiblePocketUI.Toggle(
                widget,
                "marker Button.onClick");
        }

        public void OnPointerEnter()
        {
            if (background != null)
            {
                background.color =
                    new Color(
                        0.18f,
                        0.18f,
                        0.18f,
                        0.96f);
            }
        }

        public void OnPointerExit()
        {
            if (background != null)
            {
                background.color =
                    new Color(
                        0.05f,
                        0.05f,
                        0.05f,
                        0.88f);
            }
        }

        private void OnDestroy()
        {
            if (button != null &&
                listenerAttached)
            {
                button.onClick.RemoveListener(
                    HandleClick);
            }
        }

        internal void SetState(
            bool collapsed,
            int loadedCount)
        {
            if (verticalLine == null)
            {
                Transform vertical =
                    transform.Find(
                        "Vertical");

                verticalLine =
                    vertical == null
                        ? null
                        : vertical.gameObject;
            }

            if (verticalLine != null)
            {
                verticalLine.SetActive(
                    collapsed);
            }

            if (ammoCell1 == null)
            {
                Transform firstCell =
                    transform.Find(
                        "AmmoCell1");

                ammoCell1 =
                    firstCell == null
                        ? null
                        : firstCell.GetComponent<
                            Image>();
            }

            if (ammoCell2 == null)
            {
                Transform secondCell =
                    transform.Find(
                        "AmmoCell2");

                ammoCell2 =
                    secondCell == null
                        ? null
                        : secondCell.GetComponent<
                            Image>();
            }

            SetAmmoCellColor(
                ammoCell1,
                loadedCount >= 1);

            SetAmmoCellColor(
                ammoCell2,
                loadedCount >= 2);
        }

        private static void SetAmmoCellColor(
            Image cell,
            bool loaded)
        {
            if (cell == null)
            {
                return;
            }

            cell.color =
                loaded
                    ? new Color(
                        0.98f,
                        0.58f,
                        0.10f,
                        1f)
                    : new Color(
                        0.24f,
                        0.24f,
                        0.24f,
                        0.92f);
        }
    }

    internal sealed class PocketCollapseRefreshRunner :
        MonoBehaviour
    {
        private object widget;
        private string reason =
            "";
        private int generation;

        internal void Schedule(
            object targetWidget,
            string targetReason)
        {
            widget =
                targetWidget;

            reason =
                targetReason ??
                "";

            generation++;

            StartCoroutine(
                Run(
                    generation));
        }

        private IEnumerator Run(
            int scheduledGeneration)
        {
            yield return null;

            if (scheduledGeneration !=
                generation)
            {
                yield break;
            }

            CollapsiblePocketUI.Apply(
                widget,
                reason + " next-frame");

            yield return new WaitForSecondsRealtime(
                0.20f);

            if (scheduledGeneration !=
                generation)
            {
                yield break;
            }

            CollapsiblePocketUI.Apply(
                widget,
                reason + " delayed");
        }
    }
}
