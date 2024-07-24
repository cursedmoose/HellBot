#pragma warning disable 8600, 8602, 8603, 8604, 8618, 8625
#pragma warning disable SYSLIB0021

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using Websocket.Client;
using Monitor = OBSWebsocketDotNet.Types.Monitor;

namespace TwitchBot.OBS.Websocket
{
    internal class TestServerMessage
    {
        //
        // Summary:
        //     Server Message's operation code
        [JsonProperty(PropertyName = "op")]
        public ObsMessageTypes OperationCode { get; set; }

        //
        // Summary:
        //     Server Data
        [JsonProperty(PropertyName = "d")]
        public JObject Data { get; set; }
    }
    internal static class ObsMessageFactory
    {
        internal static JObject BuildMessage(ObsMessageTypes opCode, string messageType, JObject additionalFields, out string messageId)
        {
            messageId = Guid.NewGuid().ToString();
            JObject jObject = new JObject {
            {
                "op",
                (JToken)(int)opCode
            } };
            JObject jObject2 = new JObject();
            switch (opCode)
            {
                case ObsMessageTypes.Request:
                    jObject2.Add("requestType", (JToken)messageType);
                    jObject2.Add("requestId", (JToken)messageId);
                    jObject2.Add("requestData", additionalFields);
                    additionalFields = null;
                    break;
                case ObsMessageTypes.RequestBatch:
                    jObject2.Add("requestId", (JToken)messageId);
                    break;
            }

            if (additionalFields != null)
            {
                jObject2.Merge(additionalFields);
            }

            jObject.Add("d", jObject2);
            return jObject;
        }
    }
    internal enum ObsMessageTypes
    {
        Hello = 0,
        Identify = 1,
        Identified = 2,
        ReIdentify = 3,
        Event = 5,
        Request = 6,
        RequestResponse = 7,
        RequestBatch = 8,
        RequestBatchResponse = 9
    }

    //
    // Summary:
    //     Instance of a connection with an obs-websocket server
    public class HellbotObsWebsocket : IOBSWebsocket
    {
        private delegate void RequestCallback(OBSWebsocket sender, JObject body);

        private const string WEBSOCKET_URL_PREFIX = "ws://";

        private const int SUPPORTED_RPC_VERSION = 1;

        private TimeSpan wsTimeout = TimeSpan.FromSeconds(10.0);

        private string connectionPassword;

        private WebsocketClient wsConnection;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> responseHandlers;

        private static readonly Random random = new Random();

        private const string REQUEST_FIELD_VOLUME_DB = "inputVolumeDb";

        private const string REQUEST_FIELD_VOLUME_MUL = "inputVolumeMul";

        private const string RESPONSE_FIELD_IMAGE_DATA = "imageData";

        //
        // Summary:
        //     WebSocket request timeout, represented as a TimeSpan object
        public TimeSpan WSTimeout
        {
            get
            {
                return wsConnection?.ReconnectTimeout ?? wsTimeout;
            }
            set
            {
                wsTimeout = value;
                if (wsConnection != null)
                {
                    wsConnection.ReconnectTimeout = wsTimeout;
                }
            }
        }

        //
        // Summary:
        //     Current connection state
        public bool IsConnected
        {
            get
            {
                if (wsConnection != null)
                {
                    return wsConnection.IsRunning;
                }

                return false;
            }
        }

        //
        // Summary:
        //     The current program scene has changed.
        public event EventHandler<ProgramSceneChangedEventArgs> CurrentProgramSceneChanged;

        //
        // Summary:
        //     The list of scenes has changed. TODO: Make OBS fire this event when scenes are
        //     reordered.
        public event EventHandler<SceneListChangedEventArgs> SceneListChanged;

        //
        // Summary:
        //     Triggered when the scene item list of the specified scene is reordered
        public event EventHandler<SceneItemListReindexedEventArgs> SceneItemListReindexed;

        //
        // Summary:
        //     Triggered when a new item is added to the item list of the specified scene
        public event EventHandler<SceneItemCreatedEventArgs> SceneItemCreated;

        //
        // Summary:
        //     Triggered when an item is removed from the item list of the specified scene
        public event EventHandler<SceneItemRemovedEventArgs> SceneItemRemoved;

        //
        // Summary:
        //     Triggered when the visibility of a scene item changes
        public event EventHandler<SceneItemEnableStateChangedEventArgs> SceneItemEnableStateChanged;

        //
        // Summary:
        //     Triggered when the lock status of a scene item changes
        public event EventHandler<SceneItemLockStateChangedEventArgs> SceneItemLockStateChanged;

        //
        // Summary:
        //     Triggered when switching to another scene collection
        public event EventHandler<CurrentSceneCollectionChangedEventArgs> CurrentSceneCollectionChanged;

        //
        // Summary:
        //     Triggered when a scene collection is created, deleted or renamed
        public event EventHandler<SceneCollectionListChangedEventArgs> SceneCollectionListChanged;

        //
        // Summary:
        //     Triggered when switching to another transition
        public event EventHandler<CurrentSceneTransitionChangedEventArgs> CurrentSceneTransitionChanged;

        //
        // Summary:
        //     Triggered when the current transition duration is changed
        public event EventHandler<CurrentSceneTransitionDurationChangedEventArgs> CurrentSceneTransitionDurationChanged;

        //
        // Summary:
        //     Triggered when a transition between two scenes starts. Followed by OBSWebsocketDotNet.OBSWebsocket.CurrentProgramSceneChanged
        public event EventHandler<SceneTransitionStartedEventArgs> SceneTransitionStarted;

        //
        // Summary:
        //     Triggered when a transition (other than "cut") has ended. Please note that the
        //     from-scene field is not available in TransitionEnd
        public event EventHandler<SceneTransitionEndedEventArgs> SceneTransitionEnded;

        //
        // Summary:
        //     Triggered when a stinger transition has finished playing its video
        public event EventHandler<SceneTransitionVideoEndedEventArgs> SceneTransitionVideoEnded;

        //
        // Summary:
        //     Triggered when switching to another profile
        public event EventHandler<CurrentProfileChangedEventArgs> CurrentProfileChanged;

        //
        // Summary:
        //     Triggered when a profile is created, imported, removed or renamed
        public event EventHandler<ProfileListChangedEventArgs> ProfileListChanged;

        //
        // Summary:
        //     Triggered when the streaming output state changes
        public event EventHandler<StreamStateChangedEventArgs> StreamStateChanged;

        //
        // Summary:
        //     Triggered when the recording output state changes
        public event EventHandler<RecordStateChangedEventArgs> RecordStateChanged;

        //
        // Summary:
        //     Triggered when state of the replay buffer changes
        public event EventHandler<ReplayBufferStateChangedEventArgs> ReplayBufferStateChanged;

        //
        // Summary:
        //     Triggered when the preview scene selection changes (Studio Mode only)
        public event EventHandler<CurrentPreviewSceneChangedEventArgs> CurrentPreviewSceneChanged;

        //
        // Summary:
        //     Triggered when Studio Mode is turned on or off
        public event EventHandler<StudioModeStateChangedEventArgs> StudioModeStateChanged;

        //
        // Summary:
        //     Triggered when OBS exits
        public event EventHandler ExitStarted;

        //
        // Summary:
        //     Triggered when connected successfully to an obs-websocket server
        public event EventHandler Connected;

        //
        // Summary:
        //     Triggered when disconnected from an obs-websocket server
        public event EventHandler<ObsDisconnectionInfo> Disconnected;

        //
        // Summary:
        //     A scene item is selected in the UI
        public event EventHandler<SceneItemSelectedEventArgs> SceneItemSelected;

        //
        // Summary:
        //     A scene item transform has changed
        public event EventHandler<SceneItemTransformEventArgs> SceneItemTransformChanged;

        //
        // Summary:
        //     The audio sync offset of an input has changed
        public event EventHandler<InputAudioSyncOffsetChangedEventArgs> InputAudioSyncOffsetChanged;

        //
        // Summary:
        //     A filter was added to a source
        public event EventHandler<SourceFilterCreatedEventArgs> SourceFilterCreated;

        //
        // Summary:
        //     A filter was removed from a source
        public event EventHandler<SourceFilterRemovedEventArgs> SourceFilterRemoved;

        //
        // Summary:
        //     Filters in a source have been reordered
        public event EventHandler<SourceFilterListReindexedEventArgs> SourceFilterListReindexed;

        //
        // Summary:
        //     Triggered when the visibility of a filter has changed
        public event EventHandler<SourceFilterEnableStateChangedEventArgs> SourceFilterEnableStateChanged;

        //
        // Summary:
        //     A source has been muted or unmuted
        public event EventHandler<InputMuteStateChangedEventArgs> InputMuteStateChanged;

        //
        // Summary:
        //     The volume of a source has changed
        public event EventHandler<InputVolumeChangedEventArgs> InputVolumeChanged;

        //
        // Summary:
        //     A custom broadcast message was received
        public event EventHandler<VendorEventArgs> VendorEvent;

        //
        // Summary:
        //     These events are emitted by the OBS sources themselves. For example when the
        //     media file ends. The behavior depends on the type of media source being used.
        public event EventHandler<MediaInputPlaybackEndedEventArgs> MediaInputPlaybackEnded;

        //
        // Summary:
        //     These events are emitted by the OBS sources themselves. For example when the
        //     media file starts playing. The behavior depends on the type of media source being
        //     used.
        public event EventHandler<MediaInputPlaybackStartedEventArgs> MediaInputPlaybackStarted;

        //
        // Summary:
        //     This event is only emitted when something actively controls the media/VLC source.
        //     In other words, the source will never emit this on its own naturally.
        public event EventHandler<MediaInputActionTriggeredEventArgs> MediaInputActionTriggered;

        //
        // Summary:
        //     The virtual cam state has changed.
        public event EventHandler<VirtualcamStateChangedEventArgs> VirtualcamStateChanged;

        //
        // Summary:
        //     The current scene collection has begun changing.
        public event EventHandler<CurrentSceneCollectionChangingEventArgs> CurrentSceneCollectionChanging;

        //
        // Summary:
        //     The current profile has begun changing.
        public event EventHandler<CurrentProfileChangingEventArgs> CurrentProfileChanging;

        //
        // Summary:
        //     The name of a source filter has changed.
        public event EventHandler<SourceFilterNameChangedEventArgs> SourceFilterNameChanged;

        //
        // Summary:
        //     An input has been created.
        public event EventHandler<InputCreatedEventArgs> InputCreated;

        //
        // Summary:
        //     An input has been removed.
        public event EventHandler<InputRemovedEventArgs> InputRemoved;

        //
        // Summary:
        //     The name of an input has changed.
        public event EventHandler<InputNameChangedEventArgs> InputNameChanged;

        //
        // Summary:
        //     An input's active state has changed. When an input is active, it means it's being
        //     shown by the program feed.
        public event EventHandler<InputActiveStateChangedEventArgs> InputActiveStateChanged;

        //
        // Summary:
        //     An input's show state has changed. When an input is showing, it means it's being
        //     shown by the preview or a dialog.
        public event EventHandler<InputShowStateChangedEventArgs> InputShowStateChanged;

        //
        // Summary:
        //     The audio balance value of an input has changed.
        public event EventHandler<InputAudioBalanceChangedEventArgs> InputAudioBalanceChanged;

        //
        // Summary:
        //     The audio tracks of an input have changed.
        public event EventHandler<InputAudioTracksChangedEventArgs> InputAudioTracksChanged;

        //
        // Summary:
        //     The monitor type of an input has changed. Available types are: - `OBS_MONITORING_TYPE_NONE`
        //     - `OBS_MONITORING_TYPE_MONITOR_ONLY` - `OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT`
        public event EventHandler<InputAudioMonitorTypeChangedEventArgs> InputAudioMonitorTypeChanged;

        //
        // Summary:
        //     A high-volume event providing volume levels of all active inputs every 50 milliseconds.
        public event EventHandler<InputVolumeMetersEventArgs> InputVolumeMeters;

        //
        // Summary:
        //     The replay buffer has been saved.
        public event EventHandler<ReplayBufferSavedEventArgs> ReplayBufferSaved;

