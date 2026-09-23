using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;

namespace ARSTConfig
{
    class aConfg
    {
        public int lastCfgStringIndexOnFile = 0;
        public string path = "";

        public void init(string cfgPath)
        {
            try
            {
                if (!File.Exists(cfgPath)) throw new Exception("Config file not found on target location: " + cfgPath);
                path = cfgPath;
            }
            catch(Exception ex)
            {
                throw new Exception("ARSTConfig init error: " + ex.ToString());
            }
        }

        public string read(string cfgName, string cfgPath = "")
        {
            string result = "";

            try
            {
                if (cfgPath == "") cfgPath = path;
                if (!File.Exists(cfgPath)) throw new Exception("Config file not found on target location: " + cfgPath);
                string[] content = File.ReadAllLines(cfgPath, Encoding.UTF8);

                for (int i = 0; i < content.Length; i++)
                {
                    string target = content[i];
                    if(target.Contains(cfgName + "="))
                    {
                        result = target.Replace(cfgName + "=", "");
                        lastCfgStringIndexOnFile = i;
                        break;
                    }
                }

                if (result == "") throw new Exception($"Target string not found on target file! [cfgName='{cfgName}', cfgPath='{cfgPath}']");
            }
            catch(Exception ex)
            {
                throw new Exception("ARSTConfig read error: " + ex.ToString());
            }

            return result;
        }

        public void write(string cfgName, string data, string cfgPath = "")
        {
            try
            {
                if (cfgPath == "") cfgPath = path;
                if (data != "")
                {
                    string res = read(cfgPath, cfgName);
                    string[] content = File.ReadAllLines(cfgPath, Encoding.UTF8);

                    for (int i = 0; i < content.Length; i++)
                    {
                        string target = content[i];
                        if (target.Contains(cfgName + "="))
                        {
                            content[i] = target.Replace(cfgName + "=", "") + data;
                            break;
                        }
                    }

                    File.WriteAllLines(cfgPath, content);
                }
                else throw new Exception("No data!");
            }
            catch(Exception ex)
            {
                throw new Exception("ARSTConfig write error: " + ex.ToString());
            }
        }
    }
}