﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.WebSockets;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using WebSocketContext = WebSocketSharp.Net.WebSockets.WebSocketContext;

namespace D2MPMaster.Browser
{
    class BrowserService : WebSocketService
    {
        protected override void OnClose(CloseEventArgs e)
        {
            Program.Browser.OnClose(ID, Context, e);
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Program.Browser.OnError(ID, Context, e);
            base.OnError(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.Browser.OnMessage(ID, Context, e);
            base.OnMessage(e);
        }

        protected override void OnOpen()
        {
            Program.Browser.OnOpen(ID, Context);
            base.OnOpen();
        }

    }
    class BrowserManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, BrowserClient> Clients = new Dictionary<string, BrowserClient>();
        public Dictionary<string, BrowserClient> UserClients = new Dictionary<string, BrowserClient>();

        public BrowserManager()
        {
            Program.Browser = this;
            Program.LobbyManager.PublicLobbies.CollectionChanged += TransmitLobbiesChange;
        }

        public void TransmitPublicLobbiesUpdate(List<Lobby> lobbies, string[] fields)
        {
            var updates = new JArray();
            foreach (var lobby in lobbies)
            {
                updates.Add(lobby.Update("publicLobbies", fields));
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            Broadcast(msg);
        }

        public void TransmitLobbiesChange(object s, NotifyCollectionChangedEventArgs e)
        {
            var updates = new JArray();
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                updates.Add(DiffGenerator.RemoveAll("publicLobbies"));
            }
            else
            {
                if(e.NewItems != null)
                foreach (var lobby in e.NewItems)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            updates.Add(lobby.Add("publicLobbies"));
                            break;
                    }
                }
                if(e.OldItems != null)
                foreach (var lobby in e.OldItems)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Remove:
                            updates.Add(lobby.Remove("publicLobbies"));
                            break;
                    }
                }
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            Broadcast(msg);
        }

        public void Broadcast(string msg)
        {
            foreach (var client in Clients.Values)
            {
                client.Send(msg);
            }
        }

        public void TransmitLobbyUpdate(string steamid, Lobby lobby, string[] fields)
        {
            var client = UserClients[steamid];
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {lobby.Update("lobbies", fields)};
            client.Send(upd.ToString(Formatting.None));
        }

        public void TransmitLobbySnapshot(string steamid, Lobby lob)
        {
            var client = UserClients[steamid];
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {DiffGenerator.RemoveAll("lobbies"), lob.Add("lobbies") };
            client.Send(upd.ToString(Formatting.None));
        }

        public void OnMessage(string ID, WebSocketContext Context, MessageEventArgs e)
        {
            var client = Clients[ID];
            if (client.proccommand) return;
            client.proccommand = true;
            client.HandleMessage(e.Data, Context, ID);
            client.proccommand = false;
        }

        public void OnClose(string ID, WebSocketContext Context, CloseEventArgs e)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients[ID].OnClose(e, ID);
        }

        public void OnOpen(string ID, WebSocketContext Context)
        {
            var client = new BrowserClient(Context.WebSocket, ID);
            Clients[ID] = client;
            Program.LobbyManager.TransmitPublicLobbySnapshot(client);
            log.Debug(string.Format("Client connected #{1}: {0}.", ID, Context.UserEndPoint));
        }

        public void OnError(string ID, WebSocketContext Context, ErrorEventArgs e)
        {
            //log.Error(e.Message);
        }

        /// <summary>
        /// When a user logged in, check to see if we can merge their BrowserClients
        /// </summary>
        /// <param name="browserClient"></param>
        /// <param name="user"></param>
        public void RegisterUser(BrowserClient browserClient, User user)
        {
            if (UserClients.ContainsKey(user.services.steam.steamid))
            {
                var client = UserClients[user.services.steam.steamid];
                client.RegisterSocket(browserClient.baseWebsocket, browserClient.baseSession);
                Clients[browserClient.baseSession] = client;
                browserClient.Obsolete();
                if (client.lobby != null)
                {
                    TransmitLobbySnapshot(user.services.steam.steamid, client.lobby);
                }
            }
            else
            {
                UserClients.Add(user.services.steam.steamid, browserClient);
            }
        }

        public void DeregisterClient(BrowserClient browserClient, string baseSession)
        {
            try
            {
                Clients.Remove(baseSession);
                if (browserClient.user != null)
                {
                    UserClients.Remove(browserClient.user.services.steam.steamid);
                }
            }
            catch
            {
            }
        }

        public void DeregisterUser(BrowserClient browserClient, User user, string id)
        {
            if (user == null) return;
            //Delete all of their specific lobbies (so they are no longer in a lobby)
            browserClient.SendClearLobby(browserClient.sockets[id]);
            if (browserClient.sockets.Count > 1)
            {
                //Create a new BrowserClient to handle the new de-authed orphan
                var client = new BrowserClient(browserClient.sockets[id], id);
                browserClient.sockets.Remove(id);
                Clients[id] = client;
            }
            else
            {
                UserClients.Remove(browserClient.user.services.steam.steamid);
                Program.LobbyManager.LeaveLobby(browserClient);
            }
        }


    }
}