        //
        // Summary:
        //     A new scene has been created.
        public event EventHandler<SceneCreatedEventArgs> SceneCreated;

        //
        // Summary:
        //     A scene has been removed.
        public event EventHandler<SceneRemovedEventArgs> SceneRemoved;

        //
        // Summary:
        //     The name of a scene has changed.
        public event EventHandler<SceneNameChangedEventArgs> SceneNameChanged;

        //
        // Summary:
        //     Update message handler
        //
        // Parameters:
        //   eventType:
        //     Value of "event-type" in the JSON body
        //
        //   body:
        //     full JSON message body
        protected void ProcessEventType(string eventType, JObject body)
        {
            body = (JObject)body["eventData"];
            switch (eventType)
            {
                case "CurrentProgramSceneChanged":
                    this.CurrentProgramSceneChanged?.Invoke(this, new ProgramSceneChangedEventArgs((string?)body["sceneName"]));
                    break;
                case "SceneListChanged":
                    this.SceneListChanged?.Invoke(this, new SceneListChangedEventArgs(JsonConvert.DeserializeObject<List<JObject>>((string?)body["scenes"])));
                    break;
                case "SceneItemListReindexed":
                    this.SceneItemListReindexed?.Invoke(this, new SceneItemListReindexedEventArgs((string?)body["sceneName"], JsonConvert.DeserializeObject<List<JObject>>((string?)body["sceneItems"])));
                    break;
                case "SceneItemCreated":
                    this.SceneItemCreated?.Invoke(this, new SceneItemCreatedEventArgs((string?)body["sceneName"], (string?)body["sourceName"], (int)body["sceneItemId"], (int)body["sceneItemIndex"]));
                    break;
                case "SceneItemRemoved":
                    this.SceneItemRemoved?.Invoke(this, new SceneItemRemovedEventArgs((string?)body["sceneName"], (string?)body["sourceName"], (int)body["sceneItemId"]));
                    break;
                case "SceneItemEnableStateChanged":
                    this.SceneItemEnableStateChanged?.Invoke(this, new SceneItemEnableStateChangedEventArgs((string?)body["sceneName"], (int)body["sceneItemId"], (bool)body["sceneItemEnabled"]));
                    break;
                case "SceneItemLockStateChanged":
                    this.SceneItemLockStateChanged?.Invoke(this, new SceneItemLockStateChangedEventArgs((string?)body["sceneName"], (int)body["sceneItemId"], (bool)body["sceneItemLocked"]));
                    break;
                case "CurrentSceneCollectionChanged":
                    this.CurrentSceneCollectionChanged?.Invoke(this, new CurrentSceneCollectionChangedEventArgs((string?)body["sceneCollectionName"]));
                    break;
                case "SceneCollectionListChanged":
                    this.SceneCollectionListChanged?.Invoke(this, new SceneCollectionListChangedEventArgs(JsonConvert.DeserializeObject<List<string>>((string?)body["sceneCollections"])));
                    break;
                case "CurrentSceneTransitionChanged":
                    this.CurrentSceneTransitionChanged?.Invoke(this, new CurrentSceneTransitionChangedEventArgs((string?)body["transitionName"]));
                    break;
                case "CurrentSceneTransitionDurationChanged":
                    this.CurrentSceneTransitionDurationChanged?.Invoke(this, new CurrentSceneTransitionDurationChangedEventArgs((int)body["transitionDuration"]));
                    break;
                case "SceneTransitionStarted":
                    this.SceneTransitionStarted?.Invoke(this, new SceneTransitionStartedEventArgs((string?)body["transitionName"]));
                    break;
                case "SceneTransitionEnded":
                    this.SceneTransitionEnded?.Invoke(this, new SceneTransitionEndedEventArgs((string?)body["transitionName"]));
                    break;
                case "SceneTransitionVideoEnded":
                    this.SceneTransitionVideoEnded?.Invoke(this, new SceneTransitionVideoEndedEventArgs((string?)body["transitionName"]));
                    break;
                case "CurrentProfileChanged":
                    this.CurrentProfileChanged?.Invoke(this, new CurrentProfileChangedEventArgs((string?)body["profileName"]));
                    break;
                case "ProfileListChanged":
                    this.ProfileListChanged?.Invoke(this, new ProfileListChangedEventArgs(JsonConvert.DeserializeObject<List<string>>((string?)body["profiles"])));
                    break;
                case "StreamStateChanged":
                    this.StreamStateChanged?.Invoke(this, new StreamStateChangedEventArgs(new OutputStateChanged(body)));
                    break;
                case "RecordStateChanged":
                    this.RecordStateChanged?.Invoke(this, new RecordStateChangedEventArgs(new RecordStateChanged(body)));
                    break;
                case "CurrentPreviewSceneChanged":
                    this.CurrentPreviewSceneChanged?.Invoke(this, new CurrentPreviewSceneChangedEventArgs((string?)body["sceneName"]));
                    break;
                case "StudioModeStateChanged":
                    this.StudioModeStateChanged?.Invoke(this, new StudioModeStateChangedEventArgs((bool)body["studioModeEnabled"]));
                    break;
                case "ReplayBufferStateChanged":
                    this.ReplayBufferStateChanged?.Invoke(this, new ReplayBufferStateChangedEventArgs(new OutputStateChanged(body)));
                    break;
                case "ExitStarted":
                    this.ExitStarted?.Invoke(this, EventArgs.Empty);
                    break;
                case "SceneItemSelected":
                    this.SceneItemSelected?.Invoke(this, new SceneItemSelectedEventArgs((string?)body["sceneName"], (string?)body["sceneItemId"]));
                    break;
                case "SceneItemTransformChanged":
                    this.SceneItemTransformChanged?.Invoke(this, new SceneItemTransformEventArgs((string?)body["sceneName"], (string?)body["sceneItemId"], new SceneItemTransformInfo((JObject)body["sceneItemTransform"])));
                    break;
                case "InputAudioSyncOffsetChanged":
                    this.InputAudioSyncOffsetChanged?.Invoke(this, new InputAudioSyncOffsetChangedEventArgs((string?)body["inputName"], (int)body["inputAudioSyncOffset"]));
                    break;
                case "InputMuteStateChanged":
                    this.InputMuteStateChanged?.Invoke(this, new InputMuteStateChangedEventArgs((string?)body["inputName"], (bool)body["inputMuted"]));
                    break;
                case "InputVolumeChanged":
                    this.InputVolumeChanged?.Invoke(this, new InputVolumeChangedEventArgs(new InputVolume(body)));
                    break;
                case "SourceFilterCreated":
                    this.SourceFilterCreated?.Invoke(this, new SourceFilterCreatedEventArgs((string?)body["sourceName"], (string?)body["filterName"], (string?)body["filterKind"], (int)body["filterIndex"], (JObject)body["filterSettings"], (JObject)body["defaultFilterSettings"]));
                    break;
                case "SourceFilterRemoved":
                    this.SourceFilterRemoved?.Invoke(this, new SourceFilterRemovedEventArgs((string?)body["sourceName"], (string?)body["filterName"]));
                    break;
                case "SourceFilterListReindexed":
                    if (this.SourceFilterListReindexed != null)
                    {
                        List<FilterReorderItem> list = new List<FilterReorderItem>();
                        JsonConvert.PopulateObject(body["filters"]!.ToString(), list);
                        this.SourceFilterListReindexed?.Invoke(this, new SourceFilterListReindexedEventArgs((string?)body["sourceName"], list));
                    }

                    break;
                case "SourceFilterEnableStateChanged":
                    this.SourceFilterEnableStateChanged?.Invoke(this, new SourceFilterEnableStateChangedEventArgs((string?)body["sourceName"], (string?)body["filterName"], (bool)body["filterEnabled"]));
                    break;
                case "VendorEvent":
                    this.VendorEvent?.Invoke(this, new VendorEventArgs((string?)body["vendorName"], (string?)body["eventType"], body));
                    break;
                case "MediaInputPlaybackEnded":
                    this.MediaInputPlaybackEnded?.Invoke(this, new MediaInputPlaybackEndedEventArgs((string?)body["inputName"]));
                    break;
                case "MediaInputPlaybackStarted":
                    this.MediaInputPlaybackStarted?.Invoke(this, new MediaInputPlaybackStartedEventArgs((string?)body["sourceName"]));
                    break;
                case "MediaInputActionTriggered":
                    this.MediaInputActionTriggered?.Invoke(this, new MediaInputActionTriggeredEventArgs((string?)body["inputName"], (string?)body["mediaAction"]));
                    break;
                case "VirtualcamStateChanged":
                    this.VirtualcamStateChanged?.Invoke(this, new VirtualcamStateChangedEventArgs(new OutputStateChanged(body)));
                    break;
                case "CurrentSceneCollectionChanging":
                    this.CurrentSceneCollectionChanging?.Invoke(this, new CurrentSceneCollectionChangingEventArgs((string?)body["sceneCollectionName"]));
                    break;
                case "CurrentProfileChanging":
                    this.CurrentProfileChanging?.Invoke(this, new CurrentProfileChangingEventArgs((string?)body["profileName"]));
                    break;
                case "SourceFilterNameChanged":
                    this.SourceFilterNameChanged?.Invoke(this, new SourceFilterNameChangedEventArgs((string?)body["sourceName"], (string?)body["oldFilterName"], (string?)body["filterName"]));
                    break;
                case "InputCreated":
                    this.InputCreated?.Invoke(this, new InputCreatedEventArgs((string?)body["inputName"], (string?)body["inputKind"], (string?)body["unversionedInputKind"], (JObject)body["inputSettings"], (JObject)body["defaultInputSettings"]));
                    break;
                case "InputRemoved":
                    this.InputRemoved?.Invoke(this, new InputRemovedEventArgs((string?)body["inputName"]));
                    break;
                case "InputNameChanged":
                    this.InputNameChanged?.Invoke(this, new InputNameChangedEventArgs((string?)body["oldInputName"], (string?)body["inputName"]));
                    break;
                case "InputActiveStateChanged":
                    this.InputActiveStateChanged?.Invoke(this, new InputActiveStateChangedEventArgs((string?)body["inputName"], (bool)body["videoActive"]));
                    break;
                case "InputShowStateChanged":
                    this.InputShowStateChanged?.Invoke(this, new InputShowStateChangedEventArgs((string?)body["inputName"], (bool)body["videoShowing"]));
                    break;
                case "InputAudioBalanceChanged":
                    this.InputAudioBalanceChanged?.Invoke(this, new InputAudioBalanceChangedEventArgs((string?)body["inputName"], (double)body["inputAudioBalance"]));
                    break;
                case "InputAudioTracksChanged":
                    this.InputAudioTracksChanged?.Invoke(this, new InputAudioTracksChangedEventArgs((string?)body["inputName"], (JObject)body["inputAudioTracks"]));
                    break;
                case "InputAudioMonitorTypeChanged":
                    this.InputAudioMonitorTypeChanged?.Invoke(this, new InputAudioMonitorTypeChangedEventArgs((string?)body["inputName"], (string?)body["monitorType"]));
                    break;
                case "InputVolumeMeters":
                    this.InputVolumeMeters?.Invoke(this, new InputVolumeMetersEventArgs(JsonConvert.DeserializeObject<List<JObject>>((string?)body["inputs"])));
                    break;
                case "ReplayBufferSaved":
                    this.ReplayBufferSaved?.Invoke(this, new ReplayBufferSavedEventArgs((string?)body["savedReplayPath"]));
                    break;
                case "SceneCreated":
                    this.SceneCreated?.Invoke(this, new SceneCreatedEventArgs((string?)body["sceneName"], (bool)body["isGroup"]));
                    break;
                case "SceneRemoved":
                    this.SceneRemoved?.Invoke(this, new SceneRemovedEventArgs((string?)body["sceneName"], (bool)body["isGroup"]));
                    break;
                case "SceneNameChanged":
                    this.SceneNameChanged?.Invoke(this, new SceneNameChangedEventArgs((string?)body["oldSceneName"], (string?)body["sceneName"]));
                    break;
                case "InputSettingsChanged":

                    break;
                default:
                    Console.WriteLine($"Unsupported Event: {eventType}\n{body}");
                    break;
            }
        }

