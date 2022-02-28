using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Threading.Tasks;

    // Group class
    public class Group
    {
        // total number of users in a group is 5
        private const int FULL_USERS = 5;
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string GroupType { get; set; } = "";
        public int Count { get; set; } = 0;
        public bool isEmpty => Count < FULL_USERS;
        public List<string> UserList = new List<string>(){};
        public List<Messages> ChatHistory = new List<Messages>(){};

        // constructor
        public Group(string name, string description, string type) => (Name, Description, GroupType) = (name, description, type);

        public void IncrementUsersCount(string name)
        {
            UserList.Add(name);
            Count++;
        }

        public void DecrementUsersCount(string name)
        {
            UserList.Remove(name);
            Count--;
        }

        public bool NoUsers()
        {
            if(Count == 0)
            {
                return true;
            }
            return false;
        }
    }

    // ChatHistory class
    public class Messages{
        public string MessageType { get; set; }
        public object[] Param { get; set; }

        public Messages(string messageType, object[] param) => (MessageType, Param) = (messageType, param);
    }

    // ============================================
    // ChatHub
    // ============================================
    public class ChatHub : Hub
    {
        // store all groups in lists
        private static List<Group> GroupList = new List<Group>()
        {
            // groups that are created
            // new Group("one", "dijeiapjd"),
            // new Group("two", "2"),
            // new Group("three", "dijeiapjd"),
        };

        // create a group with name and description
        public string CreateGroup(string name, string description, string type)
        {
            var group = new Group(name, description, type);
            GroupList.Add(group);
            /* no need to increment user count here or it will cause a bug which adds two users because when the user is connected they are also added to the list */
            return group.Id;
        }

        // allow user to join private room if groupId is valid
        public string JoinPrivateRoom(string groupId)
        {
            var searchGroup = GroupList.Find(g => g.Id == groupId);
            
            if(searchGroup == null)
            {
                return null;
            }

            if(!searchGroup.isEmpty)
            {
                return null;
            }

            return searchGroup.Id;
        }

        // ==========================================
        // Message Functions
        // ==========================================
        private async Task UpdateList(string? id = null)
        {
            var activeGroups = GroupList.FindAll(g => g.isEmpty);

            if(id == null)
            {
                await Clients.All.SendAsync("UpdateList", activeGroups);
            }
            else
            {
                await Clients.Client(id).SendAsync("UpdateList", activeGroups);
            }
        }

        // send text message
        public async Task SendText(string name, string message)
        {
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            var group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }
            
            await Clients.Caller.SendAsync("ReceiveText", name, message, "caller");
            await Clients.OthersInGroup(groupId).SendAsync("ReceiveText", name, message, "others");
            group.ChatHistory.Add(new Messages("ReceiveText", new object[] { name, message, "others" }));
        }

        // send image
        public async Task SendImage(string name, string image)
        {
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            var group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }

            await Clients.Caller.SendAsync("ReceiveImage", name, image, "caller");
            await Clients.OthersInGroup(groupId).SendAsync("ReceiveImage", name, image, "others");
            group.ChatHistory.Add(new Messages("ReceiveImage", new object[] { name, image, "others" }));
        }

        // send youtube
        public async Task SendYouTube(string name, string youtube)
        {
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            var group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }

            await Clients.Caller.SendAsync("ReceiveYouTube", name, youtube, "caller");
            await Clients.OthersInGroup(groupId).SendAsync("ReceiveYouTube", name, youtube, "others");
            group.ChatHistory.Add(new Messages("ReceiveYouTube", new object[] { name, youtube, "others" }));
        }

        public async Task SendDecoration(string name, string msg, string decoration)
        {
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            var group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }
            
            await Clients.Caller.SendAsync("ReceiveDecoration", name, msg, decoration, "caller");
            await Clients.OthersInGroup(groupId).SendAsync("ReceiveDecoration", name,  msg, decoration, "others");
            group.ChatHistory.Add(new Messages("ReceiveDecoration", new object[] { name,  msg, decoration, "others" }));
        }

        // send users list
        public async Task UpdateUsers(string groupId, List<string> users, string name)
        {
            await Clients.Caller.SendAsync("ReceiveUsers", users, "caller");
            await Clients.OthersInGroup(groupId).SendAsync("ReceiveUsers", name, "others");
        }

        // show message history
        public async Task ShowChatHistory(Group group)
        {
            await Clients.Caller.SendAsync("ReceiveChatHistory", group.ChatHistory);
        }

        // ==========================================
        // Connected
        // ==========================================
        public override async Task OnConnectedAsync()
        {
            string page = Context.GetHttpContext().Request.Query["page"];

            switch(page)
            {
                case "group": await GroupConnected();
                break;
                case "chat": await ChatConnected();
                break;
            }

            await base.OnConnectedAsync();
        }

        private async Task GroupConnected()
        {
            string id = Context.ConnectionId;
            await UpdateList(id);
        }

        private async Task ChatConnected()
        {
            string name = Context.GetHttpContext()?.Request.Query["name"] ?? "";
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            string id = Context.ConnectionId;

            Group group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }
            else
            {
                group.IncrementUsersCount(name);
            }

            await Groups.AddToGroupAsync(id, groupId);
            await Clients.Group(groupId).SendAsync("UpdateStatus", $"<b>{name} has joined</b", "joined", group.Count);
            await UpdateUsers(groupId, group.UserList, name);
            await Clients.Caller.SendAsync("ShowGroup", group);
            await ShowChatHistory(group);
            await UpdateList();
        }

        // ==========================================
        // Disconnected
        // ==========================================
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string page = Context.GetHttpContext().Request.Query["page"];

            switch(page)
            {
                case "group": GroupDisconnected();
                break;
                case "chat": await ChatDisconnected();
                break;
            }

            await base.OnDisconnectedAsync(exception);
        }

        private void GroupDisconnected()
        {
            // Do nothing
        }

        private async Task ChatDisconnected()
        {
            string id = Context.ConnectionId;
            string groupId = Context.GetHttpContext().Request.Query["groupId"];
            string name = Context.GetHttpContext()?.Request.Query["name"] ?? "";

            Group group = GroupList.Find(g => g.Id == groupId);

            if(group == null)
            {
                await Clients.Caller.SendAsync("Reject");
                return;
            }
            else
            {
                group.DecrementUsersCount(name);
            }

            if(group.NoUsers())
            {
                GroupList.Remove(group);
            }

            await Groups.RemoveFromGroupAsync(id, groupId);
            await Clients.Group(groupId).SendAsync("UpdateStatus", $"<b>{name} has left</b", "left", group.Count);
            await Clients.Group(groupId).SendAsync("ReceiveUsers", group.UserList, "remove");
            await UpdateList();
        }
    }