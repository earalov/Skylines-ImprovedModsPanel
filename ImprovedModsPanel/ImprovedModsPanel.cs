using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace ImprovedModsPanel
{

    public class ImprovedModsPanel : MonoBehaviour
    {

        private enum SortMode
        {
            [Description("Name")]
            Alphabetical = 0,
            [Description("Last updated")]
            LastUpdated = 1,
            [Description("Last subscribed")]
            LastSubscribed = 2,
            [Description("Active")]
            Active = 3
        }

        private enum SortOrder
        {
            [Description("Ascending")]
            Ascending = 0,
            [Description("Descending")]
            Descending = 1
        }

        private static bool _ui_initialized;

        private static RedirectCallsState _revertState;
        private static RedirectCallsState _revertState2;


        private static SortMode _sortMode = SortMode.Alphabetical;
        private static SortOrder _sortOrder = SortOrder.Ascending;
        private static UIPanel _sortPanel;
        private static UIDropDown _sortDropDown;
        private static UILabel _sortLabel;

        public static void Bootstrap()
        {
            var syncObject = GameObject.Find("ImprovedModsPanelSyncObject");
            if (syncObject != null)
            {
                return;
            }
            syncObject = new GameObject("ImprovedModsPanelSyncObject");
            syncObject.AddComponent<UpdateHook>().onUnityDestroy = Revert;
            var updateHook = syncObject.AddComponent<UpdateHook>();
            updateHook.once = false;
            updateHook.onUnityUpdate = () =>
            {
                if (Singleton<LoadingManager>.instance.m_loadedEnvironment == null)
                {
                    if (!_ui_initialized)
                    {
                        var contentManagerPanelGameObject = GameObject.Find("(Library) ContentManagerPanel");
                        if (contentManagerPanelGameObject == null)
                        {
                            return;
                        }
                        var contentManagerPanel = contentManagerPanelGameObject.GetComponent<ContentManagerPanel>();
                        if (contentManagerPanel == null)
                        {
                            return;
                        }
                        var categoryContainerGameObject = GameObject.Find("CategoryContainer");
                        if (categoryContainerGameObject == null)
                        {
                            return;
                        }
                        var categoryContainer = categoryContainerGameObject.GetComponent<UITabContainer>();
                        if (categoryContainer == null)
                        {
                            return;
                        }
                        var mods = categoryContainer.Find("Mods");
                        if (mods == null)
                        {
                            return;;
                        }
                        var modsList = mods.Find("Content");
                        if (modsList == null)
                        {
                            return;
                        }
                        var moarGroupObj = GameObject.Find("MoarGroup");
                        if (moarGroupObj == null)
                        {
                            return;
                        }
                        syncObject.gameObject.AddComponent<UpdateHook>().onUnityUpdate = RefreshPlugins;
                    }
                }
                else
                {
                    Destroy(syncObject);
                }

                updateHook.once = true;

            };
            if (Singleton<LoadingManager>.instance.m_loadedEnvironment != null)
            {
                return;
            }
            _revertState = RedirectionHelper.RedirectCalls
            (
                typeof(PackageEntry).GetMethod("FormatPackageName",
                    BindingFlags.Static | BindingFlags.NonPublic),
                typeof(ImprovedModsPanel).GetMethod("FormatPackageName",
                    BindingFlags.Static | BindingFlags.NonPublic)
            );

            _revertState2 = RedirectionHelper.RedirectCalls
            (
                typeof(ContentManagerPanel).GetMethod("RefreshPlugins",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                typeof(ImprovedModsPanel).GetMethod("RefreshPlugins",
                    BindingFlags.Static | BindingFlags.Public)
            );
        }

        private static void Initialize()
        {
            if (_sortDropDown != null)
            {
                return;
            }

            var moarGroupObj = GameObject.Find("MoarGroup");
            var moarGroup = moarGroupObj.GetComponent<UIPanel>();
            var moarLabel = moarGroup.Find<UILabel>("Moar");
            var moarButton = moarGroup.Find<UIButton>("Button");

            moarGroup.position = new Vector3(moarGroup.position.x, -6.0f, moarGroup.position.z);

            moarLabel.isVisible = false;
            moarButton.isVisible = false;

            var uiView = FindObjectOfType<UIView>();


            _sortPanel = uiView.AddUIComponent(typeof(UIPanel)) as UIPanel;
            _sortPanel.transform.parent = moarGroup.transform;

            _sortDropDown = UIUtils.CreateDropDown(_sortPanel);
            _sortDropDown.size = new Vector2(200.0f, 24.0f);
            _sortDropDown.AlignTo(_sortPanel, UIAlignAnchor.TopLeft);

            var enumValues = Enum.GetValues(typeof(SortMode));
            _sortDropDown.items = new string[enumValues.Length];
            var i = 0;
            foreach (var value in enumValues)
            {
                _sortDropDown.items[i] = ((SortMode)value).GetEnumDescription();
                i++;
            }
            _sortDropDown.selectedIndex = 0;

            _sortDropDown.eventSelectedIndexChanged += (component, value) =>
            {
                _sortDropDown.enabled = false;
                _sortMode = (SortMode)value;
                RefreshPlugins();
                _sortDropDown.enabled = true;
            };

            _sortLabel = InitializeLabel(uiView, _sortPanel, "Sort by");
        }

        public static void Revert()
        {
            _ui_initialized = false;
            if (_revertState != null)
            {
                RedirectionHelper.RevertRedirect(typeof (PackageEntry).GetMethod("FormatPackageName",
                    BindingFlags.Static | BindingFlags.NonPublic), _revertState);
            }
            if (_revertState2 != null)
            {
                RedirectionHelper.RevertRedirect(typeof(ContentManagerPanel).GetMethod("RefreshPlugins",
                        BindingFlags.Instance | BindingFlags.NonPublic), _revertState2);
            }

            if (_sortPanel != null)
            {
                Destroy(_sortPanel.gameObject);
            }
            _sortPanel = null;
            _sortDropDown = null;
            _sortLabel = null;
            _sortMode = SortMode.Alphabetical;
            _sortOrder = SortOrder.Ascending;

            var syncObject = GameObject.Find("ImprovedModsPanelSyncObject");
            if (syncObject != null)
            {
                Destroy(syncObject);
            }
        }

        private static string FormatPackageName(string entryName, string authorName, bool isWorkshopItem)
        {
            return String.Format(entryName, !isWorkshopItem ? authorName : "Unknown");
        }


        private static UILabel InitializeLabel(UIView uiView, UIComponent parent, string labelText)
        {
            var label = uiView.AddUIComponent(typeof(UILabel)) as UILabel;
            label.transform.parent = parent.transform;
            label.text = labelText;
            label.AlignTo(parent, UIAlignAnchor.TopLeft);
            label.textColor = Color.white;
            label.textScale = 0.5f;
            return label;
        }


        private class Plugin
        {
            public string name;
            public string description;
            public TimeSpan lastUpdatedTimeDelta;
            public TimeSpan subscribedTimeDelta;
        }

        public static void RefreshPlugins()
        {
//            if (!_ui_initialized)
//            {
//                Initialize();
//                _ui_initialized = true;
//            }
//
//
//            var categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
//            var modsList = categoryContainer.Find("Mods").Find("Content");
//            var uiView = FindObjectOfType<UIView>();
//            var plugins = new Dictionary<PluginManager.PluginInfo, Plugin>();
//
//            foreach (var current in PluginManager.instance.GetPluginsInfo())
//            {
//                Plugin plugin;
//                try
//                {
//                    var instances = current.GetInstances<IUserMod>();
//                    if (instances.Length == 0)
//                    {
//                        Debug.LogErrorFormat("User assembly \"{0}\" does not implement the IUserMod interface!", current.name);
//                        continue;
//                    }
//                    plugin = new Plugin
//                    {
//                        name = instances[0].Name,
//                        description = instances[0].Description,
//                        lastUpdatedTimeDelta = GetPluginLastModifiedDelta(current),
//                        subscribedTimeDelta = GetPluginCreatedDelta(current)
//                    };
//
//                }
//                catch
//                {
//                    plugin = new Plugin
//                    {
//                        name = current.assembliesString,
//                        description = "Broken assembly!",
//                        lastUpdatedTimeDelta = GetPluginLastModifiedDelta(current),
//                        subscribedTimeDelta = GetPluginCreatedDelta(current)
//                    };
//                    Debug.LogErrorFormat("Exception happened when getting IUserMod instances from assembly \"{0}\"!", current.name);
//                }
//                plugins.Add(current, plugin);
//            }
//
//            var uIComponent = modsList.GetComponent<UIComponent>();
//            UITemplateManager.ClearInstances("ModEntryTemplate");
//
//            Func<PluginManager.PluginInfo, PluginManager.PluginInfo, int> comparerLambda;
//            var alphabeticalSort = false;
//
//            Func<PluginManager.PluginInfo, PluginManager.PluginInfo, int> compareNames =
//                (a, b) => String.Compare(plugins[a].name, plugins[b].name, StringComparison.InvariantCultureIgnoreCase);
//            switch (_sortMode)
//            {
//                case SortMode.Alphabetical:
//                    comparerLambda = compareNames;
//                    alphabeticalSort = true;
//                    break;
//                case SortMode.LastUpdated:
//                    comparerLambda = (a, b) => plugins[a].lastUpdatedTimeDelta.CompareTo(plugins[b].lastUpdatedTimeDelta);
//                    break;
//                case SortMode.LastSubscribed:
//                    comparerLambda = (a, b) => plugins[a].subscribedTimeDelta.CompareTo(plugins[b].subscribedTimeDelta);
//                    break;
//                case SortMode.Active:
//                    comparerLambda = (a, b) => b.isEnabled.CompareTo(a.isEnabled);
//                    break;
//                default:
//                    throw new Exception(String.Format("Unknown sort mode: '{0}'", _sortMode));
//            }
//
//            var pluginsSorted = plugins.Keys.ToArray();
//            Array.Sort(pluginsSorted, new FunctionalComparer<PluginManager.PluginInfo>((a, b) =>
//            {
//                var diff =
//                    (_sortOrder == SortOrder.Ascending
//                        ? comparerLambda
//                        : (arg1, arg2) => -comparerLambda(arg1, arg2))(a, b);
//                return diff != 0 || alphabeticalSort ? diff : compareNames(a, b);
//
//            }));
//
//            var whiteColor = new Color32(200, 200, 200, 255);
//
//            foreach (var current in pluginsSorted)
//            {
//                var packageEntry = UITemplateManager.Get<PackageEntry>("ModEntryTemplate");
//                uIComponent.AttachUIComponent(packageEntry.gameObject);
//
//                packageEntry.entryName = String.Format("{0} (by {{0}})", plugins[current].name);
//                packageEntry.entryActive = current.isEnabled;
//                packageEntry.pluginInfo = current;
//                packageEntry.publishedFileId = current.publishedFileID;
//                packageEntry.RequestDetails();
//
//                var panel = packageEntry.gameObject.GetComponent<UIPanel>();
//                panel.size = new Vector2(panel.size.x, 24.0f);
//
//                var name = (UILabel)panel.Find("Name");
//                name.textScale = 0.85f;
//                name.tooltip = plugins[current].description;
//                name.textColor = whiteColor;
//                name.textScaleMode = UITextScaleMode.ControlSize;
//                name.position = new Vector3(30.0f, 2.0f, name.position.z);
//
//                var view = (UIButton)panel.Find("View");
//                view.size = new Vector2(20.0f, 20.0f);
//                view.textScale = 0.7f;
//                view.position = new Vector3(675.0f, 2.0f, view.position.z);
//
//                var share = (UIButton)panel.Find("Share");
//                share.size = new Vector2(84.0f, 20.0f);
//                share.textScale = 0.7f;
//                share.position = new Vector3(703.0f, 2.0f, share.position.z);
//
//                var options = (UIButton)panel.Find("Options");
//                options.size = new Vector2(84.0f, 20.0f);
//                options.textScale = 0.7f;
//                options.position = new Vector3(790.0f, 2.0f, options.position.z);
//
//
//                var lastUpdated = (UILabel)panel.Find("LastUpdated") ??
//                                  uiView.AddUIComponent(typeof(UILabel)) as UILabel;
//
//                lastUpdated.name = "LastUpdated";
//                lastUpdated.autoSize = false;
//                lastUpdated.size = new Vector2(400.0f, 18.0f);
//                lastUpdated.textScale = 0.8f;
//                lastUpdated.textAlignment = UIHorizontalAlignment.Right;
//                lastUpdated.textColor = whiteColor;
//                lastUpdated.text = String.Format("Last update: {0}",
//                    DateTimeUtil.TimeSpanToString(plugins[current].lastUpdatedTimeDelta));
//                lastUpdated.AlignTo(panel, UIAlignAnchor.TopRight);
//                lastUpdated.relativePosition = new Vector3(264.0f, 6.0f, 0.0f);
//
//                var delete = (UIButton)panel.Find("Delete");
//                delete.size = new Vector2(24.0f, 24.0f);
//                delete.position = new Vector3(895.0f, 2.0f, delete.position.z);
//
//                var active = (UICheckBox)panel.Find("Active");
//                active.position = new Vector3(4.0f, -2.0f, active.position.z);
//
//                var onOff = (UILabel)active.Find("OnOff");
//                onOff.enabled = false;
//            }
        }

        private static TimeSpan GetPluginLastModifiedDelta(PluginManager.PluginInfo plugin)
        {
            var lastModified = DateTime.MinValue;

            foreach (var file in Directory.GetFiles(plugin.modPath))
            {
                if (Path.GetExtension(file) != ".dll")
                {
                    continue;
                }
                var tmp = File.GetLastWriteTime(file);
                if (tmp > lastModified)
                {
                    lastModified = tmp;
                }
            }

            return DateTime.Now - lastModified;
        }

        private static TimeSpan GetPluginCreatedDelta(PluginManager.PluginInfo plugin)
        {
            var created = DateTime.MinValue;

            foreach (var file in Directory.GetFiles(plugin.modPath))
            {
                if (Path.GetExtension(file) != ".dll") continue;
                var tmp = File.GetCreationTime(file);
                if (tmp > created)
                {
                    created = tmp;
                }
            }

            return DateTime.Now - created;
        }


    }

}