        //
        // Summary:
        //     Constructor
        public HellbotObsWebsocket()
        {
            responseHandlers = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();
        }

        //
        // Summary:
        //     Connect this instance to the specified URL, and authenticate (if needed) with
        //     the specified password. NOTE: Please subscribe to the Connected/Disconnected
        //     events (or atleast check the IsConnected property) to determine when the connection
        //     is actually fully established
        //
        // Parameters:
        //   url:
        //     Server URL in standard URL format.
        //
        //   password:
        //     Server password
        [Obsolete("Please use ConnectAsync, this function will be removed in the next version")]
        public void Connect(string url, string password)
        {
            ConnectAsync(url, password);
        }

        //
        // Summary:
        //     Connect this instance to the specified URL, and authenticate (if needed) with
        //     the specified password. NOTE: Please subscribe to the Connected/Disconnected
        //     events (or atleast check the IsConnected property) to determine when the connection
        //     is actually fully established
        //
        // Parameters:
        //   url:
        //     Server URL in standard URL format.
        //
        //   password:
        //     Server password
        public void ConnectAsync(string url, string password)
        {
            if (!url.ToLower().StartsWith("ws://"))
            {
                throw new ArgumentException("Invalid url, must start with 'ws://'");
            }

            if (wsConnection != null && wsConnection.IsRunning)
            {
                Disconnect();
            }

            wsConnection = new WebsocketClient(new Uri(url));
            wsConnection.IsReconnectionEnabled = false;
            wsConnection.ReconnectTimeout = null;
            wsConnection.ErrorReconnectTimeout = null;
            wsConnection.MessageReceived.Subscribe(delegate (ResponseMessage m)
            {
                Task.Run(delegate
                {
                    WebsocketMessageHandler(this, m);
                });
            });
            wsConnection.DisconnectionHappened.Subscribe(delegate (DisconnectionInfo d)
            {
                Task.Run(delegate
                {
                    OnWebsocketDisconnect(this, d);
                });
            });
            connectionPassword = password;
            wsConnection.StartOrFail();
        }

        //
        // Summary:
        //     Disconnect this instance from the server
        public void Disconnect()
        {
            connectionPassword = null;
            if (wsConnection != null)
            {
                try
                {
                    wsConnection.Stop(WebSocketCloseStatus.NormalClosure, "User requested disconnect");
                    ((IDisposable)wsConnection).Dispose();
                }
                catch
                {
                }

                wsConnection = null;
            }

            KeyValuePair<string, TaskCompletionSource<JObject>>[] array = responseHandlers.ToArray();
            responseHandlers.Clear();
            KeyValuePair<string, TaskCompletionSource<JObject>>[] array2 = array;
            foreach (KeyValuePair<string, TaskCompletionSource<JObject>> keyValuePair in array2)
            {
                keyValuePair.Value.TrySetCanceled();
            }
        }

        private void OnWebsocketDisconnect(object sender, DisconnectionInfo d)
        {
            if (d == null || !d.CloseStatus.HasValue)
            {
                this.Disconnected?.Invoke(sender, new ObsDisconnectionInfo(ObsCloseCodes.UnknownReason, null, d));
            }
            else
            {
                this.Disconnected?.Invoke(sender, new ObsDisconnectionInfo((ObsCloseCodes)d.CloseStatus.Value, d.CloseStatusDescription, d));
            }
        }

        private void WebsocketMessageHandler(object sender, ResponseMessage e)
        {
            if (e.MessageType != 0)
            {
                return;
            }

            TestServerMessage serverMessage = JsonConvert.DeserializeObject<TestServerMessage>(e.Text);
            JObject body = serverMessage.Data;
            switch (serverMessage.OperationCode)
            {
                case ObsMessageTypes.Hello:
                    HandleHello(body);
                    break;
                case ObsMessageTypes.Identified:
                    Task.Run(delegate
                    {
                        this.Connected?.Invoke(this, EventArgs.Empty);
                    });
                    break;
                case ObsMessageTypes.RequestResponse:
                case ObsMessageTypes.RequestBatchResponse:
                    if (body.ContainsKey("requestId"))
                    {
                        string key = (string?)body["requestId"];
                        if (responseHandlers.TryRemove(key, out var value))
                        {
                            value.SetResult(body);
                        }
                    }

                    break;
                case ObsMessageTypes.Event:
                    {
                        string eventType = body["eventType"]!.ToString();
                        Task.Run(delegate
                        {
                            ProcessEventType(eventType, body);
                        });
                        break;
                    }
            }
        }

        //
        // Summary:
        //     Sends a message to the websocket API with the specified request type and optional
        //     parameters
        //
        // Parameters:
        //   requestType:
        //     obs-websocket request type, must be one specified in the protocol specification
        //
        //   additionalFields:
        //     additional JSON fields if required by the request type
        //
        // Returns:
        //     The server's JSON response as a JObject
        public JObject SendRequest(string requestType, JObject additionalFields = null)
        {
            return SendRequest(ObsMessageTypes.Request, requestType, additionalFields);
        }

        //
        // Summary:
        //     Internal version which allows to set the opcode Sends a message to the websocket
        //     API with the specified request type and optional parameters
        //
        // Parameters:
        //   operationCode:
        //     Type/OpCode for this messaage
        //
        //   requestType:
        //     obs-websocket request type, must be one specified in the protocol specification
        //
        //   additionalFields:
        //     additional JSON fields if required by the request type
        //
        //   waitForReply:
        //     Should wait for reply vs "fire and forget"
        //
        // Returns:
        //     The server's JSON response as a JObject
        internal JObject SendRequest(ObsMessageTypes operationCode, string requestType, JObject additionalFields = null, bool waitForReply = true)
        {
            if (wsConnection == null)
            {
                throw new NullReferenceException("Websocket is not initialized");
            }

            TaskCompletionSource<JObject> taskCompletionSource = new TaskCompletionSource<JObject>();
            JObject jObject = null;
            string messageId;
            do
            {
                jObject = ObsMessageFactory.BuildMessage(operationCode, requestType, additionalFields, out messageId);
            }
            while (waitForReply && !responseHandlers.TryAdd(messageId, taskCompletionSource));
            wsConnection.Send(jObject.ToString());
            if (!waitForReply)
            {
                return null;
            }

            taskCompletionSource.Task.Wait(wsTimeout.Milliseconds);
            if (taskCompletionSource.Task.IsCanceled)
            {
                throw new ErrorResponseException("Request canceled", 0);
            }

            JObject result = taskCompletionSource.Task.Result;
            if (!(bool)result["requestStatus"]!["result"])
            {
                JObject jObject2 = (JObject)result["requestStatus"];
                throw new ErrorResponseException(string.Format("ErrorCode: {0}{1}", jObject2["code"], jObject2.ContainsKey("comment") ? string.Format(", Comment: {0}", jObject2["comment"]) : ""), (int)jObject2["code"]);
            }

            if (result.ContainsKey("responseData"))
            {
                return result["responseData"]!.ToObject<JObject>();
            }

            return new JObject();
        }

        //
        // Summary:
        //     Request authentication data. You don't have to call this manually.
        //
        // Returns:
        //     Authentication data in an OBSWebsocketDotNet.Communication.OBSAuthInfo object
        public OBSAuthInfo GetAuthInfo()
        {
            return new OBSAuthInfo(SendRequest("GetAuthRequired"));
        }

        //
        // Summary:
        //     Authenticates to the Websocket server using the challenge and salt given in the
        //     passed OBSWebsocketDotNet.Communication.OBSAuthInfo object
        //
        // Parameters:
        //   password:
        //     User password
        //
        //   authInfo:
        //     Authentication data
        //
        // Returns:
        //     true if authentication succeeds, false otherwise
        protected void SendIdentify(string password, OBSAuthInfo authInfo = null)
        {
            JObject jObject = new JObject {
            {
                "rpcVersion",
                (JToken)1
            } };
            if (authInfo != null)
            {
                string text = HashEncode(password + authInfo.PasswordSalt);
                string text2 = HashEncode(text + authInfo.Challenge);
                jObject.Add("authentication", (JToken)text2);
            }

            SendRequest(ObsMessageTypes.Identify, null, jObject, waitForReply: false);
        }

        //
        // Summary:
        //     Encode a Base64-encoded SHA-256 hash
        //
        // Parameters:
        //   input:
        //     source string
        protected string HashEncode(string input)
        {
            using SHA256Managed sHA256Managed = new SHA256Managed();
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            return Convert.ToBase64String(sHA256Managed.ComputeHash(bytes));
        }

        //
        // Summary:
        //     Generate a message ID
        //
        // Parameters:
        //   length:
        //     (optional) message ID length
        //
        // Returns:
        //     A random string of alphanumerical characters
        protected string NewMessageID(int length = 16)
        {
            string text = "";
            for (int i = 0; i < length; i++)
            {
                int index = random.Next(0, "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".Length - 1);
                text += "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[index];
            }

            return text;
        }

        private void HandleHello(JObject payload)
        {
            if (wsConnection.IsStarted)
            {
                OBSAuthInfo authInfo = null;
                if (payload.ContainsKey("authentication"))
                {
                    authInfo = new OBSAuthInfo((JObject)payload["authentication"]);
                }

                SendIdentify(connectionPassword, authInfo);
                connectionPassword = null;
            }
        }

        //
        // Summary:
        //     Get basic OBS video information
        public ObsVideoSettings GetVideoSettings()
        {
            return JsonConvert.DeserializeObject<ObsVideoSettings>(SendRequest("GetVideoSettings").ToString());
        }

        //
        // Summary:
        //     Saves a screenshot of a source to the filesystem. The `imageWidth` and `imageHeight`
        //     parameters are treated as \"scale to inner\", meaning the smallest ratio will
        //     be used and the aspect ratio of the original resolution is kept. If `imageWidth`
        //     and `imageHeight` are not specified, the compressed image will use the full resolution
        //     of the source. **Compatible with inputs and scenes.**
        //
        // Parameters:
        //   sourceName:
        //     Name of the source to take a screenshot of
        //
        //   imageFormat:
        //     Image compression format to use. Use `GetVersion` to get compatible image formats
        //
        //   imageFilePath:
        //     Path to save the screenshot file to. Eg. `C:\\Users\\user\\Desktop\\screenshot.png`
        //
        //   imageWidth:
        //     Width to scale the screenshot to
        //
        //   imageHeight:
        //     Height to scale the screenshot to
        //
        //   imageCompressionQuality:
        //     Compression quality to use. 0 for high compression, 100 for uncompressed. -1
        //     to use \"default\" (whatever that means, idk)
        //
        // Returns:
        //     Base64-encoded screenshot string
        public string SaveSourceScreenshot(string sourceName, string imageFormat, string imageFilePath, int imageWidth = -1, int imageHeight = -1, int imageCompressionQuality = -1)
        {
            JObject jObject = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "imageFormat",
                    (JToken)imageFormat
                },
                {
                    "imageFilePath",
                    (JToken)imageFilePath
                }
            };
            if (imageWidth > -1)
            {
                jObject.Add("imageWidth", (JToken)imageWidth);
            }

            if (imageHeight > -1)
            {
                jObject.Add("imageHeight", (JToken)imageHeight);
            }

            if (imageCompressionQuality > -1)
            {
                jObject.Add("imageCompressionQuality", (JToken)imageCompressionQuality);
            }

