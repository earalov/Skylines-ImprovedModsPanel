using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using ColossalFramework.Plugins;
using ColossalFramework.Steamworks;
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

        private static bool _bootstrapped;


        private static bool _detoured = false;
        private static RedirectCallsState _revertState;
        private static RedirectCallsState _revertState2;


        private static SortMode _sortMode = SortMode.Alphabetical;
        private static SortOrder _sortOrder = SortOrder.Ascending;

        private static GameObject _thisGameObject;

        private static UIPanel _sortPanel;
        private static UIDropDown _sortDropDown;
        private static UILabel _sortLabel;

        public static void Bootstrap()
        {
            if (_bootstrapped)
            {
                return;
            }
            try
            {
                if (_thisGameObject == null)
                {
                    _thisGameObject = new GameObject { name = "ImprovedModsPanel" };
                    _thisGameObject.AddComponent<ImprovedModsPanel>();
                }

                if (!_detoured)
                {
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
                    _detoured = true;
                }


                InitializeModSortDropDown();

                var contentManagerPanelObj = GameObject.Find("(Library) ContentManagerPanel");
                if (contentManagerPanelObj == null)
                {
                    return;
                }
                contentManagerPanelObj.AddComponent<UpdateHook>().onUnityUpdate = RefreshPlugins;
                _bootstrapped = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void InitializeModSortDropDown()
        {
            if (_sortDropDown != null)
            {
                return;
            }

            var moarGroupObj = GameObject.Find("MoarGroup");
            if (moarGroupObj == null)
            {
                return;
            }
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
            if (_thisGameObject != null)
            {
                Destroy(_thisGameObject);
                _thisGameObject = null;
            }

            if (_sortPanel != null)
            {
                Destroy(_sortPanel.gameObject);
            }
            _sortPanel = null;
            _sortDropDown = null;
            _sortLabel = null;

            if (!_bootstrapped)
            {
                return;
            }

            if (_detoured)
            {
                _sortOrder = SortOrder.Ascending;
                RedirectionHelper.RevertRedirect(typeof(PackageEntry).GetMethod("FormatPackageName",
                        BindingFlags.Static | BindingFlags.NonPublic), _revertState);

                RedirectionHelper.RevertRedirect(typeof(ContentManagerPanel).GetMethod("RefreshPlugins",
                    BindingFlags.Instance | BindingFlags.NonPublic), _revertState2);
                _detoured = false;
            }

            _bootstrapped = false;
        }

        private static bool _refreshModContents;

        void OnDestroy()
        {
            Revert();
        }

        void Update()
        {
            if (!_refreshModContents || !_bootstrapped)
            {
                return;
            }

            var categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
            var modsList = categoryContainer.Find("Mods").Find("Content");
            if (modsList == null)
            {
                return;
            }

            for (var i = 0; i < modsList.transform.childCount; i++)
            {
                var child = modsList.transform.GetChild(i).GetComponent<UIPanel>();
                var shareButton = child.Find<UIButton>("Share");
                var packageEntry = child.GetComponent<PackageEntry>();

                if (packageEntry.publishedFileId == PublishedFileId.invalid)
                {
                    shareButton.isVisible = true;
                    continue;
                }

                var workshopDetails = Util.GetPrivate<UGCDetails>(packageEntry, "m_WorkshopDetails");
                if ((Steam.steamID == workshopDetails.creatorID))
                {
                    shareButton.isVisible = true;
                }
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


        public static void RefreshPlugins()
        {
            var categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
            var modsList = categoryContainer.Find("Mods").Find("Content");
            if (modsList == null)
            {
                return;
            }

            var uiView = FindObjectOfType<UIView>();

            var plugins = PluginManager.instance.GetPluginsInfo();

            var pluginNames = new Dictionary<PluginManager.PluginInfo, string>();
            var pluginDescriptions = new Dictionary<PluginManager.PluginInfo, string>();
            var pluginLastUpdatedTimeDelta = new Dictionary<PluginManager.PluginInfo, TimeSpan>();
            var pluginSubscribedTimeDelta = new Dictionary<PluginManager.PluginInfo, TimeSpan>();

            foreach (var current in plugins)
            {
                var instances = current.GetInstances<IUserMod>();
                if (instances.Length == 0)
                {
                    Debug.LogErrorFormat("User assembly \"{0}\" does not implement the IUserMod interface!");
                    continue;
                }

                pluginNames.Add(current, instances[0].Name);
                pluginDescriptions.Add(current, instances[0].Description);
                pluginLastUpdatedTimeDelta.Add(current, GetPluginLastModifiedDelta(current));
                pluginSubscribedTimeDelta.Add(current, GetPluginCreatedDelta(current));
            }

            var uIComponent = modsList.GetComponent<UIComponent>();
            UITemplateManager.ClearInstances("ModEntryTemplate");

            var pluginsSorted = PluginManager.instance.GetPluginsInfo().ToArray();

            Func<PluginManager.PluginInfo, PluginManager.PluginInfo, int> comparerLambda;
            var alphabeticalSort = false;

            Func<PluginManager.PluginInfo, PluginManager.PluginInfo, int> compareNames =
                (a, b) => String.Compare(pluginNames[a], pluginNames[b], StringComparison.InvariantCultureIgnoreCase);
            switch (_sortMode)
            {
                case SortMode.Alphabetical:
                    comparerLambda = compareNames;
                    alphabeticalSort = true;
                    break;
                case SortMode.LastUpdated:
                    comparerLambda = (a, b) => pluginLastUpdatedTimeDelta[a].CompareTo(pluginLastUpdatedTimeDelta[b]);
                    break;
                case SortMode.LastSubscribed:
                    comparerLambda = (a, b) => pluginSubscribedTimeDelta[a].CompareTo(pluginSubscribedTimeDelta[b]);
                    break;
                case SortMode.Active:
                    comparerLambda = (a, b) => b.isEnabled.CompareTo(a.isEnabled);
                    break;
                default:
                    throw new Exception(String.Format("Unknown sort mode: '{0}'", _sortMode));
            }

            Array.Sort(pluginsSorted, new FunctionalComparer<PluginManager.PluginInfo>((a, b) =>
            {
                var diff = (_sortOrder == SortOrder.Ascending ? comparerLambda : (arg1, arg2) => -comparerLambda(arg1, arg2))(a, b);
                return diff != 0 || alphabeticalSort ? diff : compareNames(a, b);

            }));

            var whiteColor = new Color32(200, 200, 200, 255);

            foreach (var current in pluginsSorted)
            {
                var packageEntry = UITemplateManager.Get<PackageEntry>("ModEntryTemplate");
                uIComponent.AttachUIComponent(packageEntry.gameObject);

                packageEntry.entryName = String.Format("{0} (by {{0}})", pluginNames[current]);
                packageEntry.entryActive = current.isEnabled;
                packageEntry.pluginInfo = current;
                packageEntry.publishedFileId = current.publishedFileID;
                packageEntry.RequestDetails();

                var panel = packageEntry.gameObject.GetComponent<UIPanel>();
                panel.size = new Vector2(panel.size.x, 24.0f);

                var name = (UILabel)panel.Find("Name");
                name.textScale = 0.85f;
                name.tooltip = pluginDescriptions[current];
                name.textColor = whiteColor;
                name.textScaleMode = UITextScaleMode.ControlSize;
                name.position = new Vector3(30.0f, 2.0f, name.position.z);

                var view = (UIButton)panel.Find("View");
                view.size = new Vector2(20.0f, 20.0f);
                view.textScale = 0.7f;
                view.position = new Vector3(675.0f, 2.0f, view.position.z);

                var share = (UIButton)panel.Find("Share");
                share.size = new Vector2(84.0f, 20.0f);
                share.textScale = 0.7f;
                share.position = new Vector3(703.0f, 2.0f, share.position.z);

                var options = (UIButton)panel.Find("Options");
                options.size = new Vector2(84.0f, 20.0f);
                options.textScale = 0.7f;
                options.position = new Vector3(790.0f, 2.0f, options.position.z);


                var lastUpdated = (UILabel)panel.Find("LastUpdated") ??
                                  uiView.AddUIComponent(typeof(UILabel)) as UILabel;

                lastUpdated.name = "LastUpdated";
                lastUpdated.autoSize = false;
                lastUpdated.size = new Vector2(400.0f, 18.0f);
                lastUpdated.textScale = 0.8f;
                lastUpdated.textAlignment = UIHorizontalAlignment.Right;
                lastUpdated.textColor = whiteColor;
                lastUpdated.text = String.Format("Last update: {0}",
                    DateTimeUtil.TimeSpanToString(pluginLastUpdatedTimeDelta[current]));
                lastUpdated.AlignTo(panel, UIAlignAnchor.TopRight);
                lastUpdated.relativePosition = new Vector3(264.0f, 6.0f, 0.0f);

                var delete = (UIButton)panel.Find("Delete");
                delete.size = new Vector2(24.0f, 24.0f);
                delete.position = new Vector3(895.0f, 2.0f, delete.position.z);

                var active = (UICheckBox)panel.Find("Active");
                active.position = new Vector3(4.0f, -2.0f, active.position.z);

                var onOff = (UILabel)active.Find("OnOff");
                onOff.enabled = false;
            }

            _refreshModContents = true;
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
