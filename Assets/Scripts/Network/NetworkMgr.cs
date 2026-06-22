
#if UNITY_WEBGL && !UNITY_EDITOR
#define USE_WEBSOCKET
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using JetBrains.Annotations;
using Managers;
using MessagePack;
using MyBox;
using Network;
using ScriptedObjs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using Color = UnityEngine.Color;

#if USE_WEBSOCKET
    using NativeWebSocket;
#else
    using System.Net.Sockets;
    using System.IO;
#endif

public class NetworkMgr : MonoBehaviour
    {
        [Header("Server Settings")] public string serverHost = "127.0.0.1";
        public int udpPort = 8080;
        public int tcpPort = 8081;
        public int wsPort = 8082;

        [NotNull] internal readonly Dictionary<string, NetworkBehaviour> InstantiatedPrefabs =
            new Dictionary<string, NetworkBehaviour>();

        [NotNull] internal readonly Dictionary<string, PlayerController> PlayerControllers = new Dictionary<string, PlayerController>();

        public string username => DataManager.Instance.CurrentSave.username;
        private string clientId;
        internal string playerId;
        private string roomId;
        internal string roomState;
        
        internal MatchData currentMatchData;
        internal int tasksCompleted;
        internal bool handledComplete = false;  
        
        private bool isRoomHost = false;
        private bool connectedToRoom = false;

        internal bool connected = false;
        private bool connecting = false;
        internal bool ready = false;
        
        public bool IsConnected => connected && ready;
        public bool IsConnectedToRoom => (connectedToRoom && IsConnected);
        public bool IsRoomHost => (isRoomHost && IsConnectedToRoom);
        

        private uint sequence = 0;
        private Queue<Action> mainThreadActions = new Queue<Action>();

        private Dictionary<uint, TaskCompletionSource<object>> pendingRequests =
            new Dictionary<uint, TaskCompletionSource<object>>();

        public static float UpdateCooldown = 1.0f;
        public static float RetryCooldown = 1.0f;

        private float _updateTimer;
        private float _retryTimer;

        private GameMgr _gameMgr;
        private HomeMgr _homeMgr;

        private void Awake()
        {
            if (Instance)
            {
                DestroyImmediate(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            Instance = this;
        }

        async void Start()
        {
            await ConnectToServer();
        }

#if USE_WEBSOCKET
        // WebGL: Use WebSocket
        private WebSocket websocket;

        private string[] connectionUrls = new string[]
        {
            "ws://127.0.0.1:8082/",
            "wss://hqhpftnv-8082.use.devtunnels.ms/",
            "wss://8wllqhqd-8082.use.devtunnels.ms/",
        };

        private int currentUrlIndex = 0;

        public async Task ConnectToServer()
        {
            connecting = true;
            
            // Try each URL until one works
            for (int i = 0; i < connectionUrls.Length; i++)
            {
                currentUrlIndex = i;
                string url = connectionUrls[i];
                
                Debug.Log($"Attempting connection {i + 1}/{connectionUrls.Length}: {url}");
                
                bool success = await TryConnect(url);
                
                if (success)
                {
                    Debug.Log($"Successfully connected to: {url}");
                    currentUrlIndex = 0;
                    return;
                }
                
                Debug.LogWarning($"Failed to connect to: {url}");
                
                // Wait a bit before trying next URL
                await UniTask.Delay(500,ignoreTimeScale:true);
            }
            
            // All URLs failed
            connecting = false;
            Debug.LogError("Failed to connect to any server URL");
            currentUrlIndex = 0;
        }

        private async Task<bool> TryConnect(string url)
        {
            try
            {
                websocket = new WebSocket(url);
                
                bool connectionSucceeded = false;
                bool connectionFailed = false;
                
                websocket.OnOpen += async () =>
                {
                    connecting = false;
                    connected = true;
                    connectionSucceeded = true;
                    Debug.Log($"WebSocket connection opened to {url}");
                    
                    // Send initial connect packet
                    await SendPacket(PacketType.CONNECT, new Dictionary<string, object>
                    {
                        { "client_type", "unity_client" }
                    });
                };
                
                websocket.OnMessage += (bytes) =>
                {
                    try
                    {
                        var packet = Packet.Deserialize(bytes);
                        lock (mainThreadActions)
                        {
                            mainThreadActions.Enqueue(async () => await ProcessPacket(packet, "WebSocket"));
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error processing message: {e.Message}\n{e.StackTrace}");
                    }
                };
                
                websocket.OnError += (errorMsg) =>
                {
                    Debug.LogError($"WebSocket error on {url}: {errorMsg}");
                    connectionFailed = true;
                };
                
                websocket.OnClose += (closeCode) =>
                {
                    Debug.Log($"WebSocket connection closed: {closeCode}");
                    connected = false;
                    connectionFailed = true;
                };
                
                await websocket.Connect();
                
                // Wait for connection to succeed or fail (with timeout)
                float timeout = 5f;
                float elapsed = 0f;
                
                while (!connectionSucceeded && !connectionFailed && elapsed < timeout)
                {
                    await UniTask.Delay(100,ignoreTimeScale:true);
                    elapsed += 0.1f;
                }
                
                if (connectionSucceeded)
                {
                    return true;
                }
                
                // Clean up failed connection
                if (websocket != null && websocket.State == WebSocketState.Open)
                {
                    await websocket.Close();
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception connecting to {url}: {e.Message}");
                return false;
            }
        }

        public async Task SendPacket(PacketType type, Dictionary<string, object> data)
        {
            if (websocket.State != WebSocketState.Open) return;

            var packet = new Packet { Type = type, Data = NetworkHelper.SanitizeData(data), Sequence = sequence++ };

            await websocket.Send(packet.Serialize());
        }

        public async Task<object> SendPacketWithResponse(PacketType type, Dictionary<string, object> data,
            float timeoutSeconds = 5f)
        {
            if (websocket.State != WebSocketState.Open) return null;

            var packet = new Packet { Type = type, Data = NetworkHelper.SanitizeData(data), Sequence = sequence++ };

            var tcs = new TaskCompletionSource<object>();
            pendingRequests.Add(packet.Sequence, tcs);
            await websocket.Send(packet.Serialize());

// Set a timeout that cancels the task internally if it takes too long
            var (isCanceled, result) = await tcs.Task.AsUniTask()
                .Timeout(TimeSpan.FromSeconds(timeoutSeconds))
                .SuppressCancellationThrow();

            if (isCanceled) 
            {
                Debug.LogWarning($"Request {packet.Sequence} timed out");
                pendingRequests.Remove(packet.Sequence);
                return null;
            }

            return result;


            return await tcs.Task;
        }
        
    public void DisconnectFromServer()
    {
        websocket.Close();
        connected = false;
        connectedToRoom = false;
    }
#else
        
    private UdpClient udpClient;
    private TcpClient tcpClient;
    private NetworkStream tcpStream;
    
    public async System.Threading.Tasks.Task ConnectToServer()
    {
        try
        {
            // Setup UDP
            udpClient = new UdpClient();
            udpClient.Connect(serverHost, udpPort);
            
            // Setup TCP
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverHost, tcpPort);
            tcpStream = tcpClient.GetStream();
            
            connected = true;
            Debug.Log($"Connected to server at {serverHost}");
            
            // Start receiving
            _ = ReceiveTCP();
            _ = ReceiveUDP();
            
            // Send connect packet
            await SendTCP(PacketType.CONNECT, new Dictionary<string, object>
            {
                { "client_type", "unity_client" }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection failed: {e.Message}");
            connected = false;
        }
    }
    


    #region networking logic


    public async System.Threading.Tasks.Task SendTCP(PacketType type, Dictionary<string, object> data)
    {
        if (!connected || tcpStream == null)
        {
            Debug.LogError("Error: not connected");
            return;
        }
        
        try
        {
            var packet = new Packet
            {
                Type = type,
                Sequence = sequence++,
                Data = NetworkHelper.SanitizeData(data)
            };
            
            byte[] serialized = packet.Serialize();

            //Debug.Log($"[TCP SEND] Type={type}, Seq={packet.Sequence}, Size={serialized.Length}");
            //Debug.Log($"[TCP SEND] Header hex: {BitConverter.ToString(serialized, 0, Math.Min(10, serialized.Length))}");

            // Create length prefix (big endian)
            byte[] lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(serialized.Length >> 24);
            lengthBytes[1] = (byte)((serialized.Length >> 16) & 0xFF);
            lengthBytes[2] = (byte)((serialized.Length >> 8) & 0xFF);
            lengthBytes[3] = (byte)(serialized.Length & 0xFF);
        
            
            //Debug.Log($"[TCP SEND] Length prefix: {serialized.Length} = {BitConverter.ToString(lengthBytes)}");

            
            // IMPORTANT: Send as a single write operation
            byte[] fullPacket = new byte[4 + serialized.Length];
            lengthBytes.CopyTo(fullPacket, 0);
            serialized.CopyTo(fullPacket, 4);
        
            await tcpStream.WriteAsync(fullPacket, 0, fullPacket.Length);
            await tcpStream.FlushAsync();  // Force immediate send

            // Small delay to prevent packets from being combined
            await UniTask.Delay(5,ignoreTimeScale:true);
        }
        catch (Exception e)
        {
            Debug.LogError($"TCP send error: {e.Message}");
            DisconnectFromServer();
        }
    }
    
    public async Task<object> SendTCPWithResponse(PacketType type, Dictionary<string, object> data, float timeoutSeconds = 5f)
    {
        if (!connected || tcpStream == null)
        {
            Debug.LogError("Error: not connected");
            return null;
        }
    
        var packet = new Packet
        {
            Type = type,
            Sequence = sequence++,
            Data = NetworkHelper.SanitizeData(data)
        };
    
        // Create completion source for this request
        var tcs = new TaskCompletionSource<object>();
        pendingRequests.Add(packet.Sequence, tcs);
        //Debug.Log(packet.Sequence);
    
        try
        {
            byte[] serialized = packet.Serialize();

            //Debug.Log($"[TCP SEND] Type={type}, Seq={packet.Sequence}, Size={serialized.Length}");
            //Debug.Log($"[TCP SEND] Header hex: {BitConverter.ToString(serialized, 0, Math.Min(10, serialized.Length))}");

            // Create length prefix (big endian)
            byte[] lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(serialized.Length >> 24);
            lengthBytes[1] = (byte)((serialized.Length >> 16) & 0xFF);
            lengthBytes[2] = (byte)((serialized.Length >> 8) & 0xFF);
            lengthBytes[3] = (byte)(serialized.Length & 0xFF);
        
            
            //Debug.Log($"[TCP SEND] Length prefix: {serialized.Length} = {BitConverter.ToString(lengthBytes)}");

            
            // IMPORTANT: Send as a single write operation
            byte[] fullPacket = new byte[4 + serialized.Length];
            lengthBytes.CopyTo(fullPacket, 0);
            serialized.CopyTo(fullPacket, 4);
        
            await tcpStream.WriteAsync(fullPacket, 0, fullPacket.Length);
            await tcpStream.FlushAsync();  // Force immediate send

            
            await UniTask.Delay(5,ignoreTimeScale:true);

// Set a timeout that cancels the task internally if it takes too long
            var (isCanceled, result) = await tcs.Task.AsUniTask()
                .Timeout(TimeSpan.FromSeconds(timeoutSeconds))
                .SuppressCancellationThrow();

            if (isCanceled) 
            {
                Debug.LogWarning($"Request {packet.Sequence} timed out");
                pendingRequests.Remove(packet.Sequence);
                return null;
            }
        
            return await tcs.Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"TCP send error: {e.Message}");
            pendingRequests.Remove(packet.Sequence);
            return null;
        }
    }

    
    public void SendUDP(PacketType type, Dictionary<string, object> data)
    {
        if (!connected || udpClient == null) return;
        
        try
        {
            var packet = new Packet
            {
                Type = type,
                Sequence = sequence++,
                Data = NetworkHelper.SanitizeData(data)
            };
            
            byte[] serialized = packet.Serialize();
            udpClient.Send(serialized, serialized.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP send error: {e.Message}");
        }
    }
    
    private async System.Threading.Tasks.Task ReceiveTCP()
    {
        byte[] lengthBuffer = new byte[4];
        
        while (connected && tcpStream != null)
        {
            try
            {
                // Read length prefix
                int bytesRead = await tcpStream.ReadAsync(lengthBuffer, 0, 4);
                if (bytesRead == 0) break;
                
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lengthBuffer);
                
                int length = BitConverter.ToInt32(lengthBuffer, 0);
                
                // Read packet data
                byte[] buffer = new byte[length];
                bytesRead = 0;
                while (bytesRead < length)
                {
                    int read = await tcpStream.ReadAsync(buffer, bytesRead, length - bytesRead);
                    if (read == 0) break;
                    bytesRead += read;
                }
                
                var packet = Packet.Deserialize(buffer);
                 HandlePacket(packet, "TCP");
            }
            catch (Exception e)
            {
                Debug.LogError($"TCP receive error: {e.Message}\n\n{e.StackTrace}");
                break;
            }
        }
    }
    
    private async System.Threading.Tasks.Task ReceiveUDP()
    {
        while (connected && udpClient != null)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                var packet = Packet.Deserialize(result.Buffer);
                HandlePacket(packet, "UDP");
            }
            catch (Exception e)
            {
                Debug.LogError($"UDP receive error: {e.Message}");
                await UniTask.Delay(10,ignoreTimeScale:true);
            }
        }
    }
    
    private void HandlePacket(Packet packet, string protocol)
    {
        // Queue for main thread
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(async () => await ProcessPacket(packet, protocol));
        }
    }

    public void DisconnectFromServer()
    {
        connected = false;
        connectedToRoom = false;
        udpClient?.Close();
        tcpClient?.Close();
    }
    
    #endregion
    
    #endif


    enum NetworkProtocol
    {
        TCP,
        UDP,
    }

    private async Task SendPacket(NetworkProtocol protocol, PacketType type, Dictionary<string, object> data)
    {
        if (!connected)
            return;
        
#if USE_WEBSOCKET
        await SendPacket(type, data);
#else
        if (protocol == NetworkProtocol.TCP)
        {
            await SendTCP(type, data);
        }
        else
        {
            SendUDP(type, data);
        }
#endif
    }

    private async Task<object> SendPacketWithResponse(NetworkProtocol protocol, PacketType type,
        Dictionary<string, object> data)
    {
        if (!connected)
            return null;

        #if USE_WEBSOCKET
        return await SendPacketWithResponse(type, data);
#else
        if (protocol == NetworkProtocol.TCP)
        {
            return await SendTCPWithResponse(type, data);
        }
        else
        {
            return null;
        }
#endif
    }
    
    private async void LateUpdate()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer >= UpdateCooldown)
        {
            _updateTimer = 0;
            if (connected) {
                await SendPacket(NetworkProtocol.TCP,PacketType.HEARTBEAT, new Dictionary<string, object>
                {
                    {"timestamp", Time.time}
                });
            }
        }

        if (!connected && !connecting)
        {
            _retryTimer += Time.deltaTime;
            if (_retryTimer >= RetryCooldown)
            {
                _retryTimer = 0;
                Debug.Log("Retrying connection to server");
                await ConnectToServer();
            }
        }
    }
    
    void Update()
    {
        Instance = this;

#if (!UNITY_WEBGL || UNITY_EDITOR ) && USE_WEBSOCKET
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
        // Execute main thread actions
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }    
        
    }
    
    private async Task ProcessPacket(Packet packet, string protocol)
    {

        
        //Debug.Log($"[{protocol}] Received: {packet.Type}");
        object result = null;
        switch (packet.Type)
        {
            case PacketType.WORLD_STATE:
                await HandleWorldState(packet.Data);
                break;
            case PacketType.PLAYER_STATE:
                await HandlePlayerState(packet.Data);
                break;
            case PacketType.JOIN_ROOM:
                await HandleJoinRoom(packet.Data);
                break;
            case PacketType.CREATE_ROOM:
                result = HandleCreateRoom(packet.Data);
                break;
            case PacketType.ROOM_LIST:
                result = HandleListRoom(packet.Data);
                break;
            case PacketType.CONNECT:
                await HandleConnect(packet.Data);
                break;
            case PacketType.DISCONNECT:
                await HandleDisconnect(packet.Data);
                break;
            case PacketType.HEARTBEAT:
                await HandleHeartbeat(packet.Data);
                break;
            case PacketType.INSTANTIATE:
                await HandleInstantiate(packet.Data);
                break;
            case PacketType.OBJECT_DESTROY:
                await HandleObjectDestroy(packet.Data);
                break;
            case PacketType.PLAYER_INFO:
                result = HandlePlayerInfo(packet.Data);
                break;
            case PacketType.SCENE_CHANGE:
                await HandleSceneChange(packet.Data);
                break;
            case PacketType.PLAYER_DAMAGE:
                HandlePlayerDamage(packet.Data);
                break;
            default:
                break;
        }
        
        // Check if this is a response to a pending request
        if (pendingRequests.ContainsKey(packet.Sequence))
        {
            var tcs = pendingRequests[packet.Sequence];
            tcs.SetResult(result);
            pendingRequests.Remove(packet.Sequence);
            return; // Don't process further
        }
    }
    
    

    private async Task HandleConnect(Dictionary<string, object> data)
    {
        if (data.TryGetValue("success", out object success) && (bool)success)
        {
            var version = data["version"].ToString();

            if (version == "post-tcp")
            {
#if USE_WEBSOCKET
                ready = true;
                clientId =  data["client_id"].ToString();
                Debug.Log("Connected to post-tcp!");
                #else
                await SendPacket(NetworkProtocol.UDP,PacketType.CONNECT, new Dictionary<string, object>
                {
                    { "client_id", data["client_id"] },
                });
#endif
            }
            else if (version == "post-udp")
            {
                ready = true;
                clientId =  data["client_id"].ToString();
            }
        }
    }

    private async Task HandleDisconnect(Dictionary<string, object> data)
    {
        if (data.TryGetValue("success", out object success) && (bool)success)
        {
            Debug.Log($"Disconnecting from {data["client_id"]}");
            var message = data["message"].ToString();
            if (message != "forced")
            {
                Debug.LogWarning(message);
            }
            
            foreach (var keyValuePair in InstantiatedPrefabs)
            {
                keyValuePair.Value.OnDisconnect();
                Destroy(keyValuePair.Value.gameObject);
            }
            
            DisconnectFromServer();

        }
    }

    private async Task HandleHeartbeat(Dictionary<string, object> data)
    {
        
    }

    private Dictionary<string,object> HandlePlayerInfo(Dictionary<string, object> data)
    {
        Debug.Log("Player info recieved: "+data["username"]);
        return data;
    }

    private async Task HandleObjectDestroy(Dictionary<string, object> data)
    {
        if (InstantiatedPrefabs.ContainsKey(data["object_id"].ToString())) {
            var obj = InstantiatedPrefabs[data["object_id"].ToString()];

            if (obj && obj.gameObject)
            {
                Destroy(obj.gameObject);
                if (PlayerControllers.ContainsValue(obj.As<PlayerController>()))
                {
                    PlayerControllers.Remove(data["object_owner"].ToString());
                }
            }
            
            InstantiatedPrefabs.Remove(data["object_id"].ToString());
        }
    }
    
    private async Task HandleInstantiate(Dictionary<string, object> data)
    {

        int prefabIndex = int.Parse(data["prefab_index"].ToString());
        string ownedPlayer =  data["player_id"].ToString();

        if (prefabIndex >= 0 && prefabIndex < ObjectRegistry.Instance.NetworkPrefabs.Count && !InstantiatedPrefabs.ContainsKey(data["object_id"].ToString()))
        {
            //Debug.Log("instantiating prefab with index  " + prefabIndex + " owned by " + ownedPlayer);
            var obj = GameObject.Instantiate(ObjectRegistry.Instance.NetworkPrefabs[prefabIndex]);
            obj.OwnedId = ownedPlayer;
            obj.ObjectId = data["object_id"].ToString();
            obj.transform.position = NetworkHelper.ListToVector3(MessagePackHelper.ToList(data["position"]));

            if (prefabIndex == 0 && obj.IsOwnedByClient)
            {
                await SendPacket(NetworkProtocol.TCP,PacketType.PLAYER_ASSIGN, new Dictionary<string, object>
                {
                    { "object_id",obj.ObjectId },
                });
            } 
            
            InstantiatedPrefabs.Add(obj.ObjectId,obj);
            
        }
    }

    private async Task HandleSnapshot(Dictionary<string, object> data)
    {
        
        roomState = data["state"].ToString();
        if (data.ContainsKey("objects"))
        {
            var objects = (object[])data["objects"];

            foreach (var pre_obj in objects)
            {
                var obj = (Dictionary<object, object>)pre_obj;
                NetworkBehaviour _obj;
                if (obj["object_owner"].ToString() != playerId)
                {
                    if (!InstantiatedPrefabs.ContainsKey(obj["object_id"].ToString()))
                    {
                        int prefabIndex = int.Parse(obj["object_type"].ToString());

                        if (prefabIndex >= 0 && prefabIndex < ObjectRegistry.Instance.NetworkPrefabs.Count)
                        {
                            _obj = GameObject.Instantiate(ObjectRegistry.Instance.NetworkPrefabs[prefabIndex]);
                            _obj.OwnedId = obj["object_owner"].ToString();
                            ;
                            _obj.ObjectId = obj["object_id"].ToString();

                            InstantiatedPrefabs.Add(_obj.ObjectId, _obj);
                            Debug.Log($"Added new {ObjectRegistry.Instance.NetworkPrefabs[prefabIndex].name} under {_obj.OwnedId}");
                        }
                    }
                }

                if (!InstantiatedPrefabs.ContainsKey(obj["object_id"].ToString()))
                {
                    continue;
                }
                
                _obj = InstantiatedPrefabs[obj["object_id"].ToString()];
                _obj.OwnedId = obj["object_owner"].ToString();
                
                
                if (!_obj.IsOwnedByClient)
                {
                    _obj.OnNetworkUpdate(obj);
                }
            }
        }

        if (data.ContainsKey("players"))
        {
            var players = (object[])data["players"];

            int deadPlayers = 0;
            foreach (var pre in players)
            {
                var player = MessagePackHelper.ToStringDict(pre);

                if (_homeMgr)
                {
                    if (!_homeMgr.RoomScreenMgr.loadedPlayers.Contains(player["player_id"].ToString()))
                    {
                        _homeMgr.RoomScreenMgr.OnPlayerJoined(player["player_id"].ToString());
                    }
                }


                if (player["player_id"].ToString() != playerId)
                {
                    if (InstantiatedPrefabs.ContainsKey(player["object_id"].ToString()))
                    {
                        var player_obj = (PlayerController)InstantiatedPrefabs[player["object_id"].ToString()];

                        if (!PlayerControllers.ContainsKey(player["player_id"].ToString()))
                        {
                            PlayerControllers[player["player_id"].ToString()] = player_obj;
                        }

                        if (player_obj)
                        {
                            player_obj.Health = float.Parse(player["health"].ToString());
                        }
                    }
                }
                
                //Debug.Log((bool)player["is_alive"]);
                
                if ((bool)player["is_alive"] == false)
                {
                    deadPlayers++;
                }
            }

            if (deadPlayers == players.Length && IsRoomHost && !handledComplete)
            {
                handledComplete = true;
                FindAnyObjectByType<GameMgr>().HandleGameComplete(false);;
            }
        }
        
        if (data.ContainsKey("tasks_completed") && IsRoomHost && data["state"].ToString() == "active" && !handledComplete) {

            tasksCompleted = int.Parse(data["tasks_completed"].ToString());

            var mapController = FindAnyObjectByType<MapController>();
            if (mapController) {
                if (tasksCompleted == mapController.TaskCount)
                {
                    handledComplete = true;
                    FindAnyObjectByType<GameMgr>().HandleGameComplete(true);;

                }
            }

        }

    }
    
    private async Task HandleWorldState(Dictionary<string, object> data)
    {
        // Update game objects based on world state
        // Parse players, enemy, tasks, etc.
        //Debug.Log("Received world state");
        await HandleSnapshot(data);
    }
    
    private async Task HandlePlayerState(Dictionary<string, object> data)
    {
        if (data.TryGetValue("event", out object eventObj))
        {
            string _event = eventObj.ToString();
            Debug.Log("Received event: " + _event);
            
            if (_event == "player_join")
            {
                _homeMgr.RoomScreenMgr.OnPlayerJoined(data["player_id"].ToString());
            }
            
            if (_event == "player_left")
            {
                _homeMgr.RoomScreenMgr.OnPlayerLeft(data["player_id"].ToString());
            }

            if (_event == "cosmetic_update")
            {
                PlayerController.HandleCosmeticUpdate(data["player_id"].ToString());
                await _homeMgr.RoomScreenMgr.OnPlayerCosmeticChange(data["player_id"].ToString());
            }
            
            if (_event == "game_started")
            {
                currentMatchData = MatchData.FromMpack(MessagePackHelper.ToStringDict(data["match"]));
                handledComplete = false;
            }
            
            if (_event == "game_ended")
            {
                var m = data["match"];
                await UniTask.WaitUntil( () => SceneManager.GetActiveScene().name == "HomeSceneTest" );
                await UniTask.Delay(100);
                
                
                var matchDataDict = MessagePackHelper.ToStringDict(m);
                var matchData = MatchData.FromMpack(matchDataDict);
                
                
                
                _homeMgr.RoomScreenMgr.ShowOnlineMenu(matchData);
                currentMatchData = null;
                handledComplete = false;
            }

            if (_event == "player_death")
            {
                Debug.Log($"{playerId} has died");
                if ((string)data["player_id"] == playerId)
                {
                    PlayerController.Instance.HandleDeath();
                }
            }

            if (_event == "end_game_seq")
            {
                if (!IsRoomHost)
                {
                    FindAnyObjectByType<GameMgr>().HandleGameComplete((bool)data["victory"]);

                }
            }
        }
    }
    
    private async Task HandleJoinRoom(Dictionary<string, object> data)
    {
        if (data.TryGetValue("success", out object success) && (bool)success)
        {
            if (PlayerController.Instance)
            {
                PlayerController.Instance.OnDisconnect();
                DestroyImmediate(PlayerController.Instance.gameObject);
            }
            
            roomId = data["room_id"].ToString();
            playerId = data["player_id"].ToString();
            
            isRoomHost = data["room_host"].ToString() == playerId;
            
            connectedToRoom = true;
            Debug.Log($"Joined room {roomId} as {playerId}");

            await UniTask.Delay(50);

            
            var snapshot = MessagePackHelper.ToStringDict(data["room_state"]);
            await HandleSnapshot(snapshot);

            
            await Instantiate(Resources.Load<NetworkBehaviour>("Player"), playerId);
        }
    }

    private string HandleCreateRoom(Dictionary<string, object> data)
    {
        return data["room_id"].ToString();
    }
    
    private object[] HandleListRoom(Dictionary<string, object> data)
    {

        object[] rooms = data["rooms"] as object[];

        return rooms;
    }

    private async Task HandleSceneChange(Dictionary<string, object> data)
    {

        var worked = data.TryGetValue("scene_id", out object sceneId);
        
        if (!worked)
            Debug.LogError("Scene id not present");
        
        FadeController.Instance.FadeAmount = 1;
        FadeController.Instance.FadeSpeed = 3;

        await UniTask.WaitForSeconds(3.5f);
        
        await SceneManager.LoadSceneAsync(sceneId.ToString());
    }

    private void HandlePlayerDamage(Dictionary<string, object> data)
    {
        PlayerController.Instance.HandleDamage(data);
    }
    
    
    // Public API for game
    public async Task JoinRoom(string roomId)
    {
        var rooms = await ListRooms();

        if (rooms.Length > 0)
        {
            roomId = (string)((Dictionary<object,object>)rooms[0])["room_id"];
        }

        
        await SendPacket(NetworkProtocol.TCP,PacketType.JOIN_ROOM, new Dictionary<string, object>
        {
            { "room_id", roomId },
            { "username", username },
            { "color", Color.black  },
            { "suit", DataManager.Instance.CurrentSave.suitIndex  },
            { "hat", DataManager.Instance.CurrentSave.hatIndex  },
            { "auto_create", true },
            { "room_info", 
                new Dictionary<string, object>(){
                    {"room_name", roomId }
                } 
            }
        });
    }

    public async Task<string> CreateRoom(string roomName = null)
    {
        if (roomName.IsNullOrEmpty())
            roomName = $"{username}'s Room";
        
        return (await SendPacketWithResponse(NetworkProtocol.TCP,PacketType.CREATE_ROOM, new Dictionary<string, object>
        {
            { "room_name", roomName },
            { "max_players", 4 },
        })).ToString();
    }

    public async Task SendPlayerState(string state, Dictionary<string, object> data)
    {
        
        data["event"] = state;
        data["player_id"] = playerId;

        await SendPacket(NetworkProtocol.TCP, PacketType.PLAYER_STATE, data);

    }

    public async Task SendPlayerDamage(float damage, PlayerController targetPlayer, EnemyController enemy = null)
    {
        var data = new Dictionary<string, object>();
        data["damage"] = damage;
        if (enemy) data["enemy_id"] = enemy.ObjectId;
        data["player_id"] = targetPlayer.OwnedId;

        await SendPacket(NetworkProtocol.TCP, PacketType.PLAYER_DAMAGE, data);
    }

    public async Task SendPlayerUpdate(Dictionary<string, object> data, PlayerController targetPlayer=null)
    {
        if (targetPlayer == null)
        {
            targetPlayer = PlayerController.Instance;
        } else if (!isRoomHost)
        {
            return;
        }
        data["player_id"] = targetPlayer.OwnedId;

        await SendPacket(NetworkProtocol.TCP, PacketType.PLAYER_UPDATE, data);
    }
    
    public async Task<Dictionary<string,object>> GetPlayerInfo(string playerId)
    {
        return await SendPacketWithResponse(NetworkProtocol.TCP, PacketType.PLAYER_INFO, new Dictionary<string, object>
        {
            { "player_id", playerId },
        }) as Dictionary<string, object>;
    }

    public async Task<object[]> ListRooms() {
        return await SendPacketWithResponse(NetworkProtocol.TCP,PacketType.ROOM_LIST, new Dictionary<string, object>
        {
            
        }) as object[];
    }
    
    public async Task Instantiate(NetworkBehaviour networkBehaviour, string assignedClient = null, bool onlySpawnOnOtherClients = false, Vector3 startPosition = default)
    {
        if (!ObjectRegistry.Instance.NetworkPrefabs.Contains(networkBehaviour))
        {
            Debug.LogError("Provided network behavior is not present in the prefab registry");
            return;
        }
        
        if (!IsConnectedToRoom)
        {
            Guid uuid = Guid.NewGuid();
            var obj = GameObject.Instantiate(networkBehaviour);
            obj.PrefabIndex = ObjectRegistry.Instance.NetworkPrefabs.IndexOf(networkBehaviour);
            
            InstantiatedPrefabs.Add(uuid+"_push", obj);
            Debug.LogWarning("Instantiated while not connected to room!");
            return;
        } 
        
        if (assignedClient == null)
            assignedClient = playerId;

        if (networkBehaviour.Is<PlayerController>())
        {
            if (!PlayerControllers.ContainsKey(assignedClient))
            {
                PlayerControllers[assignedClient] = (PlayerController)networkBehaviour;
            }
        }

        await SendPacket(NetworkProtocol.TCP,PacketType.INSTANTIATE, new Dictionary<string, object>
        {
            { "prefab_index", ObjectRegistry.Instance.NetworkPrefabs.IndexOf(networkBehaviour) },
            { "player_id", assignedClient },
            { "room_id", roomId },
            { "target", onlySpawnOnOtherClients ? "others" : "all" },
            { "position", NetworkHelper.Vector3ToList(startPosition) }
        });
    }

    public async Task Destroy(NetworkBehaviour networkBehaviour)
    {
        if (networkBehaviour.OwnedId == playerId)
        {
            await SendPacket(NetworkProtocol.TCP, PacketType.OBJECT_DESTROY, new Dictionary<string, object>
            {
                {"room_id", roomId},
                { "object_id", networkBehaviour.ObjectId }
            });
        }
    }

    public async Task StartGame()
    {
        await SendPacket(NetworkProtocol.TCP,PacketType.SCENE_CHANGE, new Dictionary<string, object>
        {
            { "scene_id", "ConnectTest0" }
        });
        await SendPacket(NetworkProtocol.TCP,PacketType.START_ROOM, new Dictionary<string, object>
        {

        });
    }

    public async Task StartGameTimer()
    {
        await SendPacket(NetworkProtocol.TCP,PacketType.START_ROOM, new Dictionary<string, object>
        {

        });
    }

    public async Task EndGame(bool victory)
    {
        await SendPacket(NetworkProtocol.TCP,PacketType.SCENE_CHANGE, new Dictionary<string, object>
        {
            { "scene_id", "HomeSceneTest" }
        });
        
        await SendPacket(NetworkProtocol.TCP,PacketType.END_ROOM, new Dictionary<string, object>
        {
            { "victory", victory }
        });
    }
    
    public async void UpdateNetworkObject(NetworkBehaviour networkBehaviour)
    {
        await UniTask.Delay(100);
        if (networkBehaviour)
        {
            var packet = await networkBehaviour.Serialize();

            packet["room_id"] = roomId;

            await SendPacket(NetworkProtocol.UDP, PacketType.OBJECT_UPDATE, packet);
        }
    }
    
    public async void CompleteTask()
    {
        await SendPacket(NetworkProtocol.TCP,PacketType.TASK_COMPLETE,new Dictionary<string, object>
        {
            
        });
    }
    
    private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstantiatedPrefabs.Clear();
        PlayerControllers.Clear();
        _gameMgr = FindAnyObjectByType<GameMgr>(FindObjectsInactive.Include);
        _homeMgr = FindAnyObjectByType<HomeMgr>(FindObjectsInactive.Include);
        
        
        if (IsConnectedToRoom)
        {
            

            if (scene.name == "ConnectTest0")
            {
                await Instantiate(Resources.Load<NetworkBehaviour>("Player"), playerId);
                if (IsRoomHost)
                {

                }
            } else if (scene.name == "HomeSceneTest")
            {
                        
                FadeController.Instance.FadeAmount = 0;
                FadeController.Instance.FadeSpeed = 3;
            }
        }
    }

    async void OnDestroy()
    {
#if !USE_WEBSOCKET
        udpClient?.Close();
        tcpClient?.Close();
    #else
        if (connected)
            await websocket?.Close()!;
#endif
        connected = false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    async void OnGUI() {
        if (ready && connected)
        {
            if (!connectedToRoom)
            {       
            } else {
                GUILayout.Label("Connected to "+roomId+" as "+username+" / "+playerId);
            }
        }
    }

    public static NetworkMgr Instance;
}