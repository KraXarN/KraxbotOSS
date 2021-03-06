﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using SteamKit2;
using SteamKit2.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using Cleverbot.Net;
using System.Net;
using DSharpPlus.Exceptions;

namespace KraxbotOSS
{
    public partial class Form1 : Form
    {
        // Some variables
	    private readonly string version;
        public  static   string ConfigPath;
        public  static   Config config;
		
	    private readonly Dictionary<SteamID, Settings>         chatrooms;
		private readonly Dictionary<SteamID, CleverbotSession> cleverbotSessions;

	    private bool running;

		// Steam variables
		private readonly SteamClient  client;
	    private readonly SteamUser    user;
        private readonly SteamFriends friends;

		// Discord
	    private DiscordBot discordBot;

	    public string DiscordStatus
	    {
		    get => lDiscordStatus.Text.Substring(9);
		    set
		    {
			    if (lDiscordStatus.InvokeRequired)
			    {
				    lDiscordStatus.Invoke(new MethodInvoker(delegate
				    {
					    lDiscordStatus.Text = $@"Discord: {value}";
					}));
			    }
				else
					lDiscordStatus.Text = $@"Discord: {value}";
		    }
	    }

		public Form1()
        {
	        InitializeComponent();

			// Vars
	        version   = "1.1.3";
	        chatrooms = new Dictionary<SteamID, Settings>();
	        config    = new Config();
	        cleverbotSessions = new Dictionary<SteamID, CleverbotSession>();

			// Crash handler
			Application.ThreadException                += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Check config dir
            ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrowGames", "KraxbotOSS");
            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(Path.Combine(ConfigPath, "chatrooms"));
                Directory.CreateDirectory(Path.Combine(ConfigPath, "sentryhash"));
            }

            // Check and load config
            if (File.Exists(Path.Combine(ConfigPath, "settings.json")))
            {
                dynamic json = JsonConvert.DeserializeObject(File.ReadAllText(Path.Combine(ConfigPath, "settings.json")));
                config.Updates              = json.Updates;
                config.FriendRequest        = json.FriendRequest;
                config.ChatRequest          = json.ChatRequest;
                config.LoginAs              = json.LoginAs;
                config.Superadmin           = json.Superadmin;
                config.Chatrooms            = json.Chatrooms;
                config.API_Steam            = json.API.SteamWeb;
                config.API_Google           = json.API.Google;
                config.API_OpenWeather      = json.API.OpenWeatherMap;
                config.API_CleverbotIO      = json.API.CleverbotIO;
                config.GamePlayed_ID        = json.GamePlayed.ID;
                config.GamePlayed_ExtraInfo = json.GamePlayed.ExtraInfo;

				// Discord settings
				if (json.Discord != null)
				{
					config.Discord_Enabled      = json.Discord.Enabled;
					config.Discord_Token        = json.Discord.Token;
					config.Discord_Admin        = json.Discord.Admin;
					config.Discord_StateChanges = json.Discord.StateChanges;
					config.Discord_Channel      = json.Discord.Channel;
					config.Discord_Messages     = json.Discord.Messages;
					config.Discord_Steam        = json.Discord.Steam;
				}
            }

            // Check for updates
            if (config.Updates != "None")
                Task.Run(() => CheckForUpdates());

            // Welcome the user :)
            log.AppendText("Welcome to KraxbotOSS " + version);

