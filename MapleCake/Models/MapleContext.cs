﻿// Project: MapleCake
// File: MapleContext.cs
// Updated By: Jared
// 

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapleCake.Models.ContextMenu;
using MapleCake.Models.Interfaces;
using MapleCake.ViewModels;
using MapleLib;
using MapleLib.Structs;

namespace MapleCake.Models
{
    public static class MapleContext
    {
        private static MapleButtons Click => MainWindowViewModel.Instance.Click;

        private static Title SelectedItem => MainWindowViewModel.Instance.Config.SelectedItem;

        public static List<ICommandItem> CreateMenu()
        {
            if (SelectedItem == null)
                return null;

            var vers = string.Join(", ", SelectedItem.Versions.ToArray());
            var items = new List<ICommandItem> {new CommandItem {Text = SelectedItem.TitleID, ToolTip = vers}};

            if (SelectedItem.Versions.Any() || SelectedItem.DLC.Any())
                items.Add(new SeparatorCommandItem());

            CreateUpdateItems(items);

            if (items.Any(x => x.Text.Contains("Update")))
                items.Add(new SeparatorCommandItem());

            CreateDlcItems(items);

            //if (items.Any(x => x.Text.Contains("DLC"))) items.Add(new SeparatorCommandItem());

            return items;
        }

        private static void CreateUpdateItems(ICollection<ICommandItem> items)
        {
            if (SelectedItem.Versions.Any())
                items.Add(new CommandItem {Text = "[+] Update", ToolTip = "Add Update", Command = Click.AddUpdate});

            var dir = Path.Combine(Settings.BasePatchDir, SelectedItem.Lower8Digits);
            var meta = Path.Combine(dir, "meta", "meta.xml");

            if (File.Exists(meta))
                items.Add(new CommandItem {Text = "[-] Update", ToolTip = "Remove Update", Command = Click.RemoveUpdate});
        }

        private static void CreateDlcItems(ICollection<ICommandItem> items)
        {
            if (SelectedItem.DLC.Any())
                items.Add(new CommandItem {Text = "[+] DLC", ToolTip = "Add DLC", Command = Click.AddDLC});

            var dir = Path.Combine(Settings.BasePatchDir, SelectedItem.Lower8Digits, "aoc");
            var meta = Path.Combine(dir, "meta", "meta.xml");

            if (File.Exists(meta))
                items.Add(new CommandItem {Text = "[-] DLC", ToolTip = "Remove DLC", Command = Click.RemoveDLC});
        }
    }
}