﻿// Project: MapleSeed
// File: Database.cs
// Updated By: Jared
// 

#region usings

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using libWiiSharp;
using MapleLib.Collections;
using MapleLib.Common;
using MapleLib.Properties;
using MapleLib.Structs;
using MapleLib.WiiU;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebClient = MapleLib.Network.Web.WebClient;

#endregion

namespace MapleLib
{
    public static class Database
    {
        static Database()
        {
            if (TitleDb == null) {
                TitleDb = new MapleDictionary(Settings.TitleDirectory);
            }
        }

        public static MapleDictionary TitleDb { get; }

        private static MapleList<Title> _db;

        public static void Load(bool forceLoad = true)
        {
            var dbFile = Path.GetFullPath(Path.Combine(Settings.ConfigDirectory, "database"));

            if (!File.Exists(dbFile) || new FileInfo(dbFile).Length <= 4000 || forceLoad) {
                Create();
            }
            else {
                var json = File.ReadAllText(dbFile);
                _db = JsonConvert.DeserializeObject<MapleList<Title>>(json);
            }
        }

        private static void Create()
        {
            var dbFile = Path.GetFullPath(Path.Combine(Settings.ConfigDirectory, "database"));

            var eShopTitlesStr = Resources.eShopAndDiskTitles; //index 12
            var eShopTitleUpdates = Resources.eShopTitleUpdates; //index 9
            var eShopTitleDLC = Resources.eShopTitleDLC; //index 8

            _db = new MapleList<Title>();
            
            foreach (var wiiutitleKey in WiiUTitleKeys()) {
                var id = wiiutitleKey["titleID"].Value<string>()?.ToUpper();
                var key = wiiutitleKey["titleKey"].Value<string>()?.ToUpper();
                var name = wiiutitleKey["name"].Value<string>()?.ToUpper();
                var region = wiiutitleKey["region"].Value<string>()?.ToUpper();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
                    continue;

                _db.Add(new Title {ID = id, Key = key, Name = name, Region = region});
            }

            var lines = eShopTitlesStr.Replace("|", "").Split('\n').ToList();
            for (int i = 11; i < lines.Count; i++) {
                var id = lines[i++].Replace("-", "").Trim().ToUpper();
                var title = _db.FirstOrDefault(x => x.ID == id) ?? new Title();

                title.ID = id;
                title.Name = lines[i++].Trim();
                title.ProductCode = lines[i++].Trim();
                title.CompanyCode = lines[i++].Trim();
                title.Notes = lines[i++].Trim();
                title.Versions = lines[i++].ToIntList(',');
                title.Region = lines[i++].Trim();

                int num = i++;
                var line = lines[num].ToLower().Trim();

                if (!line.Contains("yes") && !line.Contains("no"))
                    continue;

                title.AvailableOnCDN = lines[num].ToLower().Contains("yes");

                if (!_db.Contains(title))
                    _db.Add(title);
            }

            lines = eShopTitleUpdates.Replace("|", "").Split('\n').ToList();
            for (int i = 9; i < lines.Count; i++) {
                var line = lines[i].Trim();

                if (!line.Contains("-10"))
                    continue;

                var versionStr = lines[i + 3].Trim();
                var versions = versionStr.ToIntList(',');

                var titleId = line.Replace("-", "").ToUpper();
                var title = _db.ToList().Find(t => t.ID.Contains(titleId.Substring(8)));

                if (title != null)
                    title.Versions = versions;
            }

            lines = eShopTitleDLC.Replace("|", "").Split('\n').ToList();
            for (int i = 9; i < lines.Count; i++) {
                var line = lines[i].Trim();
                if (!line.Contains("-10"))
                    continue;

                var titleId = line.Replace("-", "").ToUpper();
                var title = _db.ToList().Find(t => t.ID.Contains(titleId.Substring(8)));

                if (title != null)
                    title.HasDLC = true;
            }

            var json = JsonConvert.SerializeObject(_db);
            File.WriteAllText(dbFile, json);
        }

        public static Title SearchById(string title_id)
        {
            return _db.ToList().Find(x => x.ID.ToUpper() == title_id.ToUpper());
        }

