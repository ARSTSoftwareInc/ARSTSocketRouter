using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace ARSTLog
{
    internal class ARSTLogAPI
    {
        string _logPath = "";
        public int counter = 0, maxCounter = 200000;
        public bool autoApplyToFile = false;

        StringBuilder log = new StringBuilder();

        public void init(string logPath)
        {
            try
            {
                if (logPath == "") throw new Exception("Путь к лог файлу должен быть не пуст");
                _logPath = logPath;
            }
            catch (Exception ex)
            {
                throw new Exception("ARSTLog api error trace::init(): " + ex.ToString());
            }
        }

        public void saveLog()
        {
            try
            {
                File.WriteAllText(_logPath, log.ToString());
            }
            catch(Exception ex)
            {
                throw new Exception("ARSTLog api error trace::sveLog(): " + ex.ToString());
            }
        }

        public void warn(string text)
        {
            addToLog("WARNING: " + text);
        }

        public void error(string text)
        {
            addToLog("ERROR: " + text);
        }

        public void info(string text)
        {
            addToLog("INFO: " + text);
        }

        public void addToLog(string text, bool showDate = true)
        {
            if (showDate) text = $"[{DateTime.Now.ToString()}] " + text;
            log.Append(text + "\n");
            Console.WriteLine($"[PServer] {text}");

            if (autoApplyToFile) saveLog();
            if(counter > maxCounter)
            {
                counter = 0;
                log.Clear();
                log.Append($"[{DateTime.Now.ToString()}]: log cleared!\n\n");
                saveLog();
            }
            else counter++;
        }
    }
}