            // Steam stuff
            // Create client and callback manager to route callbacks to functions
            client      = new SteamClient();
            var manager = new CallbackManager(client);
            // Get the user handler, which is used for logging in
            user    = client.GetHandler<SteamUser>();
            friends = client.GetHandler<SteamFriends>();
            // Register more callbacks
            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);            // When we connect
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);      // When we disconnect
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);                // When we finished logging in
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);              // When we got logged off
            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);          // We finished logging in
            manager.Subscribe<SteamFriends.FriendAddedCallback>(OnFriendAdded);       // Someone added us
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsg);           // We got a PM
            manager.Subscribe<SteamFriends.ChatMsgCallback>(OnChatMsg);               // Someone sent a chat message
            manager.Subscribe<SteamFriends.ChatInviteCallback>(OnChatInvite);         // We got invited to a chat
            manager.Subscribe<SteamFriends.ChatEnterCallback>(OnChatEnter);           // We entered a chat
            manager.Subscribe<SteamFriends.ChatMemberInfoCallback>(OnChatMemberInfo); // A user has left or entered a chat
            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);    // We logged in and can store it
            manager.Subscribe<SteamUser.LoginKeyCallback>(OnLoginKey);                // When we want to save our password
            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);       // When we get our friends list

            // Tell the main Steam loop we are running
            running = true;

            // Connect
            log.AppendText("\nConnecting to Steam... ");
            client.Connect();

			// Discord
			// We connect after Steam connects
	        if (config.Discord_Enabled)
		        DiscordStatus = "Waiting";

            // Run main loop in a seperate thread
            Task.Run(() => { while (running) { manager.RunWaitCallbacks(TimeSpan.FromSeconds(1)); } });
        }

        #region Classes

        // Chat settings
        public class Settings
        {
            public readonly List<UserInfo> Users    = new List<UserInfo>();
            public readonly List<string>   SetRules = new List<string>();

            public int     Version     = 1;
            public string  ChatName    = "NoName";
            public string  InvitedName = "NoName";
            public SteamID ChatID;
            public SteamID InvitedID;

            public string  Spam       = "Kick";
            public string  WelcomeMsg = "Welcome";
            public string  WelcomeEnd = "!";
            public string  DCKick     = "Kick";
            public SteamID LastPoke;

            public bool Cleverbot = false;
            public bool Translate = false;
            public bool Commands  = true;

            public bool Welcome     = true;
            public bool Games       = true;
            public bool Define      = true;
            public bool Wiki        = true;
            public bool Search      = true;
            public bool Weather     = true;
            public bool Store       = true;
            public bool Responses   = true;
            public bool Links       = true;
            public bool Rules       = true;
            public bool Poke        = true;
            public bool AllStates   = false;
            public bool AllPoke     = false;
            public bool AutoWelcome = false;

            public int DCLimit      = 5;
            public int DelayRandom  = 120;
            public int DelayDefine  = 300;
            public int DelayGames   = 120;
            public int DelayRecents = 120;
            public int DelaySearch  = 120;
            public int DelayYT      = 120;

            public int DCKickLimit = 3;
            public int DCBanLimit  = 5;

            public int TimeoutRandom  = 0;
            public int TimeoutDefine  = 0;
            public int TimeoutGames   = 0;
            public int TimeoutRecents = 0;
            public int TimeoutSearch  = 0;
            public int TimeoutYT      = 0;

            public bool   CustomEnabled  = false;
            public bool   CustomModOnly  = false;
            public int    CustomDelay    = 5;
            public string CustomCommand  = "!custom";
            public string CustomResponse = "Custom response";

            public string  AutokickMode = "None";
            public SteamID AutokickUser;

            // We may use these later
            public int SpamSameMessageTimeout = 3000; // Max time when saying the same message
            public int SpamTimeout            = 500;  // Max time when saying different messages
            public int SpamMessageLength      = 400;  // Max length of message
        }

        // User info
        public class UserInfo
        {
            public SteamID         SteamID;
            public EClanPermission Rank;
            public EChatPermission Permission;
            public long            LastTime = 0;
            public string          LastMessage;
            // We may use these later
            public int Disconnect = 0;
            public int Warning    = 0;
        }

        // Settings
        public class Config
        {
            // For config, we just set default settings at first
            internal string Updates        = "All";
            internal string FriendRequest  = "AcceptAll";
            internal string ChatRequest    = "AcceptAll";
            internal EPersonaState LoginAs = EPersonaState.Online;
            internal uint Superadmin;
            internal JArray Chatrooms      = new JArray();

            internal string API_Steam;
            internal string API_Google;
            internal string API_OpenWeather;
            internal string API_CleverbotIO;

            internal ulong  GamePlayed_ID;
            internal string GamePlayed_ExtraInfo;

			// 1.1: Discord Settings
			internal bool   Discord_Enabled = false;
			internal string Discord_Token;
			internal string Discord_Admin;
			internal bool   Discord_AllowCommands = false;
			internal bool   Discord_StateChanges  = true;
	        internal ulong  Discord_Channel;
	        internal string Discord_Messages      = "Both";
	        internal uint   Discord_Steam;
        }

		#endregion

		#region Steam callbacks

	    private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            // TODO: Handle this better or something
            if (callback.Result != EResult.OK)
            {
                MessageBox.Show("Error connecting to Steam, try again later.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                running = false;
                Close();
                return;
            }
            Log("Ok");
            if (File.Exists(Path.Combine(ConfigPath, "user")))
            {
                Invoke((MethodInvoker)delegate
                {
                    lSteamStatus.Text = "Steam: Connected";
                });
                var user = File.ReadAllLines(Path.Combine(ConfigPath, "user"));
                FormLogin.Username = user[0];
                Login(user[0]);
            }
            else
            {
                Invoke((MethodInvoker)delegate
                {
                    btnLogin.Enabled = true;
                    lSteamStatus.Text = "Steam: Connected";
                });
            }
		}

	    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            // TODO: Only show the message once
            Log("\nDisconnected, attempting to reconnect... ");
            Invoke((MethodInvoker)delegate
            {
                lSteamStatus.Text = "Steam: Reconnecting";
            });
            client.Connect();
        }

	    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
	            switch (callback.Result)
	            {
		            case EResult.AccountLogonDenied:
			            Invoke((MethodInvoker)delegate
			            {
				            var login = new FormLogin(this, "NeedGuard");
				            // If we use ShowDialog here, we get disconencted and can't login
				            login.Show(this);
			            });
			            break;
		            case EResult.AccountLoginDeniedNeedTwoFactor:
			            Invoke((MethodInvoker)delegate
			            {
				            var login = new FormLogin(this, "NeedTwoFactor");
				            // If we use ShowDialog here, we get disconencted and can't login
				            login.Show(this);
			            });
			            break;
		            case EResult.InvalidPassword when string.IsNullOrEmpty(FormLogin.Password):
			            File.Delete(Path.Combine(ConfigPath, "loginkey"));
			            File.Delete(Path.Combine(ConfigPath, "user"));
			            MessageBox.Show("Your saved login seems to be invalid, so it was removed.\nTry logging in again.", "Login Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
			            break;
		            default:
			            if (callback.Result != EResult.TryAnotherCM)
				            MessageBox.Show("Unable to login to Steam: " + callback.Result, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			            break;
	            }

	            return;
            }
            Log("\nLogged in");
            Invoke((MethodInvoker)delegate
            {
                btnLogin.Hide();
                btnBotSettings.Enabled = true;
                lSteamStatus.Text = "Steam: Logged in";
            });

			// To other stuff here after logging in (like joining chatrooms)
		}

	    private void OnLoginKey(SteamUser.LoginKeyCallback callback)
        {
            File.WriteAllText(Path.Combine(ConfigPath, "loginkey"), callback.LoginKey);
            user.AcceptNewLoginKey(callback);
        }

	    private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            friends.SetPersonaState(config.LoginAs);
        }

	    private void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            // Join chatrooms
            if (config.Chatrooms != null)
            {
                foreach (SteamID groupID in GetGroups())
                    if (config.Chatrooms.ToString().IndexOf(groupID.AccountID.ToString()) > -1)
                        friends.JoinChat(groupID);
            }

            // Start game
            PlayGame(config.GamePlayed_ID, config.GamePlayed_ExtraInfo);

	        // Discord
	        if (config.Discord_Enabled)
	        {
		        discordBot = new DiscordBot(this);
		        discordBot.Error += DiscordError;
			}
		}

	    private void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            if (config.FriendRequest == "AcceptAll")
                Log(callback.PersonaName + " is now my friend");
        }

	    private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
	        Log($"\nGot invite to {callback.ChatRoomName} from {friends.GetFriendPersonaName(callback.PatronID)}");

	        switch (config.ChatRequest)
	        {
		        case "AcceptAll":
		        case "SuperadminOnly" when callback.InvitedID.AccountID == config.Superadmin:
					friends.JoinChat(callback.ChatRoomID);
			        break;
	        }
        }

	    private void OnChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            var bot = callback.ChatMembers.Single(s => s.SteamID == client.SteamID);
            Log($"\nJoined {callback.ChatRoomName} as {bot.Details}");

            // Add to chatrooms list
            Invoke((MethodInvoker)delegate
            {
                lbChatrooms.Items.Add(callback.ChatRoomName);
            });

            // Create settings if needed
            if (File.Exists(Path.Combine(ConfigPath, "chatrooms", callback.ChatID.ConvertToUInt64() + ".json")))
                LoadSettings(callback.ChatID);
            else
                CreateSettings(callback.ChatID, callback.ChatRoomName, callback.FriendID.AccountID, friends.GetFriendPersonaName(callback.FriendID));
	        var chatRoom = chatrooms[callback.ChatID];

            // Add all current users to the Users list
            chatRoom.Users.Clear();
            foreach (var member in callback.ChatMembers)
            {
                chatRoom.Users.Add(new UserInfo
                {
                    SteamID = member.SteamID,
                    Rank = member.Details,
                    Permission = member.Permissions
                });
            }

            // Check if we have permission to kick
            if (!CheckPermission("kick", chatRoom.Users.Single(s => s.SteamID == client.SteamID).Permission))
                chatRoom.Spam = "None";

            // Then save settings
            SaveSettings(chatRoom);
        }

	    private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback callback)
        {
            if (callback.Type == EChatInfoType.StateChange)
            {
				// If chatrooms doesn't contain the settings, we already left the chat
	            if (!chatrooms.ContainsKey(callback.ChatRoomID))
		            return;

                // Vars
                var chatRoom   = chatrooms[callback.ChatRoomID];
                var chatRoomID = callback.ChatRoomID;
                var state      = callback.StateChangeInfo.StateChange;
                var name       = friends.GetFriendPersonaName(callback.StateChangeInfo.ChatterActedOn);

                // When a user enters or leaves a chat
                Log($"\n[{chatRoom.ChatName.Substring(0, 3)}] {friends.GetFriendPersonaName(callback.StateChangeInfo.ChatterActedOn)} {callback.StateChangeInfo.StateChange}");

                // Add or remove user from Users list
                if (state == EChatMemberStateChange.Entered)
                {
                    chatRoom.Users.Add(new UserInfo
                    {
                        SteamID    = callback.StateChangeInfo.MemberInfo.SteamID,
                        Permission = callback.StateChangeInfo.MemberInfo.Permissions,
                        Rank       = callback.StateChangeInfo.MemberInfo.Details
                    });
                    if (chatRoom.Welcome)
                        SendChatMessage(chatRoomID, $"{chatRoom.WelcomeMsg} {name}{chatRoom.WelcomeEnd}");
                }
                else if (state == EChatMemberStateChange.Left && chatRoom.AllStates)
                    SendChatMessage(chatRoomID, $"Good bye {name}{chatRoom.WelcomeEnd}");

                // Remove user if left in some way
                switch(state)
                {
                    case EChatMemberStateChange.Banned:
                    case EChatMemberStateChange.Disconnected:
                    case EChatMemberStateChange.Kicked:
                    case EChatMemberStateChange.Left:
                        chatRoom.Users.Remove(chatRoom.Users.Single(s => s.SteamID == callback.StateChangeInfo.ChatterActedOn));
                        break;
                }

                // See if bot left the chat
                if (callback.StateChangeInfo.ChatterActedOn == client.SteamID)
                {
                    switch (state)
                    {
                        case EChatMemberStateChange.Banned:
                        case EChatMemberStateChange.Disconnected:
                        case EChatMemberStateChange.Kicked:
                        case EChatMemberStateChange.Left:
                            SaveSettings(chatRoom);
	                        chatrooms.Remove(callback.ChatRoomID);
                            Invoke((MethodInvoker)delegate
                            {
                                lbChatrooms.Items.Remove(chatRoom.ChatName);
                                if (lbChatrooms.Items.Count == 0)
                                    btnChatroomInfo.Enabled = false;
                            });
                            break;
                    }
                }

				// Send to Discord if needed
	            if (config.Discord_Enabled && discordBot.ShouldSendToDiscord(chatRoomID))
	            {
		            switch (state)
		            {
						case EChatMemberStateChange.Entered:
							discordBot.SendMessage($"{name} entered Steam");
							break;

						case EChatMemberStateChange.Left:
							discordBot.SendMessage($"{name} left Steam");
							break;

						case EChatMemberStateChange.Disconnected:
							discordBot.SendMessage($"{name} disconnected from Steam");
							break;

						case EChatMemberStateChange.Kicked:
							discordBot.SendMessage($"{name} got kicked from Steam");
							break;

						case EChatMemberStateChange.Banned:
							discordBot.SendMessage($"{name} got banned from Steam");
							break;
		            }
	            }
            }
        }

	    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Log("\nLogged out");
        }

	    private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            // Writes sentry file
            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open(Path.Combine(ConfigPath, "sentryhash", FormLogin.Username + ".bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;
                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new System.Security.Cryptography.SHA1CryptoServiceProvider())
                    sentryHash = sha.ComputeHash(fs);
            }

            // Tell Steam we're accepting the sentry file
            user.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash
            });
        }

	    private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
        {
            var message = callback.Message;
            var userID = callback.Sender;
            if (!string.IsNullOrEmpty(message.Trim()))
            {
                if (!string.IsNullOrEmpty(config.API_CleverbotIO))
                {
                    Log($"\n{friends.GetFriendPersonaName(callback.Sender)}: {message}");

                    // Check if we have a Cleverbot session
                    if (!cleverbotSessions.ContainsKey(userID))
                    {
                        Log($"\nCreated Cleverbot session for {friends.GetFriendPersonaName(userID)}");
                        var apikey = config.API_CleverbotIO.Split(';');
	                    cleverbotSessions[userID] = CleverbotSession.NewSession(apikey[0], apikey[1]);
                    }
                    // Use Cleverbot
	                var session = cleverbotSessions[userID];
	                try
	                {
		                SendMessage(userID, session.Send(message));
	                }
	                catch (Exception e)
	                {
		                Log("\n" + e.Message);
	                }
                }
            }
        }

	    private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            // TODO: Log this different than friend messages
            // TODO: Does this assume we are friends with the user?
            Log($"\n{friends.GetFriendPersonaName(callback.ChatterID)}: {callback.Message}");

            // Variables
            var message    = callback.Message;
            var userID     = callback.ChatterID;
            var chatRoomID = callback.ChatRoomID;
            var chatRoom   = chatrooms[chatRoomID];
            var chatter    = chatRoom.Users.Single(s => s.SteamID == userID);
            var bot        = chatRoom.Users.Single(s => s.SteamID == client.SteamID);
            var now        = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timeout    = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var name = friends.GetFriendPersonaName(callback.ChatterID);
            var game = friends.GetFriendGamePlayedName(callback.ChatterID);

            // Check if mod
            var isMod = false;
            switch(chatter.Rank)
            {
                case EClanPermission.Moderator:
                case EClanPermission.Officer:
                case EClanPermission.Owner:
                    isMod = true;
                    break;
            }

            // Always treat Superadmin as mod
            if (userID.AccountID == config.Superadmin) isMod = true;

            // Check if bot is mod
            var isBotMod = false;
            switch(bot.Rank)
            {
                case EClanPermission.Moderator:
                case EClanPermission.Officer:
                case EClanPermission.Owner:
                    isBotMod = true;
                    break;
            }

            // Spam protection
            if (isBotMod && !isMod && chatRoom.Spam != "None")
            {
                if (chatter.LastMessage == message && chatter.LastTime + 3000 > now)
                {
                    // Sent the same message within the same 3 seconds
                    SendChatMessage(chatRoomID, $"Please {name}, don't spam");
                    if (chatRoom.Spam == "Kick")
                        friends.KickChatMember(chatRoomID, userID);
                    else if (chatRoom.Spam == "Ban")
                        friends.BanChatMember(chatRoomID, userID);
                }
                else if (chatter.LastTime + 500 > now)
                {
                    // Sent a mesage within the same 0.5 seconds
                    SendChatMessage(chatRoomID, $"Please {name}, don't post too fast");
                    if (chatRoom.Spam == "Kick")
                        friends.KickChatMember(chatRoomID, userID);
                    else if (chatRoom.Spam == "Ban")
                        friends.BanChatMember(chatRoomID, userID);
                }
                else if (message.Length > 400)
                {
                    // Sent a message longer than 400 characters
                    SendChatMessage(chatRoomID, $"Please {name}, don't post too long messages");
                    if (chatRoom.Spam == "Kick")
                        friends.KickChatMember(chatRoomID, userID);
                    else if (chatRoom.Spam == "Ban")
                        friends.BanChatMember(chatRoomID, userID);
                }
            }
            chatter.LastMessage = message;
            chatter.LastTime = now;

			// Check if we should send it to Discord
	        if (config.Discord_Enabled && discordBot.ShouldSendToDiscord(chatRoomID))
				discordBot.SendMessage($"{name}: {message}");

            // Link resolving
            if (chatRoom.Links)
            {
	            var links = message.Split(' ');
	            foreach (var link in links)
	            {
		            if (link.StartsWith("http"))
		            {
			            var response = Get(link);
			            if (!string.IsNullOrEmpty(response))
			            {
				            if (response.IndexOf("<title") > -1 && response.IndexOf("</title>") > -1)
				            {
					            string title = GetStringBetween(GetStringBetween(response, "<title", "</title"), ">");
					            if (title.IndexOf("YouTube") > -1)
					            {
						            // Youtube
						            if (string.IsNullOrEmpty(config.API_Google))
						            {
							            var video = title.Substring(0, title.LastIndexOf("- YouTube"));
							            SendChatMessage(chatRoomID, $"{name} posted a video: {video}");
						            }
						            else
						            {
							            var videoID = link.Substring(link.StartsWith("https://youtu.be/") ? 17 : 32, 11);

							            if (!string.IsNullOrEmpty(videoID))
							            {
								            dynamic  info      = JsonConvert.DeserializeObject(Get("https://www.googleapis.com/youtube/v3/videos?id=" + videoID + "&key=" + config.API_Google + "&part=statistics,snippet,contentDetails"));
								            var      video     = info.items[0];
								            TimeSpan time      = System.Xml.XmlConvert.ToTimeSpan(video.contentDetails.duration.ToString());
								            var      duration  = time.Minutes + ":" + time.Seconds;
								            SendChatMessage(chatRoomID, $"{name} posted a video: {video.snippet.title} by {video.snippet.channelTitle} with {video.statistics.viewCount.ToString()} views, lasting {duration}");
							            }
						            }
					            }
					            else if (title.Contains("on Steam"))
						            SendChatMessage(chatRoomID, $"{name} posted a game: {GetStringBetween(title, "", "on Steam")}");
					            else if (title.Contains("Steam Community :: Screenshot"))
						            SendChatMessage(chatRoomID, $"{name} posted a screenshot from {GetStringBetween(response, "This item is incompatible with ", ". Please see the")}");
					            else if (!title.Contains("Item Inventory"))
						            SendChatMessage(chatRoomID, $"{name} posted {title.Trim()}");
				            }
			            }
		            }
	            }
            }

            // Cleverbot
            if (message.StartsWith(".") && !string.IsNullOrEmpty(config.API_CleverbotIO))
            {
                // Check if we have a Cleverbot session
                if (!cleverbotSessions.ContainsKey(chatRoomID))
                {
                    Log($"\nCreated Cleverbot session for {chatRoom.ChatName}");
                    var apikey = config.API_CleverbotIO.Split(';');
	                cleverbotSessions[chatRoomID] = CleverbotSession.NewSession(apikey[0], apikey[1]);
                }
                // Use Cleverbot
                var session = cleverbotSessions[chatRoomID];
	            try
	            {
		            SendChatMessage(chatRoomID, session.Send(message));
	            }
	            catch (Exception e)
	            {
		            Log("\n" + e.Message);
	            }
            }

            // Always on commands
            if (message == "!leave")
            {
                if (isMod || userID == chatRoom.InvitedID)
                {
                    Log($"\nLeft {chatRoom.ChatName} with request from {name}");
                    SaveSettings(chatRoom);
	                chatrooms.Remove(callback.ChatRoomID);
                    Invoke((MethodInvoker)delegate
                    {
                        lbChatrooms.Items.Remove(chatRoom.ChatName);
                        if (lbChatrooms.Items.Count == 0)
                            btnChatroomInfo.Enabled = false;
                    });
                    friends.LeaveChat(chatRoomID);
                }
            }

            // Superadmin commands
            if (userID.AccountID == config.Superadmin)
            {
                if (message == "!timestamp")
                    SendChatMessage(chatRoomID, $"Current timestamp: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
                else if (message.StartsWith("!permission"))
                    SendChatMessage(chatRoomID, CheckPermission(message.Split()[1], bot.Permission).ToString());
                else if (message == "!info")
                {
                    // Get if we are using Mono
	                var runtime = Type.GetType("Mono.Runtime") != null ? "Mono" : ".NET";
                    // Get if system is 32 or 64 bit
	                var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";

                    // TODO: Maybe use WMI to get more info or find cross platform version of it
                    SendChatMessage(chatRoomID, $"\nOS: {Environment.OSVersion} {arch} \nRuntime: {runtime} {Environment.Version}");
                }
            }

            // Admin commands
            if (isMod)
            {
                if (message == "!nodelay")
                {
                    chatRoom.DelayDefine  = 0;
                    chatRoom.DelayGames   = 0;
                    chatRoom.DelayRandom  = 0;
                    chatRoom.DelayRecents = 0;
                    chatRoom.DelayYT      = 0;
                    SendChatMessage(chatRoomID, "All delays reset");
                }
                else if (message.StartsWith("!toggle "))
                {
                    bool state;
                    var toggle = message.Split(' ')[1];
                    switch (toggle.ToLower())
                    {
                        case "cleverbot": chatRoom.Cleverbot = state = !chatRoom.Cleverbot; break;
                        case "translate": chatRoom.Translate = state = !chatRoom.Translate; break;
                        case "commands":  chatRoom.Commands  = state = !chatRoom.Commands;  break;
                        case "define":    chatRoom.Define    = state = !chatRoom.Define;    break;
                        case "weather":   chatRoom.Weather   = state = !chatRoom.Weather;   break;
                        case "store":     chatRoom.Store     = state = !chatRoom.Store;     break;
                        case "responses": chatRoom.Responses = state = !chatRoom.Responses; break;
                        case "links":     chatRoom.Links     = state = !chatRoom.Links;     break;
                        case "rules":     chatRoom.Rules     = state = !chatRoom.Rules;     break;
                        case "poke":      chatRoom.Poke      = state = !chatRoom.Poke;      break;

                        // These should have custom messages
                        case "welcome":     chatRoom.Welcome       = state = !chatRoom.Welcome;       break;
                        case "games":       chatRoom.Games         = state = !chatRoom.Games;         break; // !games and !recents
                        case "search":      chatRoom.Search        = state = !chatRoom.Search;        break; // !yt
                        case "autowelcome": chatRoom.AutoWelcome   = state = !chatRoom.AutoWelcome;   break;
                        case "allstates":   chatRoom.AllStates     = state = !chatRoom.AllStates;     break;
                        case "allpoke":     chatRoom.AllPoke       = state = !chatRoom.AllPoke;       break;
                        case "custom":      chatRoom.CustomEnabled = state = !chatRoom.CustomEnabled; break;

                        default: SendChatMessage(chatRoomID, "Unknown toggle"); return;
                    }
                    toggle = toggle.First().ToString().ToUpper() + toggle.Substring(1);
                    if (state)
                        SendChatMessage(chatRoomID, toggle + " is now enabled");
                    else
                        SendChatMessage(chatRoomID, toggle + " is now disabled");
                    SaveSettings(chatRoom);
                }
                else if (message.StartsWith("!setdelay "))
                {
                    var set = message.Split(' ');
                    if (int.TryParse(set[2], out int delay))
                    {
                        switch(set[1])
                        {
                            case "define":  chatRoom.DelayDefine  = delay; break;
                            case "games":   chatRoom.DelayGames   = delay; break;
                            case "random":  chatRoom.DelayRandom  = delay; break;
                            case "recents": chatRoom.DelayRecents = delay; break;
                            case "search":  chatRoom.DelaySearch  = delay; break;
                            case "yt":      chatRoom.DelayYT      = delay; break;
                            default: return;
                        }
                        SendChatMessage(chatRoomID, $"Delay of {set[1]} was set to {set[2]} seconds");
                        SaveSettings(chatRoom);
                    }
                    else
                        SendChatMessage(chatRoomID, "Delay needs to be a number (in seconds)");
                }
                else if (message.StartsWith("!set"))
                {
                    var set = message.Split(' ');
                    if (set[1] == "spam")
                    {
                        switch(set[2])
                        {
                            case "ban":
                                if (CheckPermission("ban", bot.Permission))
                                {
                                    chatRoom.Spam = "Ban";
                                    SendChatMessage(chatRoomID, "Spam will now ban");
                                }
                                else
                                    SendChatMessage(chatRoomID, "I don't have permission to ban users");
                                break;
                            case "kick":
                                if (CheckPermission("kick", bot.Permission))
                                {
                                    chatRoom.Spam = "Kick";
                                    SendChatMessage(chatRoomID, "Spam will now kick");
                                }
                                else
                                    SendChatMessage(chatRoomID, "I don't have permission to kick users");
                                break;
                            case "none":
                                chatRoom.Spam = "None";
                                SendChatMessage(chatRoomID, "Spam will now be ignored");
                                break;
                            default:
                                SendChatMessage(chatRoomID, "Unknown spam toggle, use ban, kick or none");
                                break;
                        }
                    }
                    else if (set[1] == "dc")
                    {
                        // We make this a switch in case we want to use warnings later
                        switch (set[2])
                        {
                            case "kick":
                                chatRoom.DCKick = "Kick";
                                SendChatMessage(chatRoomID, "Will now kick user after 5 disconnections");
                                break;
                            case "none":
                                chatRoom.DCKick = "None";
                                SendChatMessage(chatRoomID, "Will now ignore disconnections");
                                break;
                            default:
                                SendChatMessage(chatRoomID, "Unknown dc toggle, use kick or none");
                                break;
                        }
                    }
                }
                else if (message.StartsWith("!rule "))
                {
                    switch(message.Split(' ')[1])
                    {
                        case "add":
                            // Add rule
                            chatRoom.SetRules.Add(message.Substring(10));
	                        SendChatMessage(chatRoomID, chatRoom.Rules ? "Rule added" : "Rule added, but rules are disabled");
	                        break;
                        case "remove":
                            // Remove rule
                            var search = message.Substring(13).ToLower();
                            var results = chatRoom.SetRules.FindAll(s => s.ToLower().Contains(search));
                            if (results.Count == 0)
                                SendChatMessage(chatRoomID, "No rule matching your search was found");
                            else if (results.Count > 1)
                                SendChatMessage(chatRoomID, "Multiple rules matching your search was found");
                            else
                            {
                                chatRoom.SetRules.Remove(chatRoom.SetRules.Single(s => s.ToLower().Contains(search)));
                                SendChatMessage(chatRoomID, "Rule removed");
                            }
                            break;
                        case "cls":
                        case "clear":
                            // Clear all rules
                            chatRoom.SetRules.Clear();
                            SendChatMessage(chatRoomID, "All rules removed");
                            break;
                        default:
                            SendChatMessage(chatRoomID, "Invalid argument, use add, remove or clear");
                            break;
                    }
                    SaveSettings(chatRoom);
                }
            }

            // User commands with cooldown / Admin commands
            if (chatRoom.Commands || isMod)
            {
                if (message.StartsWith("!poke "))
                {
                    if (isMod || chatRoom.AllPoke)
                    {
                        var search = message.Substring(6);
                        var results = new List<SteamID>();
                        foreach (UserInfo user in chatRoom.Users)
                            if (friends.GetFriendPersonaName(user.SteamID).ToLower().IndexOf(search.ToLower()) > -1)
                                results.Add(user.SteamID);

                        if (results.Count > 1)
                            SendChatMessage(chatRoomID, "Found multiple users, try to be more specific");
                        else if (results.Count == 0)
                            SendChatMessage(chatRoomID, "No user found");
                        else if (results[0] == chatRoom.LastPoke)
                            SendChatMessage(chatRoomID, "You have already poked that user");
                        else if (results[0] == client.SteamID)
                            SendChatMessage(chatRoomID, "Poked myself, *poke*");
                        else
                        {
                            SendMessage(results[0], $"Hey you! {friends.GetFriendPersonaName(userID)} poked you in {chatRoom.ChatName}");
                            SendChatMessage(chatRoomID, $"Poked {friends.GetFriendPersonaName(results[0])}");
                            chatRoom.LastPoke = userID;
                        }
                    }
                }
                else if (message.StartsWith("!random"))
                {
                    if (isMod || chatRoom.TimeoutRandom < timeout)
                    {
                        var users = new List<UserInfo>(chatRoom.Users);
                        users.Remove(users.Single(s => s.SteamID == client.SteamID));
                        SendChatMessage(chatRoomID, $"The winner is {friends.GetFriendPersonaName(users[new Random().Next(users.Count)].SteamID)}!");
                        chatRoom.TimeoutRandom = timeout + chatRoom.DelayRandom;
                    }
                    else
                        SendChatMessage(chatRoomID, $"This command is disabled for {FormatTime(chatRoom.TimeoutRandom)}");
                }
                else if (message == "!games" && chatRoom.Games)
                {
                    if (string.IsNullOrEmpty(config.API_Steam))
                        SendChatMessage(chatRoomID, "Steam API isn't set up properly to use this command");
                    else if (isMod || chatRoom.TimeoutGames < timeout)
                    {
                        var response = Get("http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key=" + config.API_Steam + "&include_appinfo=1&include_played_free_games=1&steamid=" + userID.ConvertToUInt64());
                        if (!string.IsNullOrEmpty(response))
                        {
                            dynamic result = JsonConvert.DeserializeObject(response);
                            SendChatMessage(chatRoomID, string.Format("You have {0} games", result.response.game_count));
                            JArray array = result.response.games;
                            var games = new JArray(array.OrderByDescending(obj => obj["playtime_forever"]));
                            for (var i = 0; i <= 4; i++)
                                SendChatMessage(chatRoomID, $"{i + 1}: {games[i]["name"]} ({Math.Round((double) games[i]["playtime_forever"] / 60)} hours played)");
                        }
                        else
                            SendChatMessage(chatRoomID, "Error: No or invalid response from Steam");
                        chatRoom.TimeoutGames = timeout + chatRoom.DelayGames;
                    }
                    else
                        SendChatMessage(chatRoomID, $"This command is disabled for {FormatTime(chatRoom.TimeoutGames - timeout)}");
                }
                else if (message == "!recents" && chatRoom.Games)
                {
                    if (string.IsNullOrEmpty(config.API_Steam))
                        SendChatMessage(chatRoomID, "Steam API isn't set up properly to use this command");
                    else if (isMod || chatRoom.TimeoutRecents < timeout)
                    {
                        string response = Get("http://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key=" + config.API_Steam + "&steamid=" + userID.ConvertToUInt64());
                        if (string.IsNullOrEmpty(response))
                            SendChatMessage(chatRoomID, "Error: No or invalid response from Steam");
                        else
                        {
                            dynamic result = JsonConvert.DeserializeObject(response);
                            SendChatMessage(chatRoomID, string.Format("You have played {0} games recently", result.response.total_count));
                            JArray array = result.response.games;
                            var games = new JArray(array.OrderByDescending(obj => obj["playtime_2weeks"]));
                            var total = 5;
                            if (games.Count < 5)
	                            total = games.Count;
                            for (var i = 0; i < total; i++)
                            {
	                            var playtime = (int)Math.Round((double)games[i]["playtime_2weeks"] / 60);
	                            SendChatMessage(chatRoomID,
		                            playtime == 1
			                            ? $"{i + 1}: {games[i]["name"]} (1 hour played recently)"
			                            : $"{i + 1}: {games[i]["name"]} ({playtime} hours played recently)");
                            }
                        }
                        chatRoom.TimeoutRecents = timeout + chatRoom.DelayRecents;
                    }
                    else
                        SendChatMessage(chatRoomID, $"This command is disabled for {FormatTime(chatRoom.TimeoutRecents - timeout)}");
                }
                else if (message.StartsWith("!define ") && chatRoom.Define)
                {
                    if (isMod || chatRoom.TimeoutDefine < timeout)
                    {
                        string response = Get("http://api.urbandictionary.com/v0/define?term=" + message.Substring(8));
                        dynamic result = JsonConvert.DeserializeObject(response);
                        if (result.result_type == "no_results")
                            SendChatMessage(chatRoomID, "No results found");
                        else
                        {
                            string def = result.list[0].definition;
                            def = def.Replace("\n", " ");
	                        SendChatMessage(chatRoomID,
		                        def.Length < 500
			                        ? def
			                        : $"{def.Substring(0, 500)}...");
	                        if (isMod)
                            {
                                if (result.list[0].example != null)
                                    SendChatMessage(chatRoomID, string.Format("Example: \n{0}", result.list[0].example));

                                double thumbsUp    = result.list[0].thumbs_up;
                                double thumbsDown  = result.list[0].thumbs_down;
                                var    thumbsTotal = thumbsUp + thumbsDown;
                                SendChatMessage(chatRoomID, $"Rating: {Math.Round(thumbsUp / thumbsTotal * 100)}% positive ({thumbsUp}/{thumbsTotal})");
                            }
                        }
                        chatRoom.TimeoutDefine = timeout + chatRoom.DelayDefine;
                    }
                    else
                        SendChatMessage(chatRoomID, $"This command is disabled for {FormatTime(chatRoom.TimeoutDefine - timeout)}");
                }
                else if (message.StartsWith("!yt ") && chatRoom.Search)
                {
                    if (isMod || chatRoom.TimeoutYT < timeout)
                    {
                        if (string.IsNullOrEmpty(config.API_Google))
                            SendChatMessage(chatRoomID, "Google API isn't set up properly to use this command");
                        else
                        {
                            dynamic result = JsonConvert.DeserializeObject(Get("https://www.googleapis.com/youtube/v3/search?part=snippet&q=" + message.Substring(4) + "&type=video&key=" + config.API_Google));
                            if (string.IsNullOrEmpty(result.items[0].snippet.ToString()))
                                SendChatMessage(chatRoomID, "No results found");
                            else
                            {
                                string results = null;
                                var limit = 1;
                                if (isMod) limit = 3;
                                for (var i = 0; i < limit; i++)
                                    if (!string.IsNullOrEmpty(result.items[i].ToString()))
                                        results += string.Format("\n{0} ({1}): https://youtu.be/{2}", result.items[i].snippet.title, result.items[i].snippet.channelTitle, result.items[i].id.videoId);
                                SendChatMessage(chatRoomID, "Results: " + results);
                            }
                        }
                        chatRoom.TimeoutYT = timeout + chatRoom.DelayYT;
                    }
                    else
                        SendChatMessage(chatRoomID, $"This command is disabled for {FormatTime(chatRoom.TimeoutYT - timeout)}");
                }
            }

            // User commands
            if (chatRoom.Commands)
            {
                if (message == "!help")
                    SendChatMessage(chatRoomID, "Check https://github.com/KraXarN/KraxbotOSS/wiki/Commands for all commands and how to use them");
                else if (message == "!bday")
                {
                    if (string.IsNullOrEmpty(config.API_Steam))
                        SendChatMessage(chatRoomID, "Steam API isn't set up properly to use this command");
                    else
                    {
                        var response = Get("http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=" + config.API_Steam + "&steamids=" + userID.ConvertToUInt64());
                        if (string.IsNullOrEmpty(response))
                            SendChatMessage(chatRoomID, "Error: No or invalid response from Steam");
                        else
                        {
                            dynamic result = JsonConvert.DeserializeObject(response);
                            var date = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            date = date.AddSeconds(long.Parse(result.response.players[0].timecreated.ToString()));
                            var curDate = DateTime.UtcNow;
	                        SendChatMessage(chatRoomID,
		                        curDate.Year - date.Year == 1
			                        ? $"{name}'s Steam birthday is {date.Day}{GetDateSuffix(date.Day)} of {date:MMMM} (Account created {date.Year} and 1 year old)"
			                        : $"{name}'s Steam birthday is {date.Day}{GetDateSuffix(date.Day)} of {date:MMMM} (Account created {date.Year} and {curDate.Year - date.Year} years old)");
                        }
                    }
                }
                else if (message == "!users")
                {
                    int members, mods, officers;
                    var nobodies = members = mods = officers = 0;
                    var owner = "no ";
                    foreach (var u in chatRoom.Users)
                    {
                        switch (u.Rank)
                        {
                            case EClanPermission.Nobody:    nobodies++;   break;
                            case EClanPermission.Member:    members++;    break;
                            case EClanPermission.Moderator: mods++;       break;
                            case EClanPermission.Officer:   officers++;   break;
                            case EClanPermission.Owner:     owner = null; break;
                        }
                    }
                    string users = null;
                    if (nobodies != 0) users += nobodies + " are guests, ";
                    if (members != 0)  users += members  + " are users, ";
                    if (mods != 0)     users += mods     + " are mods, ";
                    if (officers != 0) users += officers + " are admins, ";
                    SendChatMessage(chatRoomID, $"{chatRoom.Users.Count} people are in this chat, where {users.Substring(0, users.Length - 2)} and {owner}owner");
                }
                else if (message == "!invited")
                    SendChatMessage(chatRoomID, chatRoom.InvitedName + " invited me to this chat");
                else if (message == "!name")
                {
	                var chatr = chatter.Rank.ToString();
	                SendChatMessage(chatRoomID, string.IsNullOrEmpty(game) ? $"{name} ({chatr})" : $"{name} playing {game} ({chatr})");
                }
                else if (message == "!ver")
                    SendChatMessage(chatRoomID, $"KraxbotOSS {version} by Kraxie / kraxarn");
                else if (message == "!id")
                    SendChatMessage(chatRoomID, $"{name}'s SteamID is {userID}");
                else if (message == "!chatid")
                    SendChatMessage(chatRoomID, "This chat's SteamID is " + chatRoomID);
                else if (message.StartsWith("!8ball"))
                {
                    string[] words = {
                        "It is certain", "It is decidedly so", "Without a doubt",
                        "Yes definitely", "You may rely on it", "As I see it, yes",
                        "Most likely", "Outlook good", "Yes", "Signs point to yes",
                        "Reply hazy try again", "Ask again later", "Better not tell you now",
                        "Cannot predict now", "Concentrate and ask again", "Do not count on it",
                        "My reply is no", "My sources say no", "Outlook not so good", "Very doubtful"
                    };
                    SendChatMessage(chatRoomID, words[new Random().Next(words.Length)]);
                }
                else if (message == "!time")
                    SendChatMessage(chatRoomID, "Current time is " + DateTime.Now.ToShortTimeString());
                else if (message.StartsWith("!roll"))
                {
                    int.TryParse(message.Split(' ')[1], out var max);
                    SendChatMessage(chatRoomID, "Your number is " + new Random().Next(0, max));
                }
                else if (message.StartsWith("!math "))
                    SendChatMessage(chatRoomID, "= " + new DataTable().Compute(message.Substring(6), null));
                else if (message.StartsWith("!players "))
                {
                    // TODO: Collect all results in a list and choose the value with most players
                    var app = message.Substring(9);
                    var response = Get("http://api.steampowered.com/ISteamApps/GetAppList/v2");
                    if (string.IsNullOrEmpty(response))
                        SendChatMessage(chatRoomID, "Error: No or invalid response from Steam");
                    else
                    {
                        dynamic result  = JsonConvert.DeserializeObject(response);
                        string gameName = null;
                        var playerCount = 0;
                        foreach (var value in result.applist.apps)
                        {
                            if (value.name.ToString().ToLower().IndexOf(app.ToLower()) > -1)
                            {
                                gameName = value.name;
                                playerCount = JsonConvert.DeserializeObject(Get("http://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=" + value.appid)).response.player_count;
                                if (playerCount > 0)
	                                break;
                            }
                        }

	                    SendChatMessage(chatRoomID,
		                    string.IsNullOrEmpty(gameName) ? "No results found" : $"There are currently {playerCount} people playing {gameName}");
                    }
                }
                else if (message == "!players")
                {
                    var appID = friends.GetFriendGamePlayed(userID).AppID;
                    if (appID == 0)
                        SendChatMessage(chatRoomID, "You need to either play a game or specify a game to check players");
                    else
                    {
                        var response = Get("http://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid=" + appID);
                        if (string.IsNullOrEmpty(response))
                            SendChatMessage(chatRoomID, "Error: No or invalid response from Steam");
                        else
                        {
                            dynamic result = JsonConvert.DeserializeObject(response);
                            SendChatMessage(chatRoomID, $"There are currently {result.response.player_count} people playing {game}");
                        }
                    }

                    SendChatMessage(chatRoomID, friends.GetFriendGamePlayed(userID));
                }
                else if (message.StartsWith("!weather ") && chatRoom.Weather)
                {
                    if (string.IsNullOrEmpty(config.API_OpenWeather))
                        SendChatMessage(chatRoomID, "Weather API isn't set up properly to use this command");
                    else
                    {
                        dynamic result = JsonConvert.DeserializeObject(Get("http://api.openweathermap.org/data/2.5/weather?units=metric&appid=" + config.API_OpenWeather + "&q=" + message.Substring(9)));
                        if (!string.IsNullOrEmpty(result.message))
                            SendChatMessage(chatRoomID, result.message);
                        else
                        {
                            DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(long.Parse(result.dt.ToString()));
                            SendChatMessage(chatRoomID, $"The weather in {result.name} is {result.weather[0].main}, {Math.Round(double.Parse(result.main.temp.ToString()))}ºC with wind at {Math.Round(double.Parse(result.wind.speed.ToString()))} m/s and {result.clouds.all}% clouds (Updated {date.Hour}:{date.Minute})");
                        }
                    }
                }
                else if (message.StartsWith("!convert "))
                {
	                dynamic result = JsonConvert.DeserializeObject(Get("http://api.duckduckgo.com/?format=json&q=" + message.Substring(9)));
	                SendChatMessage(chatRoomID,
		                string.IsNullOrEmpty(result.Answer.ToString()) ? "No answer found" : (string) result.Answer.ToString());
                }
                else if (message == "!rules" && chatRoom.Rules)
                {
                    if (chatRoom.SetRules.Count == 0)
                        SendChatMessage(chatRoomID, "No rules found, use !rule to add some");
                    else
                    {
                        var count = 1;
                        foreach (var rule in chatRoom.SetRules)
                        {
                            SendChatMessage(chatRoomID, $"{count}: {rule}");
                            count++;
                        }
                    }
                }
            }
        }

		#endregion

        #region Steam functions

        public void Login(string username, string password, bool rememberPassword, string authCode = null, string twoFactorCode = null)
        {
            var isUsernameNull = string.IsNullOrEmpty(username);
            var isPasswordNull = string.IsNullOrEmpty(password);
            if ((isUsernameNull && isPasswordNull) || (isUsernameNull || isPasswordNull))
                return;

            // Use sentry hash if we have one
            byte[] sentryHash = null;
            if (File.Exists(Path.Combine(ConfigPath, "sentryhash", username + ".bin")))
                sentryHash = CryptoHelper.SHAHash(File.ReadAllBytes(Path.Combine(ConfigPath, "sentryhash", username + ".bin")));

            user.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                Password = password,
                SentryFileHash = sentryHash,
                ShouldRememberPassword = rememberPassword,
                AuthCode = authCode,
                TwoFactorCode = twoFactorCode
            });
        }
        
	    public void Login(string username)
        {
            // Use sentry hash if we have one
            byte[] sentryHash = null;
            if (File.Exists(Path.Combine(ConfigPath, "sentryhash", username + ".bin")))
                sentryHash = CryptoHelper.SHAHash(File.ReadAllBytes(Path.Combine(ConfigPath, "sentryhash", username + ".bin")));

            // Get our login key
            string loginkey = null;
            if (File.Exists(Path.Combine(ConfigPath, "loginkey")))
                loginkey = File.ReadAllText(Path.Combine(ConfigPath, "loginkey"));

            user.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                ShouldRememberPassword = true,
                LoginKey = loginkey,
                SentryFileHash = sentryHash
            });
        }
        
	    public void UpdateBotSetttings(string name, EPersonaState state)
        {
            friends.SetPersonaName(name);
            friends.SetPersonaState(state);
        }
        
	    public List<SteamID> GetGroups()
        {
            var groups = new List<SteamID>();
            for (var i = 0; i < friends.GetClanCount(); i++)
                groups.Add(friends.GetClanByIndex(i));
            return groups;
        }
        
	    public List<SteamID> GetFriends()
        {
            var friend = new List<SteamID>();
            for (var i = 0; i < friends.GetFriendCount(); i++)
                friend.Add(friends.GetFriendByIndex(i));
            return friend;
        }
		
	    public string GetFriendName(SteamID userID) => friends.GetFriendPersonaName(userID);

		public string GetGroupName(SteamID clanID) => friends.GetClanName(clanID);

		public void PlayGame(ulong gameID, string gameExtraInfo)
        {
            var playGame = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            playGame.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = gameID,
                game_extra_info = gameExtraInfo

            });
            client.Send(playGame);
        }

		#endregion

		#region Other stuff

	    public void Log(string text)
        {
            Invoke((MethodInvoker)delegate
            {
                log.AppendText(text);
            });
        }

		public void SendChatMessage(SteamID chatRoomID, string message) 
			=> friends.SendChatRoomMessage(chatRoomID, EChatEntryType.ChatMsg, message);

		private void SendMessage(SteamID userID, string message) 
			=> friends.SendChatMessage(userID, EChatEntryType.ChatMsg, message);

		public void CreateSettings(SteamID chatroomID, string chatroomName, SteamID invitedID, string InvitedName)
        {
            chatrooms[chatroomID] = new Settings
            {
                ChatID      = chatroomID,
                ChatName    = chatroomName,
                InvitedID   = invitedID,
                InvitedName = InvitedName
            };
        }

	    private static void SaveSettings(Settings setting)
        {
            var file = Path.Combine(ConfigPath, "chatrooms", setting.ChatID.ConvertToUInt64() + ".json");
            File.WriteAllText(file, JsonConvert.SerializeObject(setting, Formatting.Indented));
        }

	    private void LoadSettings(SteamID chatRoomID)
        {
            var file = File.ReadAllText(Path.Combine(ConfigPath, "chatrooms", chatRoomID.ConvertToUInt64() + ".json"));
	        var settings = JsonConvert.DeserializeObject<Settings>(file);
			chatrooms[settings.ChatID] = settings;
        }

	    private static bool CheckPermission(string check, EChatPermission permission)
	    {
			// TODO: Not actually sure if this is how it works
			switch (check)
			{
				case "kick":
					return (permission & EChatPermission.Kick) == EChatPermission.Kick;
				case "ban":
					return (permission & EChatPermission.Ban) == EChatPermission.Ban;
				default:
					return false;
			}
		}

		private static string Get(string url)
        {
            // TODO: Check if better way to do this
            using (var webClient = new WebClient())
            {
                try {
                    webClient.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:54.0) Gecko/20100101 Firefox/54.0");
                    return webClient.DownloadString(url);
                } catch (WebException e) {
                    Console.WriteLine(e.Message);
                    return null;
                }
            }
        }

	    private static string GetDateSuffix(int day)
	    {
			switch (day % 10)
			{
				case 1 when day != 11:
					return "st";
				case 2 when day != 12:
					return "nd";
				case 3 when day != 13:
					return "rd";
				default:
					return "th";
			}
		}

	    public bool CheckForUpdates()
	    {
		    if (!TryGetLatestVersion(config.Updates == "Beta", out var newVersion))
			    return false;

		    if (version != newVersion)
		    {
			    Invoke(new MethodInvoker(delegate {
				    if (MessageBox.Show($"Current version is {version} \nNew Version is {newVersion} \nDo you want to update now?", "New Update Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					    System.Diagnostics.Process.Start("https://github.com/KraXarN/KraxbotOSS/releases");
			    }));
			    return true;
		    }

		    return false;
		}

	    private bool TryGetLatestVersion(bool beta, out string ver)	
	    {
		    var response = Get($"https://api.github.com/repos/KraXarN/KraxbotOSS/releases{(beta ? "" : "/latest")}");

		    if (string.IsNullOrEmpty(response))
		    {
			    ver = null;
				return false;
			}

		    dynamic result = JsonConvert.DeserializeObject(response);

			ver = ((string) (beta ? result[0].tag_name : result.tag_name)).Substring(1);

			return true;
	    }

		private static string GetStringBetween(string token, string first, string second = null)
        {
            var from = token.IndexOf(first) + first.Length;
            if (!string.IsNullOrEmpty(second))
            {
                var to = token.IndexOf(second);
                return token.Substring(from, to - from);
            }

	        return token.Substring(from);
        }
        
	    private static string FormatTime(int seconds)
        {
            var min = (int)Math.Floor(seconds / 60.0);
            var sec = seconds - min * 60;
            return sec < 10 ? $"{min}:0{sec}" : $"{min}:{sec}";
        }

		#endregion

		#region GUI stuff

        private void BtnSettings_Click(object sender, EventArgs e)
        {
            // Hide the main form and show the settings form
            Form settings = new FormSettings(this);
            settings.ShowDialog(this);

			// See if we enabled Discord
			if (config.Discord_Enabled && discordBot == null)
			{
				discordBot = new DiscordBot(this);
				discordBot.Error += DiscordError;
			}
		}

	    private void DiscordError(Exception e)
	    {
		    DiscordStatus = e.Message;

			if (e is NotFoundException)
		    {
			    MessageBox.Show($"Failed to join Discord channel. Check so the correct channel ID is set in settings and try again. Error code: {e.Message}",
				    "Discord channel not found",
				    MessageBoxButtons.OK, MessageBoxIcon.Warning);
		    }
		    else
		    {
			    MessageBox.Show($"{e.GetType().FullName}\n{e.Message}\n\n{e.StackTrace}",
				    "Failed to connect to Discord",
				    MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

		    discordBot?.Disconnect();
		    discordBot = null;
		    DiscordStatus = "Disabled";
	    }

	    private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (File.Exists(Path.Combine(ConfigPath, "user")))
            {
                var username = File.ReadAllLines(Path.Combine(ConfigPath, "user"));
                FormLogin.Username = username[0];
                Login(username[0]);
            }
            else
            {
                Form login = new FormLogin(this);
                login.ShowDialog(this);
            }
        }
        
	    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            running = false;
	        discordBot?.Disconnect();
	        Environment.Exit(Environment.ExitCode);
        }
        
	    private void BtnBotSettings_Click(object sender, EventArgs e)
        {
            Form botSettings = new FormBotSettings(this)
            {
                Tag = friends.GetPersonaName() + (int)friends.GetPersonaState()
            };
            botSettings.ShowDialog(this);
        }

		private void LbChatrooms_SelectedIndexChanged(object sender, EventArgs e) => btnChatroomInfo.Enabled = true;

		private void BtnChatroomInfo_Click(object sender, EventArgs e)
        {
            Form chatroomInfo = new FormChatroomInfo(chatrooms.Single(s => s.Value.ChatName == lbChatrooms.Items[lbChatrooms.SelectedIndex].ToString()).Value, this);
            chatroomInfo.ShowDialog(this);
        }

		#endregion

		#region Crash handler

		private void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            DumpError(e.Exception);
            #if DEBUG
            #else
                MessageBox.Show(e.Exception.Message + "\n" + e.Exception.StackTrace, "Thread Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            #endif
            Close();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            DumpError(ex);
            #if DEBUG
            #else
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace, "Unhandled Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            #endif
        }
        
	    private static void DumpError(Exception error)
        {
            string[] dump =
            {
	            error.Message, error.StackTrace
            };

            File.WriteAllLines(Path.Combine(ConfigPath, "crash.log"), dump);
        }

		#endregion
	}
}