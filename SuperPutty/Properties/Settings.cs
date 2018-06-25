﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using log4net;
using Microsoft.Win32;
using SuperPutty.Data;
using SuperPutty.Utils;

namespace SuperPutty.Properties {
    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    internal sealed partial class Settings {

        private static readonly ILog Log = LogManager.GetLogger(typeof(Settings));

        public Settings() {
            SettingChanging += SettingChangingEventHandler;
            SettingsSaving += SettingsSavingEventHandler;
        }

        public string SettingsFilePath { get; private set; }

        protected override void OnSettingsLoaded(object sender, SettingsLoadedEventArgs e)
        {
            Log.InfoFormat("Settings Loaded");
            base.OnSettingsLoaded(sender, e);

            if (e.Provider is PortableSettingsProvider provider)
            {
                SettingsFilePath = provider.SettingsFilePath;
            }
        }
        
        private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e) {
            // Add code to handle the SettingChangingEvent event here.
            Log.DebugFormat("SettingChanging: name={0}, oldVal={1}, newVal={2}", e.SettingName, this[e.SettingName], e.NewValue);

        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // Add code to handle the SettingsSaving event here.
            Log.InfoFormat("Settings Saved");
        }

        /// <summary>
        /// Load old settings from registry into Settings
        /// </summary>
        public void ImportFromRegistry()
        {
            // Get Registry Entry for Putty Exe
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Jim Radford\SuperPuTTY\Settings");
            if (key != null)
            {
                string puttyExe = key.GetValue("PuTTYExe", "").ToString();
                if (File.Exists(puttyExe))
                {
                    Log.Debug("Imported PuttyExe from Registry");
                    PuttyExe = puttyExe;
                }

                string pscpExe = key.GetValue("PscpExe", "").ToString();
                if (File.Exists(pscpExe))
                {
                    Log.Debug("Imported PscpExe from Registry");
                    PscpExe = pscpExe;
                }
            }
        }

        public KeyboardShortcut[] LoadShortcuts()
        {
            List<KeyboardShortcut> shortcuts = new List<KeyboardShortcut>();

            foreach (SuperPuttyAction action in Enum.GetValues(typeof(SuperPuttyAction)))
            {
                string name = string.Format("Action_{0}_Shortcut", action);

                // default
                KeyboardShortcut ks = new KeyboardShortcut { Key = Keys.None };

                // try load froms settings, note that Ctrl/Strg have conflicts
                // http://blogs.msdn.com/b/michkap/archive/2010/06/05/10019465.aspx
                try
                {
                    Keys keys = (Keys)this[name];
                    ks = KeyboardShortcut.FromKeys(keys);
                }
                catch (ArgumentException ex)
                {
                    Log.Warn("Could not convert shortcut text to Keys, possible localization bug with Ctrl and Strg.  Setting to None: " + name, ex);
                }
                catch (SettingsPropertyNotFoundException)
                {
                    Log.Debug("Could not load shortcut for " + name + ", Setting to None.");
                }

                ks.Name = action.ToString();
                shortcuts.Add(ks);
            }

            return shortcuts.ToArray();
        }

        public void UpdateFromShortcuts(KeyboardShortcut[] shortcuts)
        {
            foreach (KeyboardShortcut ks in shortcuts)
            {
                SuperPuttyAction action = (SuperPuttyAction)Enum.Parse(typeof(SuperPuttyAction), ks.Name);
                string name = string.Format("Action_{0}_Shortcut", action);
                try
                {
                    this[name] = ks.Key | ks.Modifiers;
                }
                catch (ArgumentException ex)
                {
                    this[name] = Keys.None;
                    Log.WarnFormat("Could not update shortcut for " + name + ".  Setting to None.", ex);
                }
            }
        }
    }
}
