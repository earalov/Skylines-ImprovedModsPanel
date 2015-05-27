﻿using System;
using System.Collections.Generic;
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
            Alphabetical = 0,
            LastUpdated = 1,
            LastSubscribed = 2
        }

        private static bool bootstrapped = false;

        private static RedirectCallsState revertState;
        private static RedirectCallsState revertState2;

        private static UIPanel sortDropDown;

        private static SortMode sortMode = SortMode.Alphabetical;

        private static GameObject thisGameObject;

        public static void Bootstrap()
        {
            try
            {
                if (thisGameObject == null)
                {
                    thisGameObject = new GameObject();
                    thisGameObject.name = "ImprovedModsPanel [Fixed For v1.1]";
                    thisGameObject.AddComponent<ImprovedModsPanel>();
                }

                InitializeModSortDropDown();

                if (bootstrapped)
                {
                    return;
                }

                var ContentManagerPanel = GameObject.Find("(Library) ContentManagerPanel").GetComponent<ContentManagerPanel>();
                ContentManagerPanel.gameObject.AddComponent<UpdateHook>().onUnityUpdate = () =>
                {
                    RefreshPlugins();
                };

                revertState = RedirectionHelper.RedirectCalls
                (
                    typeof(PackageEntry).GetMethod("FormatPackageName",
                        BindingFlags.Static | BindingFlags.NonPublic),
                    typeof(ImprovedModsPanel).GetMethod("FormatPackageName",
                        BindingFlags.Static | BindingFlags.NonPublic)
                );

                revertState2 = RedirectionHelper.RedirectCalls
                (
                    typeof(ContentManagerPanel).GetMethod("RefreshPlugins",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    typeof(ImprovedModsPanel).GetMethod("RefreshPlugins",
                        BindingFlags.Static | BindingFlags.Public)
                );

                bootstrapped = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void InitializeModSortDropDown()
        {
            if (GameObject.Find("ModsSortBy") != null)
            {
                return;
            }

            var shadows = GameObject.Find("Shadows").GetComponent<UIPanel>();

            if (shadows == null)
            {
                return;
            }

            var moarGroup = GameObject.Find("MoarGroup").GetComponent<UIPanel>();

            if (moarGroup == null)
            {
                return;
            }

            var moarLabel = moarGroup.Find<UILabel>("Moar");
            var moarButton = moarGroup.Find<UIButton>("Button");

            moarGroup.position = new Vector3(moarGroup.position.x, -6.0f, moarGroup.position.z);

            moarLabel.isVisible = false;
            moarButton.isVisible = false;

            sortDropDown = GameObject.Instantiate(shadows);
            sortDropDown.gameObject.name = "ModsSortBy";
            sortDropDown.transform.parent = moarGroup.transform;
            sortDropDown.name = "ModsSortBy";
            sortDropDown.Find<UILabel>("Label").isVisible = false;

            var dropdown = sortDropDown.Find<UIDropDown>("ShadowsQuality");
            dropdown.name = "SortByDropDown";
            dropdown.size = new Vector2(200.0f, 24.0f);
            dropdown.textScale = 0.8f;

            dropdown.eventSelectedIndexChanged += (component, value) =>
            {
                sortMode = (SortMode)value;
                RefreshPlugins();
            };

            var sprite = dropdown.Find<UIButton>("Sprite");
            sprite.foregroundSpriteMode = UIForegroundSpriteMode.Scale;

            var enumValues = Enum.GetValues(typeof(SortMode));
            dropdown.items = new string[enumValues.Length];

            int i = 0;
            foreach (var value in enumValues)
            {
                dropdown.items[i] = String.Format("Sort by: {0}", EnumToString((SortMode)value));
                i++;
            }
        }

        private static string EnumToString(SortMode mode)
        {
            switch (mode)
            {
                case SortMode.Alphabetical:
                    return "Name";
                case SortMode.LastSubscribed:
                    return "Last subscribed";
                case SortMode.LastUpdated:
                    return "Last updated";
            }

            return "Unknown";
        }

        public static void Revert()
        {
            if (thisGameObject != null)
            {
                Destroy(thisGameObject);
                thisGameObject = null;
            }

            if (sortDropDown != null)
            {
                Destroy(sortDropDown);
                sortDropDown = null;
            }

            if (!bootstrapped)
            {
                return;
            }

            UITabContainer categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
            var modsList = categoryContainer.Find("Mods").Find("Content");
            if (modsList == null)
            {
                return;
            }

            RedirectionHelper.RevertRedirect(typeof(PackageEntry).GetMethod("FormatPackageName",
                    BindingFlags.Static | BindingFlags.NonPublic), revertState);

            RedirectionHelper.RevertRedirect(typeof(ContentManagerPanel).GetMethod("RefreshPlugins",
                BindingFlags.Instance | BindingFlags.NonPublic), revertState2);

            bootstrapped = false;
        }

        private static bool refreshModContents = false;

        void OnDestroy()
        {
            Revert();
        }

        void Update()
        {
            if (!refreshModContents)
            {
                return;
            }

            UITabContainer categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
            var modsList = categoryContainer.Find("Mods").Find("Content");
            if (modsList == null)
            {
                return;
            }

            for (int i = 0; i < modsList.transform.childCount; i++)
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
            if (!isWorkshopItem)
            {
                return String.Format(entryName, authorName);
            }
            else
            {
                return String.Format(entryName, "Unknown");
            }
        }

        private static Color32 blackColor = new Color32(0, 0, 0, 255);
        private static Color32 whiteColor = new Color32(200, 200, 200, 255);

        public static void RefreshPlugins()
        {
            UITabContainer categoryContainer = GameObject.Find("CategoryContainer").GetComponent<UITabContainer>();
            var modsList = categoryContainer.Find("Mods").Find("Content");
            if (modsList == null)
            {
                return;
            }

            var uiView = GameObject.FindObjectOfType<UIView>();

            var plugins = PluginManager.instance.GetPluginsInfo();

            Dictionary<PluginManager.PluginInfo, string> pluginNames = new Dictionary<PluginManager.PluginInfo, string>();
            Dictionary<PluginManager.PluginInfo, string> pluginDescriptions = new Dictionary<PluginManager.PluginInfo, string>();
            Dictionary<PluginManager.PluginInfo, TimeSpan> pluginLastUpdatedTimeDelta = new Dictionary<PluginManager.PluginInfo, TimeSpan>();
            Dictionary<PluginManager.PluginInfo, TimeSpan> pluginSubscribedTimeDelta = new Dictionary<PluginManager.PluginInfo, TimeSpan>();

            foreach (var current in plugins)
            {
                IUserMod[] instances = current.GetInstances<IUserMod>();
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

            UIComponent uIComponent = modsList.GetComponent<UIComponent>();
            UITemplateManager.ClearInstances("ModEntryTemplate");

            var pluginsSorted = PluginManager.instance.GetPluginsInfo().ToArray();

            if (sortMode == SortMode.Alphabetical)
            {
                Array.Sort(pluginsSorted, (a, b) => pluginNames[a].CompareTo(pluginNames[b]));
            }
            else if (sortMode == SortMode.LastUpdated)
            {
                Array.Sort(pluginsSorted, (a, b) => pluginLastUpdatedTimeDelta[a].CompareTo(pluginLastUpdatedTimeDelta[b]));
            }
            else if (sortMode == SortMode.LastSubscribed)
            {
                Array.Sort(pluginsSorted, (a, b) => pluginSubscribedTimeDelta[a].CompareTo(pluginSubscribedTimeDelta[b]));
            }

            int count = 0;
            foreach (var current in pluginsSorted)
            {
                PackageEntry packageEntry = UITemplateManager.Get<PackageEntry>("ModEntryTemplate");
                uIComponent.AttachUIComponent(packageEntry.gameObject);

                packageEntry.entryName = String.Format("{0} (by {{0}})", pluginNames[current]);
                packageEntry.entryActive = current.isEnabled;
                packageEntry.pluginInfo = current;
                packageEntry.publishedFileId = current.publishedFileID;
                packageEntry.RequestDetails();

                var panel = packageEntry.gameObject.GetComponent<UIPanel>();
                panel.size = new Vector2(panel.size.x, 24.0f);
                /*               panel.color = count % 2 == 0 ? panel.color : new Color32
                                   ((byte)(panel.color.r * 0.60f), (byte)(panel.color.g * 0.60f), (byte)(panel.color.b * 0.60f), panel.color.a);*/

                var name = (UILabel)panel.Find("Name");
                name.textScale = 0.85f;
                name.tooltip = pluginDescriptions[current];
                name.textColor = /*count % 2 == 0 ? blackColor :*/ whiteColor;
                name.textScaleMode = UITextScaleMode.ControlSize;
                name.position = new Vector3(30.0f, 2.0f, name.position.z);

                var view = (UIButton)panel.Find("View");
                view.size = new Vector2(20.0f, 20.0f);
                view.textScale = 0.7f;
                view.position = new Vector3(675.0f, 2.0f, view.position.z);

                var share = (UIButton)panel.Find("Share");
                share.size = new Vector2(84.0f, 20.0f);
                share.textScale = 0.7f;
                share.isVisible = false;
                share.position = new Vector3(703.0f, 2.0f, share.position.z);
                share.isVisible = true;

                var lastUpdated = (UILabel)panel.Find("LastUpdated");
                if (lastUpdated == null)
                {
                    lastUpdated = uiView.AddUIComponent(typeof(UILabel)) as UILabel;
                }

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

                count++;
            }

            refreshModContents = true;
        }

        private static TimeSpan GetPluginLastModifiedDelta(PluginManager.PluginInfo plugin)
        {
            DateTime lastModified = DateTime.MinValue;

            foreach (var file in Directory.GetFiles(plugin.modPath))
            {
                if (Path.GetExtension(file) == ".dll")
                {
                    var tmp = File.GetLastWriteTime(file);
                    if (tmp > lastModified)
                    {
                        lastModified = tmp;
                    }
                }
            }

            return DateTime.Now - lastModified;
        }

        private static TimeSpan GetPluginCreatedDelta(PluginManager.PluginInfo plugin)
        {
            DateTime created = DateTime.MinValue;

            foreach (var file in Directory.GetFiles(plugin.modPath))
            {
                if (Path.GetExtension(file) == ".dll")
                {
                    var tmp = File.GetCreationTime(file);
                    if (tmp > created)
                    {
                        created = tmp;
                    }
                }
            }

            return DateTime.Now - created;
        }


    }

}
