using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace ImprovedModsPanel
{

    public class ImprovedModsPanel
    {

        private static bool bootstrapped = false;

        private static RedirectCallsState revertState;
        private static RedirectCallsState revertState2;

        public static void Bootstrap()
        {
            try
            {
                if (bootstrapped)
                {
                    return;
                }

                var modsList = GameObject.Find("ModsList");
                if (modsList == null)
                {
                    return;
                }

                modsList.AddComponent<UpdateHook>().onUnityUpdate = () =>
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
                    typeof(CustomContentPanel).GetMethod("RefreshPlugins",
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

        public static void Revert()
        {
            if (!bootstrapped)
            {
                return;
            }

            var modsList = GameObject.Find("ModsList");
            if (modsList == null)
            {
                return;
            }

            RedirectionHelper.RevertRedirect(typeof(PackageEntry).GetMethod("FormatPackageName",
                    BindingFlags.Static | BindingFlags.NonPublic), revertState);

            RedirectionHelper.RevertRedirect(typeof(CustomContentPanel).GetMethod("RefreshPlugins",
                BindingFlags.Instance | BindingFlags.NonPublic), revertState2);

            bootstrapped = false;
        }

        private static string FormatPackageName(string entryName, string authorName, bool isWorkshopItem)
        {
            if (!isWorkshopItem)
            {
                return String.Format("{0} (by {1})", entryName, authorName);
            }
            else
            {
                return entryName;
            }
        }

        private static Color32 blackColor = new Color32(0, 0, 0, 255);
        private static Color32 whiteColor = new Color32(200, 200, 200, 255);

        public static void RefreshPlugins()
        {
            var modsList = GameObject.Find("ModsList");
            if (modsList == null)
            {
                return;
            }

            var plugins = PluginManager.instance.GetPluginsInfo();

            Dictionary<PluginManager.PluginInfo, string> pluginNames = new Dictionary<PluginManager.PluginInfo, string>();
            Dictionary<PluginManager.PluginInfo, string> pluginDescriptions = new Dictionary<PluginManager.PluginInfo, string>();

            foreach (var current in plugins)
            {
                IUserMod[] instances = current.GetInstances<IUserMod>();
                pluginNames.Add(current, instances[0].Name);
                pluginDescriptions.Add(current, instances[0].Description);
            }

            UIComponent uIComponent = modsList.GetComponent<UIComponent>();
            UITemplateManager.ClearInstances("ModEntryTemplate");

            var pluginsSorted = PluginManager.instance.GetPluginsInfo().ToArray();
            Array.Sort(pluginsSorted, (a, b) => pluginNames[a].CompareTo(pluginNames[b]));

            int count = 0;
            foreach (var current in pluginsSorted)
            {
                PackageEntry packageEntry = UITemplateManager.Get<PackageEntry>("ModEntryTemplate");
                uIComponent.AttachUIComponent(packageEntry.gameObject);
                packageEntry.entryName = pluginNames[current];
                packageEntry.entryActive = current.isEnabled;
                packageEntry.pluginInfo = current;
                packageEntry.publishedFileId = current.publishedFileID;
                packageEntry.RequestDetails();

                var panel = packageEntry.gameObject.GetComponent<UIPanel>();
                panel.size = new Vector2(panel.size.x, 24.0f);
                panel.color = count % 2 == 0 ? panel.color : new Color32
                    ((byte)(panel.color.r * 0.60f), (byte)(panel.color.g * 0.60f), (byte)(panel.color.b * 0.60f), panel.color.a);

                var name = (UILabel)panel.Find("Name");
                name.textScale = 0.85f;
                name.tooltip = pluginDescriptions[current];
                name.textColor = count % 2 == 0 ? blackColor : whiteColor;
                name.textScaleMode = UITextScaleMode.ControlSize;
                name.position = new Vector3(30.0f, 2.0f, name.position.z);

                var view = (UIButton)panel.Find("View");
                view.size = new Vector2(84.0f, 20.0f);
                view.textScale = 0.7f;
                view.text = "WORKSHOP";
                view.position = new Vector3(1011.0f, -2.0f, view.position.z);

                var share = (UIButton)panel.Find("Share");
                share.size = new Vector2(84.0f, 20.0f);
                share.textScale = 0.7f;
                share.position = new Vector3(1103.0f, -2.0f, share.position.z);

                var delete = (UIButton)panel.Find("Delete");
                delete.size = new Vector2(24.0f, 24.0f);
                delete.position = new Vector3(1195.0f, delete.position.y, delete.position.z);

                var active = (UICheckBox)panel.Find("Active");
                active.position = new Vector3(4.0f, active.position.y, active.position.z);

                var onOff = (UILabel)active.Find("OnOff");
                onOff.enabled = false;

                count++;
            }
        }

    }

}
