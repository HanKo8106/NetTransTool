using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace NetTransTool
{
    public partial class MainWindow : Window
    {
        private readonly TranslationEngine engine = new TranslationEngine();
        private TranslationData translationData;
        private Dictionary<string, string> mapping;

        private string BaseDir => AppDomain.CurrentDomain.BaseDirectory;
        private string ExcelPath => Path.Combine(BaseDir, "多國翻譯.xlsx");
        private string MappingPath => Path.Combine(BaseDir, "多國名稱比對.txt");
        private string LangFolder => Path.Combine(BaseDir, "Lang");
        private string RootResxPath => Path.Combine(BaseDir, "Resources.resx");
        private string LangResxPath => Path.Combine(LangFolder, "Resources.resx");

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(ExcelPath))
            {
                MessageBox.Show($"找不到 Excel 檔案：\n{ExcelPath}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!File.Exists(MappingPath))
            {
                MessageBox.Show($"找不到比對檔：\n{MappingPath}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                btnLoad.IsEnabled = false;
                Log("正在載入 Excel，請稍候...");

                TranslationData td = null;
                Dictionary<string, string> mp = null;
                await Task.Run(() =>
                {
                    td = engine.ReadExcel(ExcelPath);
                    mp = engine.ParseMapping(MappingPath);
                });

                translationData = td;
                mapping = mp;

                cmbComponent.Items.Clear();
                cmbComponent.Items.Add("【全部元件】");
                foreach (var name in translationData.ComponentNames)
                    cmbComponent.Items.Add(name);
                cmbComponent.SelectedIndex = 0;

                string msg = $"載入完成：{translationData.ComponentNames.Count} 個元件，{translationData.Languages.Count} 種語言";
                lblStatus.Text = msg;
                Log(msg);
                Log($"比對檔對應語言數：{mapping.Count}");
            }
            catch (Exception ex)
            {
                Log($"[錯誤] {ex.Message}");
                MessageBox.Show($"載入失敗：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLoad.IsEnabled = true;
            }
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (translationData == null || mapping == null)
            {
                MessageBox.Show("請先點擊「載入元件清單」", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Directory.Exists(LangFolder))
            {
                MessageBox.Show($"找不到 Lang 資料夾：\n{LangFolder}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string componentFilter = cmbComponent.SelectedIndex > 0 ? cmbComponent.SelectedItem?.ToString() : null;
            string label = componentFilter ?? "全部元件";
            Log($"\n══════════ 開始翻譯：{label} ══════════");

            bool syncRoot = chkSyncRoot.IsChecked == true;
            try
            {
                btnTranslate.IsEnabled = false;
                await Task.Run(() =>
                    engine.RunTranslation(
                        translationData,
                        mapping,
                        LangFolder,
                        componentFilter,
                        syncRoot,
                        RootResxPath,
                        LangResxPath,
                        Log));

                Log("══════════ 翻譯完成 ══════════");
                lblStatus.Text = $"翻譯完成：{label}";
            }
            catch (Exception ex)
            {
                Log($"\n[錯誤] {ex.Message}");
                lblStatus.Text = "翻譯失敗";
                MessageBox.Show($"翻譯失敗：\n{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTranslate.IsEnabled = true;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e) => txtLog.Clear();

        private void Log(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Log(message));
                return;
            }
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.ScrollToEnd();
        }
    }
}