        public static async void Image(this eShopTitle title, bool save = true)
        {
            if (title.ProductCode.Length <= 6)
                return;

            var code = title.ProductCode.Substring(6);

            var doc = new XmlDocument();
            doc.Load(new MemoryStream(Encoding.UTF8.GetBytes(Resources.wiiutdb)));

            var values = doc.GetElementsByTagName("id").Cast<XmlNode>().ToList();
            var value = values.Find(x => x.InnerText.Contains(code));
            var imageCode = value.InnerText;

            var cacheDir = Path.Combine(Settings.ConfigDirectory, "cache");

            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            var langCodes = "US,EN,DE,FR,JA".Split(',').ToList();

            foreach (var langCode in langCodes) {
                var cachedFile = Path.Combine(cacheDir, $"{imageCode}.jpg");

                if (File.Exists(cachedFile)) {
                    title.ImageLocation = cachedFile;
                    break;
                }

                try {
                    var url = $"http://art.gametdb.com/wiiu/coverHQ/{langCode}/{imageCode}.jpg";

                    if (WebClient.UrlExists(url)) {
                        title.ImageLocation = cachedFile;

                        if (!save) return;
                        var data = await WebClient.DownloadDataAsync(url);
                        File.WriteAllBytes(title.ImageLocation, data);
                    }
                }
                catch {
                    TextLog.MesgLog.WriteLog($"Could not locate cover image for {title}");
                }
            }
        }

        private static void CleanUpdate(string outputDir, TMD tmd)
        {
            try {
                if (!Settings.StoreEncryptedContent) {
                    Toolbelt.AppendLog("  - Deleting Encrypted Contents...");
                    foreach (var t in tmd.Contents) {
                        if (!File.Exists(Path.Combine(outputDir, t.ContentID.ToString("x8")))) continue;
                        File.Delete(Path.Combine(outputDir, t.ContentID.ToString("x8")));
                    }
                }

                Toolbelt.AppendLog("  - Deleting CDecrypt, libeay32, and msvcr120d...");
                File.Delete(Path.Combine(outputDir, "CDecrypt.exe"));
                File.Delete(Path.Combine(outputDir, "libeay32.dll"));
                File.Delete(Path.Combine(outputDir, "msvcr120d.dll"));
            }
            catch {
                // ignored
            }
        }

        private static async Task<byte[]> DownloadData(string url)
        {
            byte[] data = {};

            try {
                data = await WebClient.DownloadDataAsync(url);
            }
            catch (WebException e) {
                TextLog.MesgLog.AddHistory($"{e.Message}\n{e.StackTrace}");
            }

            return data;
        }

        private static async Task<TMD> DownloadTmd(string url, string saveTo)
        {
            var data = await DownloadData(url);
            if (data.Length <= 0) return null;
            var tmd = TMD.Load(data);

            if (tmd.TitleVersion > 999) return null;
            Toolbelt.AppendLog("  - Parsing TMD...");
            Toolbelt.AppendLog($"    + Title Version: {tmd.TitleVersion}");
            Toolbelt.AppendLog($"    + {tmd.NumOfContents} Contents");

            File.WriteAllBytes(saveTo, data);
            return tmd;
        }

        private static async Task<TMD> LoadTmd(string id, string key, string outputDir, string titleUrl, string version)
        {
            var tmdFile = Path.Combine(outputDir, "tmd");

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
                return null;

            version = int.Parse(version) == 0 ? "" : $".{version}";
            if (await DownloadTmd(titleUrl + $"tmd{version}", tmdFile) == null) {
                var url = $"http://192.99.69.253/?key={key.ToLower()}&title={id.ToLower()}&type=tmd";
                await DownloadTmd(url, tmdFile);
            }

            var file = new FileInfo(tmdFile);
            if (!file.Exists || file.Length <= 0)
                return null;

            return TMD.Load(tmdFile);
        }

        public static async Task DownloadTitle(string id, string outputDir, string contentType, string version)
        {
            #region Setup

            var workingId = id.ToUpper();

            if (contentType == "Patch") {
                workingId = $"0005000E{workingId.Substring(8)}";

                if (Settings.Cemu173Patch)
                    outputDir = Path.Combine(Settings.BasePatchDir, workingId.Substring(8));
            }

            if (contentType == "DLC") {
                workingId = $"0005000C{workingId.Substring(8)}";

                if (Settings.Cemu173Patch)
                    outputDir = Path.Combine(Settings.BasePatchDir, workingId.Substring(8), "aoc");
            }

            Title title;
            if ((title = SearchById(workingId)) == null)
                throw new Exception("Could not locate the title key");

            var key = title.Key;
            var name = title.Name;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name)) return;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var str = $"Download {contentType} content to the following location?\n\"{outputDir}\"";
            var result = MessageBox.Show(str, name, MessageBoxButtons.YesNo);

            if (result != DialogResult.Yes)
                return;

            Toolbelt.AppendLog($"Output Directory '{outputDir}'");

            #endregion

            #region TMD

            Toolbelt.AppendLog("  - Loading TMD...");
            TMD tmd = null;

            var nusUrls = new List<string>
            {
                "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/",
                "http://nus.cdn.shop.wii.com/ccs/download/",
                "http://ccs.cdn.c.shop.nintendowifi.net/ccs/download/"
            };

            foreach (var nusUrl in nusUrls) {
                string titleUrl = $"{nusUrl}{workingId}/";
                tmd = await LoadTmd(id, key, outputDir, titleUrl, version);

                if (tmd != null)
                    break;
            }

