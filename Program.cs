using ARSTConfig;
using ARSTLog;
using Fleck;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace protocolServer
{
    internal class Program
    {
        [DllImport("libc")]
        static extern uint geteuid();
        
        public static ARSTLogAPI ARSTLog = new ARSTLogAPI();
        public static aConfg config1 = new aConfg();

        static string ver = "0.1.6", mainUrl = "", serverUrl = "";
        public static int port = 0, aliveTimeSeconds = 0;
        static bool isNoBoot = false;

        static void Main(string[] args)
        {
            Console.WriteLine($"======= TCP-UDP Routing Protocol host v{ver} =======\nby ARST mechanic studio Inc, (C) 2026\n\n");

            try
            {
                Console.WriteLine("Protocol server process pre-initializing...");

                string logDir = "/var/tmp/protocolServer-log/";
                string logPath = "/etc/protocolServer/config.ini";

                if (args.Length == 0) Console.WriteLine("\n*** command line arguments list empty ***\n");
                else
                {
                    Console.WriteLine($"Target args: '{args[0]}'\n\n");
                    switch(args[0])
                    {
                        case "-help":
                            isNoBoot = true;
                            Console.WriteLine("Command line help.\n\n-help - write help command line list;\n-rstart - restarting main server host;\n-log - write log in to console.");
                            break;
                        case "-log":
                            isNoBoot = true;
                            string log = logDir + "log.txt";
                            Console.WriteLine("Checking log...");
                            if (!File.Exists(log)) throw new Exception("No log file on target directory: " + log);
                            Console.WriteLine("Writing log...");
                            Console.WriteLine($"\n***\n\n{File.ReadAllText(log)}\n\n***\n");
                            break;
                        case "-rstart":
                            isNoBoot = true;
                            Console.WriteLine("Initiating host restart...");
                            Process.Start("sudo", "systemctl restart pserver");
                            //Process.Start("/local/usr/bin/python", "/home/arst/python/aprcs.py start mono /home/arst/Debug/protocolServer.exe");
                            Environment.Exit(0);
                            break;
                        default: throw new Exception("Invalid argument!(Type '-help' for helping about commands)");
                    }
                }

                if(!isNoBoot)
                {
                    Console.WriteLine($"Log files directory verifying... [logDir='{logDir}']");
                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("Creating directory: " + logDir);
                        Directory.CreateDirectory(logDir);
                    }

                    Console.WriteLine("Log system initializing...");
                    string log = logDir + "log.txt";
                    if (File.Exists(log)) File.Delete(log);
                    ARSTLog.init(log);
                    ARSTLog.autoApplyToFile = true;
                    ARSTLog.info("Pre initiaizing phase 1 complete!");
                    ARSTLog.info("Configuration system initializing...");
                    config1.init(logPath);
                    ARSTLog.info("Configuration initialized!");
                    ARSTLog.info("Checking for root permission...\n");
                    if (!rootCheck()) throw new Exception("Server process must be runed in root!");
                }
            }
            catch(Exception ex) 
            {
                error(ex.Message, "SYS_PRE_INITIALIZE_FAIL", true);
            }

            if (!isNoBoot)
            {
                try
                {
                    ARSTLog.info("System initializing...");
                    ARSTLog.info("Attaching config...");
                    port = Convert.ToInt32(config1.read("port"));
                    //mainUrl = config1.read("mainUrl");
                    ARSTLog.info("Pending main tunnel URL...");
                    mainUrl = File.ReadAllText("/var/tunnel_url.txt", Encoding.UTF8);
                    serverUrl = config1.read("serverUrl");
                    aliveTimeSeconds = Convert.ToInt32(config1.read("aliveTimeSeconds"));

                    ARSTLog.addToLog($"========\nport={port}\nmainUrl={mainUrl}\nserveUrl={serverUrl}\naliveTimeSeconds={aliveTimeSeconds}\n========", false);
                    ARSTLog.info("Initializing redis database connection...");
                    RedisClient.Init();
                    ARSTLog.info("Initializing complete!\n\n");

                    start();
                }
                catch (Exception ex)
                {
                    error(ex.Message, "SYS_INITIALIZE_FAIL", true);
                }
            }
            else Environment.Exit(0);
        }

        static bool rootCheck()
        {
            return geteuid() == 0;
        }

        static void start()
        {
            try
            {
                ARSTLog.info("Starting WebSocket server...");

                var server = new WebSocketServer(serverUrl + ":" + port);
                server.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        ARSTLog.info($"New device connected. [client='{socket.ConnectionInfo.Id.ToString()}']");
                    };

                    socket.OnMessage = message => { handleMessage(socket, message); };
                    socket.OnClose = () => { RedisClient.RemoveDevice(socket); };
                });

                ARSTLog.info("Server started successful!");
                ARSTLog.info("Status: SERVER_AWAIT_COMMAND\n");
                while (true) Thread.Sleep(1000);
            }
            catch(Exception ex)
            {
                error(ex.Message, "SERVER_START_ERROR", true);
            }
        }

        public static void socketStatus(string text, string code, IWebSocketConnection socketConnection)
        {
            socketConnection.Send($"STATUS_{code}: {text}");
            if(code.Contains("INVALID")) ARSTLog.warn($"Socket status for connection '{socketConnection.ConnectionInfo.Id.ToString()}' --- STATUS_{code}: {text}");
            else ARSTLog.info($"Socket status for connection '{socketConnection.ConnectionInfo.Id.ToString()}' --- STATUS_{code}: {text}");
        }

        static void handleMessage(IWebSocketConnection socketConnection, string message)
        {
            try
            {
                Console.WriteLine("\n");
                if(message.Length < 201) ARSTLog.info($"Message handled! [client='{socketConnection.ConnectionInfo.Id.ToString()}', message='{message}']");
                else ARSTLog.info($"Message handled! [client='{socketConnection.ConnectionInfo.Id.ToString()}', messageLength={message.Length}]");

                JObject cmd;
                try { cmd = JObject.Parse(message); }
                catch(Exception ex)
                {
                    socketStatus(ex.Message, "INVALID_JSON", socketConnection);
                    return; 
                }

                string type = (string)cmd["type"];
                ARSTLog.info("Current message type: " + type);

                switch (type)
                {
                    case "INIT":
                        if (cmd["name"] == null ^ cmd["accessKey"] == null)
                        {
                            socketStatus("Missing fields.", "INVALID_INIT_REQUEST", socketConnection);
                            return;
                        }

                        RedisClient.RegisterDevice(socketConnection, (string)cmd["name"], (string)cmd["accessKey"]);
                        break;
                    case "SESSION_START":
                        if (cmd["from"] == null ^ cmd["to"] == null ^ cmd["accessKey"] == null ^ cmd["payload"] == null)
                        {
                            socketStatus("Missing fields.", "INVALID_SESSION_START_REQUEST", socketConnection);
                            return;
                        }

                        RedisClient.StartSession(socketConnection, (string)cmd["from"], (string)cmd["to"], (string)cmd["accessKey"], (string)cmd["payload"]);
                        break;
                    case "CONN_STOP":
                        RedisClient.RemoveDevice(socketConnection);
                        break;
                    case "KEEP_ME_ALIVE":
                        string name = (string)cmd["name"];
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            socketStatus("Empty device name.", "INVALID_KEEP_REQUEST_NAME", socketConnection);
                            return;
                        }

                        RedisClient.db.StringSet($"device:{name}:conn", socketConnection.ConnectionInfo.Id.ToString(), TimeSpan.FromSeconds(aliveTimeSeconds));
                        socketStatus($"{aliveTimeSeconds}", "YOU_CONNECTED", socketConnection);
                        break;
                    default:
                        socketStatus($"Target mode not exists on system handler('{type}':{type.Length}).", "INVALID_MODE_TYPE", socketConnection);
                        break;
                }
            }
            catch (Exception ex)
            {
                error(ex.Message, "HANDLE_MESSAGE_ERROR");
            }
        }

        public static void error(string errText, string errCode, bool isCritic = false)
        {
            if (isCritic)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                ARSTLog.error($"Internal critic error::{errCode}: {errText}");
                Console.ForegroundColor = ConsoleColor.White;

                Environment.Exit(1);
            }
            else ARSTLog.warn($"Exception::{errCode}: {errText}");
        }
    }
}