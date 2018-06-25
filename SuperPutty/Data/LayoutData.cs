using System;
using System.IO;
using SuperPutty.Gui;

namespace SuperPutty.Data
{
    public class LayoutData
    {
        public const string AutoRestore = "<Auto Restore>";
        public const string AutoRestoreLayoutFileName = "AutoRestoreLayout.XML";

        public LayoutData(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileNameWithoutExtension(filePath);
        }

        public string Name { get; set; }
        public string FilePath { get; set; }

        public bool IsReadOnly { get; set; }

        public bool IsDefault => Name == SuperPuTTY.Settings.DefaultLayoutName;

        public override string ToString()
        {
            return IsDefault ? String.Format(LocalizedText.LayoutData_default, Name) : Name;
        }
    }

    public class LayoutChangedEventArgs : EventArgs
    {
        public LayoutData New { get; set; }
        public LayoutData Old { get; set; }
        public bool IsNewLayoutAlreadyActive { get; set; }
    }
}
