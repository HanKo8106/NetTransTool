using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetTransTool
{
    public class TestCodeRabbit
    {
        // 問題1：密碼硬編碼在程式碼裡
        private string password = "admin123";

        // 問題2：公開欄位，應該用 property
        public int counter;

        public void LoadFile(string path)
        {
            // 問題3：沒有 try-catch，檔案不存在會直接 crash
            string content = File.ReadAllText(path);
            Console.WriteLine(content);
        }

        public int Divide(int a, int b)
        {
            // 問題4：沒有檢查除以零
            return a / b;
        }

        public void DoSomething()
        {
            // 問題5：無用的變數，從來沒用到
            int unused = 999;

            // 問題6：無窮迴圈風險
            while (counter > 0)
            {
                Console.WriteLine(counter);
                // 忘記 counter-- 
            }
        }

        public string GetUser(string id)
        {
            // 問題7：SQL Injection 風險
            return "SELECT * FROM users WHERE id = " + id;
        }
    }
}