            if (tmd == null) {
                TextLog.MesgLog.WriteError("Could not locate TMD. Is this content request valid?");
                return;
            }

            #endregion

            #region Ticket

            Toolbelt.AppendLog("Generating Ticket...");
            var ticket = Ticket.Load(MapleTicket.Create(SearchById(id)));
            ticket.Save(Path.Combine(outputDir, "cetk"));

            #endregion

            #region Content

            Toolbelt.AppendLog($"[+] [{contentType}] {name} v{tmd.TitleVersion}");
            Toolbelt.SetStatus($"Output Directory: {outputDir}");

            foreach (var nusUrl in nusUrls) {
                var url = nusUrl + workingId;
                if (await DownloadContent(tmd, outputDir, url) != 1)
                    continue;

                Toolbelt.AppendLog(string.Empty);
                Toolbelt.AppendLog("  - Decrypting Content");
                Toolbelt.AppendLog("  + This may take a minute. Please wait...");
                Toolbelt.SetStatus("Decrypting Content. This may take a minute. Please wait...", Color.OrangeRed);

                var code = await Toolbelt.CDecrypt(outputDir);
                if (code != 0) {
                    Toolbelt.AppendLog($"Error while decrypting {name}");
                    return;
                }

                CleanUpdate(outputDir, tmd);
                break;
            }

            #endregion

            WebClient.ResetDownloadProgressChanged();
            Toolbelt.AppendLog($"[+] [{contentType}] {name} v{tmd.TitleVersion} Finished.");
            Toolbelt.SetStatus($"[+] [{contentType}] {name} v{tmd.TitleVersion} Finished.");
        }

        private static async Task<int> DownloadContent(TMD tmd, string outputDir, string titleUrl)
        {
            var result = 0;
            for (var i = 0; i < tmd.NumOfContents; i++) {
                var i1 = i;
                result = await Task.Run(async () => {
                    var numc = tmd.NumOfContents;
                    var size = Toolbelt.SizeSuffix((long) tmd.Contents[i1].Size);
                    Toolbelt.AppendLog($"Downloading Content #{i1 + 1} of {numc}... ({size})");
                    var contentPath = Path.Combine(outputDir, tmd.Contents[i1].ContentID.ToString("x8"));

                    if (Toolbelt.IsValid(tmd.Contents[i1], contentPath)) {
                        //Toolbelt.AppendLog("   + Using Local File, Skipping...");
                    }
                    else
                        try {
                            var downloadUrl = $"{titleUrl}/{tmd.Contents[i1].ContentID:x8}";
                            await WebClient.DownloadFileAsync(downloadUrl, contentPath);
                        }
                        catch (Exception ex) {
                            Toolbelt.AppendLog($"Downloading Content #{i1 + 1} of {numc} failed...\n{ex.Message}");
                            return 0;
                        }
                    return 1;
                });
                if (result == 0)
                    break;
            }
            return result;
        }

        public static Task LoadLibrary(string titleDirectory)
        {
            var xmlFiles = Directory.GetFiles(titleDirectory, "meta.xml", SearchOption.AllDirectories);

            foreach (var xmlFile in xmlFiles) {
                var rootDir = Path.GetFullPath(Path.Combine(xmlFile, "../../"));
                var title_id = Helper.XmlGetStringByTag(xmlFile, "title_id");

                var title = SearchById(title_id);
                title.FolderLocation = rootDir;
                title.MetaLocation = xmlFile;
                TitleDb.Add(title);
            }

            return Task.Delay(0);
        }

        private static List<JObject> WiiUTitleKeys()
        {
            var wLoc = Path.Combine(Settings.ConfigDirectory, "titlekeys");

            string jsonStr;
            if (File.Exists(wLoc))
            {
                jsonStr = File.ReadAllText(wLoc);
            }
            else
            {
                jsonStr = WebClient.DownloadString("https://wiiu.titlekeys.com/json");
                File.WriteAllText(wLoc, jsonStr);
            }

            var jsonTitles = JsonConvert.DeserializeObject<ICollection<JObject>>(jsonStr);
            return jsonTitles.ToList();
        }

        private static JObject WiiUTitleKey(string id)
        {
            var wLoc = Path.Combine(Settings.ConfigDirectory, "titlekeys");

            string jsonStr;
            if (File.Exists(wLoc)) {
                jsonStr = File.ReadAllText(wLoc);
            }
            else {
                jsonStr = WebClient.DownloadString("https://wiiu.titlekeys.com/json");
                File.WriteAllText(wLoc, jsonStr);
            }

            var jsonTitles = JsonConvert.DeserializeObject<ICollection<JObject>>(jsonStr);
            return jsonTitles.ToList().Find(x => x["titleID"].Value<string>().ToUpper() == id);
        }
    }
}