using Fleck;
using StackExchange.Redis;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Security.Policy;
using System.Timers;
using System.Xml.Linq;

class RedisClient
{
    public static ConnectionMultiplexer redis;
    public static IDatabase db;
    public static Dictionary<string, IWebSocketConnection> connections = new Dictionary<string, IWebSocketConnection>();
    public static Dictionary<string, string> devicesConnectionsDataList = new Dictionary<string, string>();

    public static void Init()
    {
        try
        {
            protocolServer.Program.ARSTLog.info("Initializing sending list timer...");
            string configuration = "localhost:6379";
            protocolServer.Program.ARSTLog.info($"Initializing Redis Database on '{configuration}'...");
            redis = ConnectionMultiplexer.Connect(configuration);
            db = redis.GetDatabase();
        }
        catch(Exception ex) { throw new Exception("Redis init error: " + ex.Message); }
    }

    public static void RegisterDevice(IWebSocketConnection socket, string name, string accessKey)
    {
        try
        {
            protocolServer.Program.ARSTLog.info($"Registering device on Redis Database... [socketId='{socket.ConnectionInfo.Id.ToString()}', name='{name}', accessKey='{accessKey}']");

            if (string.IsNullOrWhiteSpace(name))
            {
                protocolServer.Program.socketStatus("Empty device name.", "INVALID_REGISTERING_NAME", socket);
                return;
            }

            if (connections.ContainsKey(name))
            {
                protocolServer.Program.ARSTLog.info($"Device '{name}' already registered. Closing old connection and replacing with new.");
                connections[name].Close();
                connections.Remove(name);
            }

            protocolServer.Program.socketStatus($"Processing register device('{name}').", "OPERATION_EXEC", socket);

            connections[name] = socket;
            db.StringSet($"device:{name}:conn", socket.ConnectionInfo.Id.ToString(), TimeSpan.FromSeconds(protocolServer.Program.aliveTimeSeconds));
            db.StringSet($"device:{name}:key", accessKey);

            protocolServer.Program.socketStatus($"Device '{name}' registered done!", "OPERATION_SUCCESS", socket);
        }
        catch (Exception ex) { throw new Exception("Register device on database error: " + ex.Message); }
    }

    public static void StartSession(IWebSocketConnection socket, string from, string to, string accessKey, string payload)
    {
        try
        {
            protocolServer.Program.socketStatus($"Processing starting session with device('{to}')", "OPERATION_EXEC", socket);

            protocolServer.Program.ARSTLog.info("Checking device name...");
            if (!connections.ContainsKey(to))
            {
                protocolServer.Program.socketStatus($"Device name '{to}' not found.", "INVALID_USER_NAME", socket);
                return;
            }

            string storedKey = db.StringGet($"device:{to}:key");
            protocolServer.Program.ARSTLog.info($"Verifying security key... [device='{to}', storedkey='{storedKey}', accessKey='{accessKey}']");
            if(storedKey != accessKey)
            {
                protocolServer.Program.socketStatus($"Invalid access key('{accessKey}').", "ACCESS_DENIED", socket);
                return;
            }

            if (payload.Length < 201) protocolServer.Program.ARSTLog.info($"Trancivering from:'{from}' -> to:'{to}'(data: '{payload}')");
            else protocolServer.Program.ARSTLog.info($"Trancivering from:'{from}' -> to:'{to}'(data length: '{payload.Length}')");

            //sendTimer.Start();

            connections[to].Send(payload);
            protocolServer.Program.socketStatus("Start session done", "OPERATION_SUCCESS", socket);
        }
        catch(Exception ex)
        {
            throw new Exception("Start session error: " + ex.Message);
        }
    }

    public static void RemoveDevice(IWebSocketConnection socket)
    {
        try
        {
            protocolServer.Program.socketStatus($"Processing removing device... [socket='{socket.ConnectionInfo.Id.ToString()}']", "OPERATION_EXEC", socket);
            string keyToRemove = null;

            foreach (var kv in connections)
            {
                if (kv.Value == socket)
                {
                    keyToRemove = kv.Key;
                    break;
                }
            }

            if (keyToRemove != null)
            {
                connections.Remove(keyToRemove);
                db.KeyDelete($"device:{keyToRemove}:conn");
                db.KeyDelete($"device:{keyToRemove}:key");
                protocolServer.Program.ARSTLog.info($"Device disconnected and removed. [hostIP='{socket.ConnectionInfo.ClientIpAddress}', keyDelete='{$"device:{keyToRemove}:conn"}'::'{$"device:{keyToRemove}:key"}'].");
            }
        }
        catch (Exception ex)
        {
            protocolServer.Program.error(ex.Message, "REMOVE_DEVICE_FROM_DATABASE_ERROR");
        }
    }
}