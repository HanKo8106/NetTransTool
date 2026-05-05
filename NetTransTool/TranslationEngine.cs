using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace NetTransTool
{
    public class TranslationData
    {
        public List<string> ComponentNames { get; } = new List<string>();
        public List<string> Languages { get; } = new List<string>();
        // [componentName][languageName] = translationValue
        public Dictionary<string, Dictionary<string, string>> Translations { get; }
            = new Dictionary<string, Dictionary<string, string>>();
    }

    public class TranslationEngine
    {
        // 解析 多國名稱比對.txt，回傳 語言名稱 → 檔名 的字典
        public Dictionary<string, string> ParseMapping(string mappingFile)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = TryReadAllLines(mappingFile);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // 支援 "English → Resources.en.resx" 或 "English -> Resources.en.resx"
                int arrowIdx = line.IndexOf('→');
                if (arrowIdx < 0) arrowIdx = line.IndexOf("->");
                if (arrowIdx < 0) continue;
                string lang = line.Substring(0, arrowIdx).Trim();
                string fileName = line.Substring(arrowIdx + 1).Trim();
                // 移除 → 本身（兩個字元的 -> 已被 IndexOf 處理；→ 是單字元）
                fileName = fileName.TrimStart('>', ' ');
                if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(fileName))
                    result[lang] = fileName;
            }
            return result;
        }

        // 讀取 Excel，第 2 列為語言標題，第 3 列起為元件資料
        public TranslationData ReadExcel(string excelFile)
        {
            var data = new TranslationData();
            using (var wb = new XLWorkbook(excelFile))
            {
                var ws = wb.Worksheets.First();
                int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 2;

                // 第 2 列讀語言名稱（第 1 欄為元件名稱欄，略過）
                var colToLang = new Dictionary<int, string>();
                for (int col = 2; col <= lastCol; col++)
                {
                    string langName = ws.Cell(2, col).GetString().Trim();
                    if (!string.IsNullOrEmpty(langName))
                    {
                        colToLang[col] = langName;
                        if (!data.Languages.Contains(langName))
                            data.Languages.Add(langName);
                    }
                }

                // 第 3 列起讀元件翻譯
                for (int row = 3; row <= lastRow; row++)
                {
                    string componentName = ws.Cell(row, 1).GetString().Trim();
                    if (string.IsNullOrEmpty(componentName)) continue;

                    data.ComponentNames.Add(componentName);
                    var langMap = new Dictionary<string, string>();
                    foreach (var kv in colToLang)
                        langMap[kv.Value] = ws.Cell(row, kv.Key).GetString();
                    data.Translations[componentName] = langMap;
                }
            }
            return data;
        }

        // 更新單一 .resx 檔中指定元件的 <value>
        public bool UpdateResx(string resxPath, string componentName, string value, Action<string> log)
        {
            if (!File.Exists(resxPath))
            {
                log?.Invoke($"  [略過] 檔案不存在: {Path.GetFileName(resxPath)}");
                return false;
            }

            XDocument xml;
            try { xml = XDocument.Load(resxPath); }
            catch (Exception ex)
            {
                log?.Invoke($"  [錯誤] 無法讀取 {Path.GetFileName(resxPath)}: {ex.Message}");
                return false;
            }

            var dataEl = xml.Root?.Elements("data")
                .FirstOrDefault(e => (string)e.Attribute("name") == componentName);

            if (dataEl == null)
            {
                log?.Invoke($"  [略過] {Path.GetFileName(resxPath)}: 找不到元件 {componentName}");
                return false;
            }

            var valueEl = dataEl.Element("value");
            if (valueEl == null)
            {
                log?.Invoke($"  [略過] {Path.GetFileName(resxPath)}: {componentName} 無 <value> 節點");
                return false;
            }

            valueEl.Value = value;
            xml.Save(resxPath);
            log?.Invoke($"  [更新] {Path.GetFileName(resxPath)}: {value}");
            return true;
        }

        // 主翻譯流程
        public void RunTranslation(
            TranslationData data,
            Dictionary<string, string> mapping,
            string langFolder,
            string componentFilter,   // null = 全部
            bool syncRootResx,
            string rootResxPath,      // 根目錄 Resources.resx
            string langResxPath,      // Lang/Resources.resx
            Action<string> log)
        {
            var components = componentFilter == null
                ? data.ComponentNames
                : new List<string> { componentFilter };

            int updatedCount = 0;
            foreach (var component in components)
            {
                if (!data.Translations.TryGetValue(component, out var langTrans)) continue;
                log?.Invoke($"\n[元件] {component}");

                foreach (var lang in data.Languages)
                {
                    if (!langTrans.TryGetValue(lang, out string val)) continue;
                    if (!mapping.TryGetValue(lang, out string fileName))
                    {
                        log?.Invoke($"  [警告] 比對檔中找不到語言: {lang}");
                        continue;
                    }
                    string resxPath = Path.Combine(langFolder, fileName);
                    if (UpdateResx(resxPath, component, val, log))
                        updatedCount++;
                }

                // 同步根目錄 Resources.resx 與 Lang/Resources.resx（使用 English 值）
                if (syncRootResx && langTrans.TryGetValue("English", out string enVal))
                {
                    if (!string.IsNullOrEmpty(rootResxPath))
                        UpdateResx(rootResxPath, component, enVal, log);
                    if (!string.IsNullOrEmpty(langResxPath))
                        UpdateResx(langResxPath, component, enVal, log);
                }
            }
            log?.Invoke($"\n共更新 {updatedCount} 筆資料。");
        }

        private static string[] TryReadAllLines(string path)
        {
            try { return File.ReadAllLines(path, Encoding.UTF8); }
            catch { return File.ReadAllLines(path); }
        }
    }
}