            return (string?)SendRequest("SaveSourceScreenshot", jObject)["imageData"];
        }

        //
        // Summary:
        //     Saves a screenshot of a source to the filesystem. The `imageWidth` and `imageHeight`
        //     parameters are treated as \"scale to inner\", meaning the smallest ratio will
        //     be used and the aspect ratio of the original resolution is kept. If `imageWidth`
        //     and `imageHeight` are not specified, the compressed image will use the full resolution
        //     of the source. **Compatible with inputs and scenes.**
        //
        // Parameters:
        //   sourceName:
        //     Name of the source to take a screenshot of
        //
        //   imageFormat:
        //     Image compression format to use. Use `GetVersion` to get compatible image formats
        //
        //   imageFilePath:
        //     Path to save the screenshot file to. Eg. `C:\\Users\\user\\Desktop\\screenshot.png`
        //
        // Returns:
        //     Base64-encoded screenshot string
        public string SaveSourceScreenshot(string sourceName, string imageFormat, string imageFilePath)
        {
            return SaveSourceScreenshot(sourceName, imageFormat, imageFilePath, -1, -1, -1);
        }

        //
        // Summary:
        //     Executes hotkey routine, identified by hotkey unique name
        //
        // Parameters:
        //   hotkeyName:
        //     Unique name of the hotkey, as defined when registering the hotkey (e.g. "ReplayBuffer.Save")
        public void TriggerHotkeyByName(string hotkeyName)
        {
            JObject additionalFields = new JObject {
            {
                "hotkeyName",
                (JToken)hotkeyName
            } };
            SendRequest("TriggerHotkeyByName", additionalFields);
        }

        //
        // Summary:
        //     Triggers a hotkey using a sequence of keys.
        //
        // Parameters:
        //   keyId:
        //     Main key identifier (e.g. OBS_KEY_A for key "A"). Available identifiers are here:
        //     https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h
        //
        //   keyModifier:
        //     Optional key modifiers object. You can combine multiple key operators. e.g. KeyModifier.Shift
        //     | KeyModifier.Control
        public void TriggerHotkeyByKeySequence(OBSHotkey keyId, KeyModifier keyModifier = KeyModifier.None)
        {
            JObject additionalFields = new JObject
            {
                {
                    "keyId",
                    (JToken)keyId.ToString()
                },
                {
                    "keyModifiers",
                    new JObject
                    {
                        {
                            "shift",
                            (JToken)((keyModifier & KeyModifier.Shift) == KeyModifier.Shift)
                        },
                        {
                            "alt",
                            (JToken)((keyModifier & KeyModifier.Alt) == KeyModifier.Alt)
                        },
                        {
                            "control",
                            (JToken)((keyModifier & KeyModifier.Control) == KeyModifier.Control)
                        },
                        {
                            "command",
                            (JToken)((keyModifier & KeyModifier.Command) == KeyModifier.Command)
                        }
                    }
                }
            };
            SendRequest("TriggerHotkeyByKeySequence", additionalFields);
        }

        //
        // Summary:
        //     Get the name of the currently active scene.
        //
        // Returns:
        //     Name of the current scene
        public string GetCurrentProgramScene()
        {
            return (string?)SendRequest("GetCurrentProgramScene")["currentProgramSceneName"];
        }

        //
        // Summary:
        //     Set the current scene to the specified one
        //
        // Parameters:
        //   sceneName:
        //     The desired scene name
        public void SetCurrentProgramScene(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            SendRequest("SetCurrentProgramScene", additionalFields);
        }

        //
        // Summary:
        //     Get OBS stats (almost the same info as provided in OBS' stats window)
        public ObsStats GetStats()
        {
            return JsonConvert.DeserializeObject<ObsStats>(SendRequest("GetStats").ToString());
        }

        //
        // Summary:
        //     List every available scene
        //
        // Returns:
        //     A System.Collections.Generic.List`1 of OBSWebsocketDotNet.Types.SceneBasicInfo
        //     objects describing each scene
        public List<SceneBasicInfo> ListScenes()
        {
            return GetSceneList().Scenes;
        }

        //
        // Summary:
        //     Get a list of scenes in the currently active profile
        public GetSceneListInfo GetSceneList()
        {
            return JsonConvert.DeserializeObject<GetSceneListInfo>(SendRequest("GetSceneList").ToString());
        }

        //
        // Summary:
        //     Get the specified scene's transition override info
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to return the override info
        //
        // Returns:
        //     TransitionOverrideInfo
        public TransitionOverrideInfo GetSceneSceneTransitionOverride(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            return SendRequest("GetSceneSceneTransitionOverride", additionalFields).ToObject<TransitionOverrideInfo>();
        }

        //
        // Summary:
        //     Set specific transition override for a scene
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to set the transition override
        //
        //   transitionName:
        //     Name of the transition to use
        //
        //   transitionDuration:
        //     Duration in milliseconds of the transition if transition is not fixed. Defaults
        //     to the current duration specified in the UI if there is no current override and
        //     this value is not given
        public void SetSceneSceneTransitionOverride(string sceneName, string transitionName, int transitionDuration = -1)
        {
            JObject jObject = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "transitionName",
                    (JToken)transitionName
                }
            };
            if (transitionDuration >= 0)
            {
                jObject.Add("transitionDuration", (JToken)transitionDuration);
            }

            SendRequest("SetSceneSceneTransitionOverride", jObject);
        }

        //
        // Summary:
        //     If your code needs to perform multiple successive T-Bar moves (e.g. : in an animation,
        //     or in response to a user moving a T-Bar control in your User Interface), set
        //     release to false and call ReleaseTBar later once the animation/interaction is
        //     over.
        //
        // Parameters:
        //   position:
        //     T-Bar position. This value must be between 0.0 and 1.0.
        //
        //   release:
        //     Whether or not the T-Bar gets released automatically after setting its new position
        //     (like a user releasing their mouse button after moving the T-Bar). Call ReleaseTBar
        //     manually if you set release to false. Defaults to true.
        public void SetTBarPosition(double position, bool release = true)
        {
            if (position < 0.0 || position > 1.0)
            {
                throw new ArgumentOutOfRangeException("position");
            }

            JObject additionalFields = new JObject
            {
                {
                    "position",
                    (JToken)position
                },
                {
                    "release",
                    (JToken)release
                }
            };
            SendRequest("SetTBarPosition", additionalFields);
        }

        //
        // Summary:
        //     Apply settings to a source filter
        //
        // Parameters:
        //   sourceName:
        //     Source with filter
        //
        //   filterName:
        //     Filter name
        //
        //   filterSettings:
        //     JObject with filter settings
        //
        //   overlay:
        //     Apply over existing settings?
        public void SetSourceFilterSettings(string sourceName, string filterName, JObject filterSettings, bool overlay = false)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                },
                { "filterSettings", filterSettings },
                {
                    "overlay",
                    (JToken)overlay
                }
            };
            SendRequest("SetSourceFilterSettings", additionalFields);
        }

        //
        // Summary:
        //     Apply settings to a source filter
        //
        // Parameters:
        //   sourceName:
        //     Source with filter
        //
        //   filterName:
        //     Filter name
        //
        //   filterSettings:
        //     Filter settings
        //
        //   overlay:
        //     Apply over existing settings?
        public void SetSourceFilterSettings(string sourceName, string filterName, FilterSettings filterSettings, bool overlay = false)
        {
            SetSourceFilterSettings(sourceName, filterName, JObject.FromObject(filterSettings), overlay);
        }

        //
        // Summary:
        //     Modify the Source Filter's visibility
        //
        // Parameters:
        //   sourceName:
        //     Source name
        //
        //   filterName:
        //     Source filter name
        //
        //   filterEnabled:
        //     New filter state
        public void SetSourceFilterEnabled(string sourceName, string filterName, bool filterEnabled)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                },
                {
                    "filterEnabled",
                    (JToken)filterEnabled
                }
            };
            SendRequest("SetSourceFilterEnabled", additionalFields);
        }

        //
        // Summary:
        //     Return a list of all filters on a source
        //
        // Parameters:
        //   sourceName:
        //     Source name
        public List<FilterSettings> GetSourceFilterList(string sourceName)
        {
            JObject additionalFields = new JObject {
            {
                "sourceName",
                (JToken)sourceName
            } };
            JObject jObject = SendRequest("GetSourceFilterList", additionalFields);
            if (!jObject.HasValues)
            {
                return new List<FilterSettings>();
            }

            return JsonConvert.DeserializeObject<List<FilterSettings>>(jObject["filters"]!.ToString());
        }

        //
        // Summary:
        //     Return a list of settings for a specific filter
        //
        // Parameters:
        //   sourceName:
        //     Source name
        //
        //   filterName:
        //     Filter name
        public FilterSettings GetSourceFilter(string sourceName, string filterName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                }
            };
            return JsonConvert.DeserializeObject<FilterSettings>(SendRequest("GetSourceFilter", additionalFields).ToString());
        }

        //
        // Summary:
        //     Remove the filter from a source
        //
        // Parameters:
        //   sourceName:
        //     Name of the source the filter is on
        //
        //   filterName:
        //     Name of the filter to remove
        public bool RemoveSourceFilter(string sourceName, string filterName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                }
            };
            try
            {
                SendRequest("RemoveSourceFilter", additionalFields);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return false;
        }

        //
        // Summary:
        //     Add a filter to a source
        //
        // Parameters:
        //   sourceName:
        //     Name of the source for the filter
        //
        //   filterName:
        //     Name of the filter
        //
        //   filterKind:
        //     Type of filter
        //
        //   filterSettings:
        //     JObject holding filter settings object
        public void CreateSourceFilter(string sourceName, string filterName, string filterKind, JObject filterSettings)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                },
                {
                    "filterKind",
                    (JToken)filterKind
                },
                { "filterSettings", filterSettings }
            };
            SendRequest("CreateSourceFilter", additionalFields);
        }

        //
        // Summary:
        //     Add a filter to a source
        //
        // Parameters:
        //   sourceName:
        //     Name of the source for the filter
        //
        //   filterName:
        //     Name of the filter
        //
        //   filterKind:
        //     Type of filter
        //
        //   filterSettings:
        //     Filter settings object
        public void CreateSourceFilter(string sourceName, string filterName, string filterKind, FilterSettings filterSettings)
        {
            CreateSourceFilter(sourceName, filterName, filterKind, JObject.FromObject(filterSettings));
        }

        //
        // Summary:
        //     Toggles the status of the stream output.
        //
        // Returns:
        //     New state of the stream output
        public bool ToggleStream()
        {
            return (bool)SendRequest("ToggleStream")["outputActive"];
        }

        //
        // Summary:
        //     Toggles the status of the record output.
        public void ToggleRecord()
        {
            SendRequest("ToggleRecord");
        }

        //
        // Summary:
        //     Gets the status of the stream output
        //
        // Returns:
        //     An OBSWebsocketDotNet.Types.OutputStatus object describing the current outputs
        //     states
        public OutputStatus GetStreamStatus()
        {
            return new OutputStatus(SendRequest("GetStreamStatus"));
        }

        //
        // Summary:
        //     Get the current transition name and duration
        //
        // Returns:
        //     An OBSWebsocketDotNet.Types.TransitionSettings object with the current transition
        //     name and duration
        public TransitionSettings GetCurrentSceneTransition()
        {
            return new TransitionSettings(SendRequest("GetCurrentSceneTransition"));
        }

        //
        // Summary:
        //     Set the current transition to the specified one
        //
        // Parameters:
        //   transitionName:
        //     Desired transition name
        public void SetCurrentSceneTransition(string transitionName)
        {
            JObject additionalFields = new JObject {
            {
                "transitionName",
                (JToken)transitionName
            } };
            SendRequest("SetCurrentSceneTransition", additionalFields);
        }

        //
        // Summary:
        //     Change the transition's duration
        //
        // Parameters:
        //   transitionDuration:
        //     Desired transition duration (in milliseconds)
        public void SetCurrentSceneTransitionDuration(int transitionDuration)
        {
            JObject additionalFields = new JObject {
            {
                "transitionDuration",
                (JToken)transitionDuration
            } };
            SendRequest("SetCurrentSceneTransitionDuration", additionalFields);
        }

        //
        // Summary:
        //     Change the current settings of a transition
        //
        // Parameters:
        //   transitionSettings:
        //     Transition settings (they can be partial)
        //
        //   overlay:
        //     Whether to overlay over the current settins or replace them
        //
        // Returns:
        //     Updated transition settings
        public void SetCurrentSceneTransitionSettings(JObject transitionSettings, bool overlay)
        {
            JObject additionalFields = new JObject
            {
                {
                    "transitionSettings",
                    JToken.FromObject(transitionSettings)
                },
                {
                    "overlay",
                    (JToken)overlay
                }
            };
            SendRequest("SetCurrentSceneTransitionSettings", additionalFields);
        }

        //
        // Summary:
        //     Change the volume of the specified source
        //
        // Parameters:
        //   inputName:
        //     Name of the source which volume will be changed
        //
        //   inputVolume:
        //     Desired volume. Must be between `0.0` and `1.0` for amplitude/mul (useDecibel
        //     is false), and under 0.0 for dB (useDecibel is true). Note: OBS will interpret
        //     dB values under -100.0 as Inf.
        //
        //   inputVolumeDb:
        //     Interperet `volume` data as decibels instead of amplitude/mul.
        public void SetInputVolume(string inputName, float inputVolume, bool inputVolumeDb = false)
        {
            JObject jObject = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            if (inputVolumeDb)
            {
                jObject.Add("inputVolumeDb", (JToken)inputVolume);
            }
            else
            {
                jObject.Add("inputVolumeMul", (JToken)inputVolume);
            }

            SendRequest("SetInputVolume", jObject);
        }

        //
        // Summary:
        //     Get the volume of the specified source Volume is between `0.0` and `1.0` if using
        //     amplitude/mul (useDecibel is false), under `0.0` if using dB (useDecibel is true).
        //
        // Parameters:
        //   inputName:
        //     Source name
        //
        // Returns:
        //     An OBSWebsocketDotNet.Types.VolumeInfoObject containing the volume and mute state
        //     of the specified source.
        public VolumeInfo GetInputVolume(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return new VolumeInfo(SendRequest("GetInputVolume", additionalFields));
        }

        //
        // Summary:
        //     Gets the audio mute state of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of input to get the mute state of
        //
        // Returns:
        //     Whether the input is muted
        public bool GetInputMute(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return (bool)SendRequest("GetInputMute", additionalFields)["inputMuted"];
        }

        //
        // Summary:
        //     Set the mute state of the specified source
        //
        // Parameters:
        //   inputName:
        //     Name of the source which mute state will be changed
        //
        //   inputMuted:
        //     Desired mute state
        public void SetInputMute(string inputName, bool inputMuted)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "inputMuted",
                    (JToken)inputMuted
                }
            };
            SendRequest("SetInputMute", additionalFields);
        }

        //
        // Summary:
        //     Toggle the mute state of the specified source
        //
        // Parameters:
        //   inputName:
        //     Name of the source which mute state will be toggled
        public void ToggleInputMute(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            SendRequest("ToggleInputMute", additionalFields);
        }

        //
        // Summary:
        //     Sets the transform and crop info of a scene item
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene that has the SceneItem
        //
        //   sceneItemId:
        //     Id of the Scene Item
        //
        //   sceneItemTransform:
        //     JObject holding transform settings
        public void SetSceneItemTransform(string sceneName, int sceneItemId, JObject sceneItemTransform)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                },
                { "sceneItemTransform", sceneItemTransform }
            };
            SendRequest("SetSceneItemTransform", additionalFields);
        }

        //
        // Summary:
        //     Sets the transform and crop info of a scene item
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene that has the SceneItem
        //
        //   sceneItemId:
        //     Id of the Scene Item
        //
        //   sceneItemTransform:
        //     Transform settings
        public void SetSceneItemTransform(string sceneName, int sceneItemId, SceneItemTransformInfo sceneItemTransform)
        {
            SetSceneItemTransform(sceneName, sceneItemId, JObject.FromObject(sceneItemTransform));
        }

        //
        // Summary:
        //     Set the current scene collection to the specified one
        //
        // Parameters:
        //   sceneCollectionName:
        //     Desired scene collection name
        public void SetCurrentSceneCollection(string sceneCollectionName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneCollectionName",
                (JToken)sceneCollectionName
            } };
            SendRequest("SetCurrentSceneCollection", additionalFields);
        }

        //
        // Summary:
        //     Get the name of the current scene collection
        //
        // Returns:
        //     Name of the current scene collection
        public string GetCurrentSceneCollection()
        {
            return (string?)SendRequest("GetSceneCollectionList")["currentSceneCollectionName"];
        }

        //
        // Summary:
        //     List all scene collections
        //
        // Returns:
        //     A System.Collections.Generic.List`1 of the names of all scene collections
        public List<string> GetSceneCollectionList()
        {
            return JsonConvert.DeserializeObject<List<string>>(SendRequest("GetSceneCollectionList")["sceneCollections"]!.ToString());
        }

        //
        // Summary:
        //     Set the current profile to the specified one
        //
        // Parameters:
        //   profileName:
        //     Name of the desired profile
        public void SetCurrentProfile(string profileName)
        {
            JObject additionalFields = new JObject {
            {
                "profileName",
                (JToken)profileName
            } };
            SendRequest("SetCurrentProfile", additionalFields);
        }

        //
        // Summary:
        //     List all profiles
        //
        // Returns:
        //     A System.Collections.Generic.List`1 of the names of all profiles
        public GetProfileListInfo GetProfileList()
        {
            return JsonConvert.DeserializeObject<GetProfileListInfo>(SendRequest("GetProfileList").ToString());
        }

        //
        // Summary:
        //     Start streaming. Will trigger an error if streaming is already active
        public void StartStream()
        {
            SendRequest("StartStream");
        }

        //
        // Summary:
        //     Stop streaming. Will trigger an error if streaming is not active.
        public void StopStream()
        {
            SendRequest("StopStream");
        }

        //
        // Summary:
        //     Start recording. Will trigger an error if recording is already active.
        public void StartRecord()
        {
            SendRequest("StartRecord");
        }

        //
        // Summary:
        //     Stop recording. Will trigger an error if recording is not active. File name for
        //     the saved recording
        public string StopRecord()
        {
            return (string?)SendRequest("StopRecord")["outputPath"];
        }

        //
        // Summary:
        //     Pause the current recording. Returns an error if recording is not active or already
        //     paused.
        public void PauseRecord()
        {
            SendRequest("PauseRecord");
        }

        //
        // Summary:
        //     Resume/unpause the current recording (if paused). Returns an error if recording
        //     is not active or not paused.
        public void ResumeRecord()
        {
            SendRequest("ResumeRecord");
        }

        //
        // Summary:
        //     Get the path of the current recording folder
        //
        // Returns:
        //     Current recording folder path
        public string GetRecordDirectory()
        {
            return (string?)SendRequest("GetRecordDirectory")["recordDirectory"];
        }

        //
        // Summary:
        //     Get current recording status.
        public RecordingStatus GetRecordStatus()
        {
            return JsonConvert.DeserializeObject<RecordingStatus>(SendRequest("GetRecordStatus").ToString());
        }

        //
        // Summary:
        //     Get the status of the OBS replay buffer.
        //
        // Returns:
        //     Current recording status. true when active
        public bool GetReplayBufferStatus()
        {
            return (bool)SendRequest("GetReplayBufferStatus")["outputActive"];
        }

        //
        // Summary:
        //     Get duration of the currently selected transition (if supported)
        //
        // Returns:
        //     Current transition duration (in milliseconds)
        public GetTransitionListInfo GetSceneTransitionList()
        {
            return JsonConvert.DeserializeObject<GetTransitionListInfo>(SendRequest("GetSceneTransitionList").ToString());
        }

        //
        // Summary:
        //     Get status of Studio Mode
        //
        // Returns:
        //     Studio Mode status (on/off)
        public bool GetStudioModeEnabled()
        {
            return (bool)SendRequest("GetStudioModeEnabled")["studioModeEnabled"];
        }

        //
        // Summary:
        //     Enables or disables studio mode
        //
        // Parameters:
        //   studioModeEnabled:
        public void SetStudioModeEnabled(bool studioModeEnabled)
        {
            JObject additionalFields = new JObject {
            {
                "studioModeEnabled",
                (JToken)studioModeEnabled
            } };
            SendRequest("SetStudioModeEnabled", additionalFields);
        }

        //
        // Summary:
        //     Get the name of the currently selected preview scene. Note: Triggers an error
        //     if Studio Mode is disabled
        //
        // Returns:
        //     Preview scene name
        public string GetCurrentPreviewScene()
        {
            return (string?)SendRequest("GetCurrentPreviewScene")["currentPreviewSceneName"];
        }

        //
        // Summary:
        //     Change the currently active preview/studio scene to the one specified. Triggers
        //     an error if Studio Mode is disabled
        //
        // Parameters:
        //   sceneName:
        //     Preview scene name
        public void SetCurrentPreviewScene(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            SendRequest("SetCurrentPreviewScene", additionalFields);
        }

        //
        // Summary:
        //     Change the currently active preview/studio scene to the one specified. Triggers
        //     an error if Studio Mode is disabled.
        //
        // Parameters:
        //   previewScene:
        //     Preview scene object
        public void SetCurrentPreviewScene(ObsScene previewScene)
        {
            SetCurrentPreviewScene(previewScene.Name);
        }

        //
        // Summary:
        //     Triggers the current scene transition. Same functionality as the `Transition`
        //     button in Studio Mode
        public void TriggerStudioModeTransition()
        {
            SendRequest("TriggerStudioModeTransition");
        }

        //
        // Summary:
        //     Toggles the state of the replay buffer output.
        public void ToggleReplayBuffer()
        {
            SendRequest("ToggleReplayBuffer");
        }

        //
        // Summary:
        //     Start recording into the Replay Buffer. Triggers an error if the Replay Buffer
        //     is already active, or if the "Save Replay Buffer" hotkey is not set in OBS' settings
        public void StartReplayBuffer()
        {
            SendRequest("StartReplayBuffer");
        }

        //
        // Summary:
        //     Stop recording into the Replay Buffer. Triggers an error if the Replay Buffer
        //     is not active.
        public void StopReplayBuffer()
        {
            SendRequest("StopReplayBuffer");
        }

        //
        // Summary:
        //     Save and flush the contents of the Replay Buffer to disk. Basically the same
        //     as triggering the "Save Replay Buffer" hotkey in OBS. Triggers an error if Replay
        //     Buffer is not active.
        public void SaveReplayBuffer()
        {
            SendRequest("SaveReplayBuffer");
        }

        //
        // Summary:
        //     Set the audio sync offset of the specified source
        //
        // Parameters:
        //   inputName:
        //     Source name
        //
        //   inputAudioSyncOffset:
        //     Audio offset (in nanoseconds) for the specified source
        public void SetInputAudioSyncOffset(string inputName, int inputAudioSyncOffset)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "inputAudioSyncOffset",
                    (JToken)inputAudioSyncOffset
                }
            };
            SendRequest("SetInputAudioSyncOffset", additionalFields);
        }

        //
        // Summary:
        //     Get the audio sync offset of the specified source
        //
        // Parameters:
        //   inputName:
        //     Source name
        //
        // Returns:
        //     Audio offset (in nanoseconds) of the specified source
        public int GetInputAudioSyncOffset(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return (int)SendRequest("GetInputAudioSyncOffset", additionalFields)["inputAudioSyncOffset"];
        }

        //
        // Summary:
        //     Removes a scene item from a scene. Scenes only.
        //
        // Parameters:
        //   sceneItemId:
        //     Scene item id
        //
        //   sceneName:
        //     Scene name from which to delete item
        public void RemoveSceneItem(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            SendRequest("RemoveSceneItem", additionalFields);
        }

        //
        // Summary:
        //     Sends CEA-608 caption text over the stream output. As of OBS Studio 23.1, captions
        //     are not yet available on Linux.
        //
        // Parameters:
        //   captionText:
        //     Captions text
        public void SendStreamCaption(string captionText)
        {
            JObject additionalFields = new JObject {
            {
                "captionText",
                (JToken)captionText
            } };
            SendRequest("SendStreamCaption", additionalFields);
        }

        //
        // Summary:
        //     Duplicates a scene item
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene that has the SceneItem
        //
        //   sceneItemId:
        //     Id of the Scene Item
        //
        //   destinationSceneName:
        //     Name of scene to add the new duplicated Scene Item. If not specified will assume
        //     sceneName
        public void DuplicateSceneItem(string sceneName, int sceneItemId, string destinationSceneName = null)
        {
            JObject jObject = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            if (!string.IsNullOrEmpty(destinationSceneName))
            {
                jObject.Add("destinationSceneName", (JToken)destinationSceneName);
            }

            SendRequest("DuplicateSceneItem", jObject);
        }

        //
        // Summary:
        //     Gets the names of all special inputs.
        //
        // Returns:
        //     Dictionary of special inputs.
        public Dictionary<string, string> GetSpecialInputs()
        {
            JObject jObject = SendRequest("GetSpecialInputs");
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            foreach (KeyValuePair<string, JToken?> item in jObject)
            {
                string key = item.Key;
                string value = (string?)item.Value;
                if (key != "requestType")
                {
                    dictionary.Add(key, value);
                }
            }

            return dictionary;
        }

        //
        // Summary:
        //     Sets the current stream service settings (stream destination). Note: Simple RTMP
        //     settings can be set with type `rtmp_custom` and the settings fields `server`
        //     and `key`.
        //
        // Parameters:
        //   service:
        //     Stream Service Type Name and Settings objects
        public void SetStreamServiceSettings(StreamingService service)
        {
            JObject additionalFields = new JObject
            {
                {
                    "streamServiceType",
                    (JToken)service.Type
                },
                {
                    "streamServiceSettings",
                    JToken.FromObject(service.Settings)
                }
            };
            SendRequest("SetStreamServiceSettings", additionalFields);
        }

        //
        // Summary:
        //     Gets the current stream service settings (stream destination).
        //
        // Returns:
        //     Stream service type and settings objects
        public StreamingService GetStreamServiceSettings()
        {
            return JsonConvert.DeserializeObject<StreamingService>(SendRequest("GetStreamServiceSettings").ToString());
        }

        //
        // Summary:
        //     Gets the audio monitor type of an input. The available audio monitor types are:
        //     - `OBS_MONITORING_TYPE_NONE` - `OBS_MONITORING_TYPE_MONITOR_ONLY` - `OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT`
        //
        // Parameters:
        //   inputName:
        //     Source name
        //
        // Returns:
        //     The monitor type in use
        public string GetInputAudioMonitorType(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return (string?)SendRequest("GetInputAudioMonitorType", additionalFields)["monitorType"];
        }

        //
        // Summary:
        //     Sets the audio monitor type of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to set the audio monitor type of
        //
        //   monitorType:
        //     Audio monitor type. See `GetInputAudioMonitorType for possible types.
        public void SetInputAudioMonitorType(string inputName, string monitorType)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "monitorType",
                    (JToken)monitorType
                }
            };
            SendRequest("SetInputAudioMonitorType", additionalFields);
        }

        //
        // Summary:
        //     Broadcasts a `CustomEvent` to all WebSocket clients. Receivers are clients which
        //     are identified and subscribed.
        //
        // Parameters:
        //   eventData:
        //     Data payload to emit to all receivers
        public void BroadcastCustomEvent(JObject eventData)
        {
            JObject additionalFields = new JObject { { "eventData", eventData } };
            SendRequest("BroadcastCustomEvent", additionalFields);
        }

        //
        // Summary:
        //     Sets the cursor position of a media input. This request does not perform bounds
        //     checking of the cursor position.
        //
        // Parameters:
        //   inputName:
        //     Name of the media input
        //
        //   mediaCursor:
        //     New cursor position to set (milliseconds).
        public void SetMediaInputCursor(string inputName, int mediaCursor)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "mediaCursor",
                    (JToken)mediaCursor
                }
            };
            SendRequest("SetMediaInputCursor", additionalFields);
        }

        //
        // Summary:
        //     Offsets the current cursor position of a media input by the specified value.
        //     This request does not perform bounds checking of the cursor position.
        //
        // Parameters:
        //   inputName:
        //     Name of the media input
        //
        //   mediaCursorOffset:
        //     Value to offset the current cursor position by (milliseconds +/-)
        public void OffsetMediaInputCursor(string inputName, int mediaCursorOffset)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "mediaCursorOffset",
                    (JToken)mediaCursorOffset
                }
            };
            SendRequest("OffsetMediaInputCursor", additionalFields);
        }

        //
        // Summary:
        //     Creates a new input, adding it as a scene item to the specified scene.
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to add the input to as a scene item
        //
        //   inputName:
        //     Name of the new input to created
        //
        //   inputKind:
        //     The kind of input to be created
        //
        //   inputSettings:
        //     Jobject holding the settings object to initialize the input with
        //
        //   sceneItemEnabled:
        //     Whether to set the created scene item to enabled or disabled
        //
        // Returns:
        //     ID of the SceneItem in the scene.
        public int CreateInput(string sceneName, string inputName, string inputKind, JObject inputSettings, bool? sceneItemEnabled)
        {
            JObject jObject = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "inputKind",
                    (JToken)inputKind
                }
            };
            if (inputSettings != null)
            {
                jObject.Add("inputSettings", inputSettings);
            }

            if (sceneItemEnabled.HasValue)
            {
                jObject.Add("sceneItemEnabled", (JToken)sceneItemEnabled.Value);
            }

            return (int)SendRequest("CreateInput", jObject)["sceneItemId"];
        }

        //
        // Summary:
        //     Gets the default settings for an input kind.
        //
        // Parameters:
        //   inputKind:
        //     Input kind to get the default settings for
        //
        // Returns:
        //     Object of default settings for the input kind
        public JObject GetInputDefaultSettings(string inputKind)
        {
            JObject additionalFields = new JObject {
            {
                "inputKind",
                (JToken)inputKind
            } };
            return (JObject)SendRequest("GetInputDefaultSettings", additionalFields)["defaultInputSettings"];
        }

        //
        // Summary:
        //     Gets a list of all scene items in a scene. Scenes only
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to get the items of
        //
        // Returns:
        //     Array of scene items in the scene
        public List<SceneItemDetails> GetSceneItemList(string sceneName)
        {
            JObject additionalFields = null;
            if (!string.IsNullOrEmpty(sceneName))
            {
                additionalFields = new JObject {
                {
                    "sceneName",
                    (JToken)sceneName
                } };
            }

            return SendRequest("GetSceneItemList", additionalFields)["sceneItems"].Select((JToken m) => new SceneItemDetails((JObject)m)).ToList();
        }

        //
        // Summary:
        //     Creates a new scene item using a source. Scenes only
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to create the new item in
        //
        //   sourceName:
        //     Name of the source to add to the scene
        //
        //   sceneItemEnabled:
        //     Enable state to apply to the scene item on creation
        //
        // Returns:
        //     Numeric ID of the scene item
        public int CreateSceneItem(string sceneName, string sourceName, bool sceneItemEnabled = true)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "sceneItemEnabled",
                    (JToken)sceneItemEnabled
                }
            };
            return (int)SendRequest("CreateSceneItem", additionalFields)["sceneItemId"];
        }

        //
        // Summary:
        //     Creates a new scene in OBS.
        //
        // Parameters:
        //   sceneName:
        //     Name for the new scene
        public void CreateScene(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            SendRequest("CreateScene", additionalFields);
        }

        //
        // Summary:
        //     Gets the enable state of all audio tracks of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input
        //
        // Returns:
        //     Object of audio tracks and associated enable states
        public SourceTracks GetInputAudioTracks(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return new SourceTracks(SendRequest("GetInputAudioTracks", additionalFields));
        }

        //
        // Summary:
        //     Sets the enable state of audio tracks of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input
        //
        //   inputAudioTracks:
        //     JObject holding track settings to apply
        public void SetInputAudioTracks(string inputName, JObject inputAudioTracks)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                { "inputAudioTracks", inputAudioTracks }
            };
            SendRequest("SetInputAudioTracks", additionalFields);
        }

        //
        // Summary:
        //     Sets the enable state of audio tracks of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input
        //
        //   inputAudioTracks:
        //     Track settings to apply
        public void SetInputAudioTracks(string inputName, SourceTracks inputAudioTracks)
        {
            SetInputAudioTracks(inputName, JObject.FromObject(inputAudioTracks));
        }

        //
        // Summary:
        //     Gets the active and show state of a source. **Compatible with inputs and scenes.**
        //
        // Parameters:
        //   sourceName:
        //     Name of the source to get the active state of
        //
        // Returns:
        //     Whether the source is showing in Program
        public SourceActiveInfo GetSourceActive(string sourceName)
        {
            JObject additionalFields = new JObject {
            {
                "sourceName",
                (JToken)sourceName
            } };
            return new SourceActiveInfo(SendRequest("GetSourceActive", additionalFields));
        }

        //
        // Summary:
        //     Gets the status of the virtualcam output.
        //
        // Returns:
        //     An OBSWebsocketDotNet.Types.VirtualCamStatus object describing the current virtual
        //     camera state
        public VirtualCamStatus GetVirtualCamStatus()
        {
            return new VirtualCamStatus(SendRequest("GetVirtualCamStatus"));
        }

        //
        // Summary:
        //     Starts the virtualcam output.
        public void StartVirtualCam()
        {
            SendRequest("StartVirtualCam");
        }

        //
        // Summary:
        //     Stops the virtualcam output.
        public void StopVirtualCam()
        {
            SendRequest("StopVirtualCam");
        }

        //
        // Summary:
        //     Toggles the state of the virtualcam output.
        //
        // Returns:
        //     Whether the output is active
        public VirtualCamStatus ToggleVirtualCam()
        {
            return new VirtualCamStatus(SendRequest("ToggleVirtualCam"));
        }

        //
        // Summary:
        //     Gets the value of a \"slot\" from the selected persistent data realm.
        //
        // Parameters:
        //   realm:
        //     The data realm to select. `OBS_WEBSOCKET_DATA_REALM_GLOBAL` or `OBS_WEBSOCKET_DATA_REALM_PROFILE`
        //
        //   slotName:
        //     The name of the slot to retrieve data from
        //
        // Returns:
        //     Value associated with the slot. `null` if not set
        public JObject GetPersistentData(string realm, string slotName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "realm",
                    (JToken)realm
                },
                {
                    "slotName",
                    (JToken)slotName
                }
            };
            return SendRequest("GetPersistentData", additionalFields);
        }

        //
        // Summary:
        //     Sets the value of a \"slot\" from the selected persistent data realm.
        //
        // Parameters:
        //   realm:
        //     The data realm to select. `OBS_WEBSOCKET_DATA_REALM_GLOBAL` or `OBS_WEBSOCKET_DATA_REALM_PROFILE`
        //
        //   slotName:
        //     The name of the slot to retrieve data from
        //
        //   slotValue:
        //     The value to apply to the slot
        public void SetPersistentData(string realm, string slotName, JObject slotValue)
        {
            JObject additionalFields = new JObject
            {
                {
                    "realm",
                    (JToken)realm
                },
                {
                    "slotName",
                    (JToken)slotName
                },
                { "slotValue", slotValue }
            };
            SendRequest("SetPersistentData", additionalFields);
        }

        //
        // Summary:
        //     Creates a new scene collection, switching to it in the process.\n\nNote: This
        //     will block until the collection has finished changing.
        //
        // Parameters:
        //   sceneCollectionName:
        //     Name for the new scene collection
        public void CreateSceneCollection(string sceneCollectionName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneCollectionName",
                (JToken)sceneCollectionName
            } };
            SendRequest("CreateSceneCollection", additionalFields);
        }

        //
        // Summary:
        //     Creates a new profile, switching to it in the process
        //
        // Parameters:
        //   profileName:
        //     Name for the new profile
        public void CreateProfile(string profileName)
        {
            JObject additionalFields = new JObject {
            {
                "profileName",
                (JToken)profileName
            } };
            SendRequest("CreateProfile", additionalFields);
        }

        //
        // Summary:
        //     Removes a profile. If the current profile is chosen, it will change to a different
        //     profile first.
        //
        // Parameters:
        //   profileName:
        //     Name of the profile to remove
        public void RemoveProfile(string profileName)
        {
            JObject additionalFields = new JObject {
            {
                "profileName",
                (JToken)profileName
            } };
            SendRequest("RemoveProfile", additionalFields);
        }

        //
        // Summary:
        //     Gets a parameter from the current profile's configuration.
        //
        // Parameters:
        //   parameterCategory:
        //     Category of the parameter to get
        //
        //   parameterName:
        //     Name of the parameter to get
        public JObject GetProfileParameter(string parameterCategory, string parameterName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "parameterCategory",
                    (JToken)parameterCategory
                },
                {
                    "parameterName",
                    (JToken)parameterName
                }
            };
            return SendRequest("GetProfileParameter", additionalFields);
        }

        //
        // Summary:
        //     Sets the value of a parameter in the current profile's configuration.
        //
        // Parameters:
        //   parameterCategory:
        //     Category of the parameter to set
        //
        //   parameterName:
        //     Name of the parameter to set
        //
        //   parameterValue:
        //     Value of the parameter to set. Use `null` to delete
        public void SetProfileParameter(string parameterCategory, string parameterName, string parameterValue)
        {
            JObject additionalFields = new JObject
            {
                {
                    "parameterCategory",
                    (JToken)parameterCategory
                },
                {
                    "parameterName",
                    (JToken)parameterName
                },
                {
                    "parameterValue",
                    (JToken)parameterValue
                }
            };
            SendRequest("SetProfileParameter", additionalFields);
        }

        //
        // Summary:
        //     Sets the current video settings. Note: Fields must be specified in pairs. For
        //     example, you cannot set only `baseWidth` without needing to specify `baseHeight`.
        //
        // Parameters:
        //   obsVideoSettings:
        //     Object containing video settings
        public void SetVideoSettings(ObsVideoSettings obsVideoSettings)
        {
            SendRequest("SetVideoSettings", JObject.FromObject(obsVideoSettings));
        }

        //
        // Summary:
        //     Gets the default settings for a filter kind.
        //
        // Parameters:
        //   filterKind:
        //     Filter kind to get the default settings for
        //
        // Returns:
        //     Object of default settings for the filter kind
        public JObject GetSourceFilterDefaultSettings(string filterKind)
        {
            JObject additionalFields = new JObject {
            {
                "filterKind",
                (JToken)filterKind
            } };
            return SendRequest("GetSourceFilterDefaultSettings", additionalFields);
        }

        //
        // Summary:
        //     Sets the name of a source filter (rename).
        //
        // Parameters:
        //   sourceName:
        //     Name of the source the filter is on
        //
        //   filterName:
        //     Current name of the filter
        //
        //   newFilterName:
        //     New name for the filter
        public void SetSourceFilterName(string sourceName, string filterName, string newFilterName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                },
                {
                    "newFilterName",
                    (JToken)newFilterName
                }
            };
            SendRequest("SetSourceFilterName", additionalFields);
        }

        //
        // Summary:
        //     Sets the index position of a filter on a source.
        //
        // Parameters:
        //   sourceName:
        //     Name of the source the filter is on
        //
        //   filterName:
        //     Name of the filter
        //
        //   filterIndex:
        //     New index position of the filter
        public void SetSourceFilterIndex(string sourceName, string filterName, int filterIndex)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "filterName",
                    (JToken)filterName
                },
                {
                    "filterIndex",
                    (JToken)filterIndex
                }
            };
            SendRequest("SetSourceFilterIndex", additionalFields);
        }

        //
        // Summary:
        //     Gets data about the current plugin and RPC version.
        //
        // Returns:
        //     Version info in an OBSWebsocketDotNet.Types.ObsVersion object
        public ObsVersion GetVersion()
        {
            return new ObsVersion(SendRequest("GetVersion"));
        }

        //
        // Summary:
        //     Call a request registered to a vendor. A vendor is a unique name registered by
        //     a third-party plugin or script, which allows for custom requests and events to
        //     be added to obs-websocket. If a plugin or script implements vendor requests or
        //     events, documentation is expected to be provided with them.
        //
        // Parameters:
        //   vendorName:
        //     Name of the vendor to use
        //
        //   requestType:
        //     The request type to call
        //
        //   requestData:
        //     Object containing appropriate request data
        //
        // Returns:
        //     Object containing appropriate response data. {} if request does not provide any
        //     response data
        public JObject CallVendorRequest(string vendorName, string requestType, JObject requestData = null)
        {
            JObject additionalFields = new JObject
            {
                {
                    "vendorName",
                    (JToken)vendorName
                },
                {
                    "requestType",
                    (JToken)requestType
                },
                { "requestData", requestData }
            };
            return SendRequest("CallVendorRequest", additionalFields);
        }

        //
        // Summary:
        //     Gets an array of all hotkey names in OBS
        //
        // Returns:
        //     Array of hotkey names
        public List<string> GetHotkeyList()
        {
            return JsonConvert.DeserializeObject<List<string>>(SendRequest("GetHotkeyList")["hotkeys"]!.ToString());
        }

        //
        // Summary:
        //     Sleeps for a time duration or number of frames. Only available in request batches
        //     with types `SERIAL_REALTIME` or `SERIAL_FRAME`.
        //
        // Parameters:
        //   sleepMillis:
        //     Number of milliseconds to sleep for (if `SERIAL_REALTIME` mode)
        //
        //   sleepFrames:
        //     Number of frames to sleep for (if `SERIAL_FRAME` mode)
        public void Sleep(int sleepMillis, int sleepFrames)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sleepMillis",
                    (JToken)sleepMillis
                },
                {
                    "sleepFrames",
                    (JToken)sleepFrames
                }
            };
            SendRequest("Sleep", additionalFields);
        }

        //
        // Summary:
        //     Gets an array of all inputs in OBS.
        //
        // Parameters:
        //   inputKind:
        //     Restrict the array to only inputs of the specified kind
        //
        // Returns:
        //     List of Inputs in OBS
        public List<InputBasicInfo> GetInputList(string inputKind = null)
        {
            JObject additionalFields = new JObject {
            {
                "inputKind",
                (JToken)inputKind
            } };
            JObject obj = ((inputKind == null) ? SendRequest("GetInputList") : SendRequest("GetInputList", additionalFields));
            List<InputBasicInfo> list = new List<InputBasicInfo>();
            foreach (JToken item in (IEnumerable<JToken>)(obj["inputs"]!))
            {
                list.Add(new InputBasicInfo(item as JObject));
            }

            return list;
        }

        //
        // Summary:
        //     Gets an array of all available input kinds in OBS.
        //
        // Parameters:
        //   unversioned:
        //     True == Return all kinds as unversioned, False == Return with version suffixes
        //     (if available)
        //
        // Returns:
        //     Array of input kinds
        public List<string> GetInputKindList(bool unversioned = false)
        {
            JObject additionalFields = new JObject {
            {
                "unversioned",
                (JToken)unversioned
            } };
            return JsonConvert.DeserializeObject<List<string>>(((!unversioned) ? SendRequest("GetInputKindList") : SendRequest("GetInputKindList", additionalFields))["inputKinds"]!.ToString());
        }

        //
        // Summary:
        //     Removes an existing input. Note: Will immediately remove all associated scene
        //     items.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to remove
        public void RemoveInput(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            SendRequest("RemoveInput", additionalFields);
        }

        //
        // Summary:
        //     Sets the name of an input (rename).
        //
        // Parameters:
        //   inputName:
        //     Current input name
        //
        //   newInputName:
        //     New name for the input
        public void SetInputName(string inputName, string newInputName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "newInputName",
                    (JToken)newInputName
                }
            };
            SendRequest("SetInputName", additionalFields);
        }

        //
        // Summary:
        //     Gets the settings of an input. Note: Does not include defaults. To create the
        //     entire settings object, overlay `inputSettings` over the `defaultInputSettings`
        //     provided by `GetInputDefaultSettings`.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to get the settings of
        //
        // Returns:
        //     New populated InputSettings object
        public InputSettings GetInputSettings(string inputName)
        {
            JObject jObject = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            JObject jObject2 = SendRequest("GetInputSettings", jObject);
            jObject2.Merge(jObject);
            return new InputSettings(jObject2);
        }

        //
        // Summary:
        //     Sets the settings of an input.
        //
        // Parameters:
        //   inputSettings:
        //     Object of settings to apply
        //
        //   overlay:
        //     True == apply the settings on top of existing ones, False == reset the input
        //     to its defaults, then apply settings.
        public void SetInputSettings(InputSettings inputSettings, bool overlay = true)
        {
            SetInputSettings(inputSettings.InputName, inputSettings.Settings, overlay);
        }

        //
        // Summary:
        //     Sets the settings of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to set the settings of
        //
        //   inputSettings:
        //     Object of settings to apply
        //
        //   overlay:
        //     True == apply the settings on top of existing ones, False == reset the input
        //     to its defaults, then apply settings.
        public void SetInputSettings(string inputName, JObject inputSettings, bool overlay = true)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                { "inputSettings", inputSettings },
                {
                    "overlay",
                    (JToken)overlay
                }
            };
            SendRequest("SetInputSettings", additionalFields);
        }

        //
        // Summary:
        //     Gets the audio balance of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to get the audio balance of
        //
        // Returns:
        //     Audio balance value from 0.0-1.0
        public double GetInputAudioBalance(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return (double)SendRequest("GetInputAudioBalance", additionalFields)["inputAudioBalance"];
        }

        //
        // Summary:
        //     Sets the audio balance of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to set the audio balance of
        //
        //   inputAudioBalance:
        //     New audio balance value
        public void SetInputAudioBalance(string inputName, double inputAudioBalance)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "inputAudioBalance",
                    (JToken)inputAudioBalance
                }
            };
            SendRequest("SetInputAudioBalance", additionalFields);
        }

        //
        // Summary:
        //     Gets the items of a list property from an input's properties. Note: Use this
        //     in cases where an input provides a dynamic, selectable list of items. For example,
        //     display capture, where it provides a list of available displays.
        //
        // Parameters:
        //   inputName:
        //     Name of the input
        //
        //   propertyName:
        //     Name of the list property to get the items of
        //
        // Returns:
        //     Array of items in the list property
        public List<JObject> GetInputPropertiesListPropertyItems(string inputName, string propertyName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "propertyName",
                    (JToken)propertyName
                }
            };
            return SendRequest("GetInputPropertiesListPropertyItems", additionalFields)["propertyItems"].Value<List<JObject>>();
        }

        //
        // Summary:
        //     Presses a button in the properties of an input. Note: Use this in cases where
        //     there is a button in the properties of an input that cannot be accessed in any
        //     other way. For example, browser sources, where there is a refresh button.
        //
        // Parameters:
        //   inputName:
        //     Name of the input
        //
        //   propertyName:
        //     Name of the button property to press
        public void PressInputPropertiesButton(string inputName, string propertyName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "propertyName",
                    (JToken)propertyName
                }
            };
            SendRequest("PressInputPropertiesButton", additionalFields);
        }

        //
        // Summary:
        //     Gets the status of a media input.\n\nMedia States: - `OBS_MEDIA_STATE_NONE` -
        //     `OBS_MEDIA_STATE_PLAYING` - `OBS_MEDIA_STATE_OPENING` - `OBS_MEDIA_STATE_BUFFERING`
        //     - `OBS_MEDIA_STATE_PAUSED` - `OBS_MEDIA_STATE_STOPPED` - `OBS_MEDIA_STATE_ENDED`
        //     - `OBS_MEDIA_STATE_ERROR`
        //
        // Parameters:
        //   inputName:
        //     Name of the media input
        //
        // Returns:
        //     Object containing string mediaState, int mediaDuration, int mediaCursor properties
        public MediaInputStatus GetMediaInputStatus(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            return new MediaInputStatus(SendRequest("GetMediaInputStatus", additionalFields));
        }

        //
        // Summary:
        //     Triggers an action on a media input.
        //
        // Parameters:
        //   inputName:
        //     Name of the media input
        //
        //   mediaAction:
        //     Identifier of the `ObsMediaInputAction` enum
        public void TriggerMediaInputAction(string inputName, string mediaAction)
        {
            JObject additionalFields = new JObject
            {
                {
                    "inputName",
                    (JToken)inputName
                },
                {
                    "mediaAction",
                    (JToken)mediaAction
                }
            };
            SendRequest("TriggerMediaInputAction", additionalFields);
        }

        //
        // Summary:
        //     Gets the filename of the last replay buffer save file.
        //
        // Returns:
        //     File path of last replay
        public string GetLastReplayBufferReplay()
        {
            return (string?)SendRequest("GetLastReplayBufferReplay")["savedReplayPath"];
        }

        //
        // Summary:
        //     Toggles pause on the record output.
        public void ToggleRecordPause()
        {
            SendRequest("ToggleRecordPause");
        }

        //
        // Summary:
        //     Currently BROKEN in obs-websocket/obs-studio Basically GetSceneItemList, but
        //     for groups. Using groups at all in OBS is discouraged, as they are very broken
        //     under the hood. Groups only
        //
        // Parameters:
        //   sceneName:
        //     Name of the group to get the items of
        //
        // Returns:
        //     Array of scene items in the group
        public List<JObject> GetGroupSceneItemList(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            return JsonConvert.DeserializeObject<List<JObject>>((string?)SendRequest("GetGroupSceneItemList", additionalFields)["sceneItems"]);
        }

        //
        // Summary:
        //     Searches a scene for a source, and returns its id.\n\nScenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene or group to search in
        //
        //   sourceName:
        //     Name of the source to find
        //
        //   searchOffset:
        //     Number of matches to skip during search. >= 0 means first forward. -1 means last
        //     (top) item
        //
        // Returns:
        //     Numeric ID of the scene item
        public int GetSceneItemId(string sceneName, string sourceName, int searchOffset)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "searchOffset",
                    (JToken)searchOffset
                }
            };
            return (int)SendRequest("GetSceneItemId", additionalFields)["sceneItemId"];
        }

        //
        // Summary:
        //     Gets the transform and crop info of a scene item. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Object containing scene item transform info
        public SceneItemTransformInfo GetSceneItemTransform(string sceneName, int sceneItemId)
        {
            return JsonConvert.DeserializeObject<SceneItemTransformInfo>(GetSceneItemTransformRaw(sceneName, sceneItemId)["sceneItemTransform"]!.ToString());
        }

        //
        // Summary:
        //     Gets the JObject of transform settings for a scene item. Use this one you don't
        //     want it populated with default values. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Object containing scene item transform info
        public JObject GetSceneItemTransformRaw(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            return SendRequest("GetSceneItemTransform", additionalFields);
        }

        //
        // Summary:
        //     Gets the enable state of a scene item. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Whether the scene item is enabled. `true` for enabled, `false` for disabled
        public bool GetSceneItemEnabled(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            return (bool)SendRequest("GetSceneItemEnabled", additionalFields)["sceneItemEnabled"];
        }

        //
        // Summary:
        //     Gets the enable state of a scene item. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        //   sceneItemEnabled:
        //     New enable state of the scene item
        public void SetSceneItemEnabled(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                },
                {
                    "sceneItemEnabled",
                    (JToken)sceneItemEnabled
                }
            };
            SendRequest("SetSceneItemEnabled", additionalFields);
        }

        //
        // Summary:
        //     Gets the lock state of a scene item. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Whether the scene item is locked. `true` for locked, `false` for unlocked
        public bool GetSceneItemLocked(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            return (bool)SendRequest("GetSceneItemLocked", additionalFields)["sceneItemLocked"];
        }

        //
        // Summary:
        //     Sets the lock state of a scene item. Scenes and Group
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        //   sceneItemLocked:
        //     New lock state of the scene item
        public void SetSceneItemLocked(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                },
                {
                    "sceneItemLocked",
                    (JToken)sceneItemLocked
                }
            };
            SendRequest("SetSceneItemLocked", additionalFields);
        }

        //
        // Summary:
        //     Gets the index position of a scene item in a scene. An index of 0 is at the bottom
        //     of the source list in the UI. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Index position of the scene item
        public int GetSceneItemIndex(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            return (int)SendRequest("GetSceneItemIndex", additionalFields)["sceneItemIndex"];
        }

        //
        // Summary:
        //     Sets the index position of a scene item in a scene. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        //   sceneItemIndex:
        //     New index position of the scene item
        public void SetSceneItemIndex(string sceneName, int sceneItemId, int sceneItemIndex)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                },
                {
                    "sceneItemIndex",
                    (JToken)sceneItemIndex
                }
            };
            SendRequest("SetSceneItemIndex", additionalFields);
        }

        //
        // Summary:
        //     Gets the blend mode of a scene item. Blend modes: - `OBS_BLEND_NORMAL` - `OBS_BLEND_ADDITIVE`
        //     - `OBS_BLEND_SUBTRACT` - `OBS_BLEND_SCREEN` - `OBS_BLEND_MULTIPLY` - `OBS_BLEND_LIGHTEN`
        //     - `OBS_BLEND_DARKEN` Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene the item is in
        //
        //   sceneItemId:
        //     Numeric ID of the scene item
        //
        // Returns:
        //     Current blend mode
        public string GetSceneItemBlendMode(string sceneName, int sceneItemId)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                }
            };
            return (string?)SendRequest("GetSceneItemBlendMode", additionalFields)["sceneItemBlendMode"];
        }

        //
        // Summary:
        //     Sets the blend mode of a scene item. Scenes and Groups
        //
        // Parameters:
        //   sceneName:
        //
        //   sceneItemId:
        //
        //   sceneItemBlendMode:
        public void SetSceneItemBlendMode(string sceneName, int sceneItemId, string sceneItemBlendMode)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "sceneItemId",
                    (JToken)sceneItemId
                },
                {
                    "sceneItemBlendMode",
                    (JToken)sceneItemBlendMode
                }
            };
            SendRequest("SetSceneItemBlendMode", additionalFields);
        }

        //
        // Summary:
        //     Gets an array of all groups in OBS. Groups in OBS are actually scenes, but renamed
        //     and modified. In obs-websocket, we treat them as scenes where we can.
        //
        // Returns:
        //     Array of group names
        public List<string> GetGroupList()
        {
            return JsonConvert.DeserializeObject<List<string>>(SendRequest("GetGroupList")["groups"]!.ToString());
        }

        //
        // Summary:
        //     Removes a scene from OBS.
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to remove
        public void RemoveScene(string sceneName)
        {
            JObject additionalFields = new JObject {
            {
                "sceneName",
                (JToken)sceneName
            } };
            SendRequest("RemoveScene", additionalFields);
        }

        //
        // Summary:
        //     Sets the name of a scene (rename).
        //
        // Parameters:
        //   sceneName:
        //     Name of the scene to be renamed
        //
        //   newSceneName:
        //     New name for the scene
        public void SetSceneName(string sceneName, string newSceneName)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sceneName",
                    (JToken)sceneName
                },
                {
                    "newSceneName",
                    (JToken)newSceneName
                }
            };
            SendRequest("SetSceneName", additionalFields);
        }

        //
        // Summary:
        //     Gets a Base64-encoded screenshot of a source. The `imageWidth` and `imageHeight`
        //     parameters are treated as \"scale to inner\", meaning the smallest ratio will
        //     be used and the aspect ratio of the original resolution is kept. If `imageWidth`
        //     and `imageHeight` are not specified, the compressed image will use the full resolution
        //     of the source. **Compatible with inputs and scenes.**
        //
        // Parameters:
        //   sourceName:
        //     Name of the source to take a screenshot of
        //
        //   imageFormat:
        //     Image compression format to use. Use `GetVersion` to get compatible image formats
        //
        //   imageWidth:
        //     Width to scale the screenshot to
        //
        //   imageHeight:
        //     Height to scale the screenshot to
        //
        //   imageCompressionQuality:
        //     Compression quality to use. 0 for high compression, 100 for uncompressed. -1
        //     to use \"default\" (whatever that means, idk)
        //
        // Returns:
        //     Base64-encoded screenshot
        public string GetSourceScreenshot(string sourceName, string imageFormat, int imageWidth = -1, int imageHeight = -1, int imageCompressionQuality = -1)
        {
            JObject jObject = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "imageFormat",
                    (JToken)imageFormat
                }
            };
            if (imageWidth > -1)
            {
                jObject.Add("imageWidth", (JToken)imageWidth);
            }

            if (imageHeight > -1)
            {
                jObject.Add("imageHeight", (JToken)imageHeight);
            }

            if (imageCompressionQuality > -1)
            {
                jObject.Add("imageCompressionQuality", (JToken)imageCompressionQuality);
            }

            return (string?)SendRequest("GetSourceScreenshot", jObject)["imageData"];
        }

        //
        // Summary:
        //     Gets an array of all available transition kinds. Similar to `GetInputKindList`
        //
        // Returns:
        //     Array of transition kinds
        public List<string> GetTransitionKindList()
        {
            return JsonConvert.DeserializeObject<List<string>>(SendRequest("GetTransitionKindList")["transitionKinds"]!.ToString());
        }

        //
        // Summary:
        //     Gets the cursor position of the current scene transition. Note: `transitionCursor`
        //     will return 1.0 when the transition is inactive.
        //
        // Returns:
        //     Cursor position, between 0.0 and 1.0
        public double GetCurrentSceneTransitionCursor()
        {
            return (double)SendRequest("GetCurrentSceneTransitionCursor")["transitionCursor"];
        }

        //
        // Summary:
        //     Opens the properties dialog of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to open the dialog of
        public void OpenInputPropertiesDialog(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            SendRequest("OpenInputPropertiesDialog", additionalFields);
        }

        //
        // Summary:
        //     Opens the filters dialog of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to open the dialog of
        public void OpenInputFiltersDialog(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            SendRequest("OpenInputFiltersDialog", additionalFields);
        }

        //
        // Summary:
        //     Opens the interact dialog of an input.
        //
        // Parameters:
        //   inputName:
        //     Name of the input to open the dialog of
        public void OpenInputInteractDialog(string inputName)
        {
            JObject additionalFields = new JObject {
            {
                "inputName",
                (JToken)inputName
            } };
            SendRequest("OpenInputInteractDialog", additionalFields);
        }

        //
        // Summary:
        //     Gets a list of connected monitors and information about them.
        //
        // Returns:
        //     a list of detected monitors with some information
        public List<Monitor> GetMonitorList()
        {
            JObject jObject = SendRequest("GetMonitorList");
            List<Monitor> list = new List<Monitor>();
            foreach (JToken item in (IEnumerable<JToken>)(jObject["monitors"]!))
            {
                list.Add(new Monitor((JObject)item));
            }

            return list;
        }

        //
        // Summary:
        //     Opens a projector for a source. Note: This request serves to provide feature
        //     parity with 4.x. It is very likely to be changed/deprecated in a future release.
        //
        // Parameters:
        //   sourceName:
        //     Name of the source to open a projector for
        //
        //   projectorGeometry:
        //     Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually
        //     exclusive with monitorIndex
        //
        //   monitorIndex:
        //     Monitor index, use GetMonitorList to obtain index. -1 to open in windowed mode
        public void OpenSourceProjector(string sourceName, string projectorGeometry, int monitorIndex = -1)
        {
            JObject additionalFields = new JObject
            {
                {
                    "sourceName",
                    (JToken)sourceName
                },
                {
                    "projectorGeometry",
                    (JToken)projectorGeometry
                },
                {
                    "monitorIndex",
                    (JToken)monitorIndex
                }
            };
            SendRequest("OpenSourceProjector", additionalFields);
        }

        //
        // Summary:
        //     Opens a projector for a specific output video mix. Note: This request serves
        //     to provide feature parity with 4.x. It is very likely to be changed/deprecated
        //     in a future release.
        //
        // Parameters:
        //   videoMixType:
        //     Mix types: OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM,
        //     OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW
        //
        //   projectorGeometry:
        //     Size/Position data for a windowed projector, in Qt Base64 encoded format. Mutually
        //     exclusive with monitorIndex
        //
        //   monitorIndex:
        //     Monitor index, use GetMonitorList to obtain index. -1 to open in windowed mode
        public void OpenVideoMixProjector(string videoMixType, string projectorGeometry, int monitorIndex = -1)
        {
            JObject additionalFields = new JObject
            {
                {
                    "videoMixType",
                    (JToken)videoMixType
                },
                {
                    "projectorGeometry",
                    (JToken)projectorGeometry
                },
                {
                    "monitorIndex",
                    (JToken)monitorIndex
                }
            };
            SendRequest("OpenVideoMixProjector", additionalFields);
        }
    }
}