using Microsoft.AspNetCore.SignalR;
using SensitiveWords.Domain.Dtos;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SensitiveWords.WebApp.Hubs
{
    public class ChatHub : Hub
    {
        // Store user connections (userId -> connectionId)
        private static readonly ConcurrentDictionary<string, string> _userConnections = new ConcurrentDictionary<string, string>();

        private readonly ILogger<ChatHub> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ChatHub(ILogger<ChatHub> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // When a user connects, store their connectionId with their userId
        public override Task OnConnectedAsync()
        {
            Random random = new Random();
            string uniqueNumber = random.Next(1000000000, int.MaxValue).ToString();
            _userConnections[Context.ConnectionId] = uniqueNumber;
            Console.WriteLine($"User connected: {uniqueNumber} with ConnectionId: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }

        // When a user disconnects, remove their connectionId
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            string userId = Context.UserIdentifier ?? Context.ConnectionId;
            _userConnections.TryRemove(userId, out _);
            Console.WriteLine($"User disconnected: {userId}");
            return base.OnDisconnectedAsync(exception);
        }
        // Send a message to a specific user by their userId
        public async Task SendToUser(string userId, string message, string userName)
        {
            // Find the connectionId where userId exists as a value
            var connectionEntry = _userConnections.FirstOrDefault(x => x.Value == userId);
            if (!string.IsNullOrEmpty(connectionEntry.Key))
            {
                await Clients.Client(connectionEntry.Key).SendAsync("SendMessageEvent", await SensitizedStringApi(message), userName);
            }
            else
            {
                await Clients.Caller.SendAsync("SendMessageEvent", "User not found.");
            }
        }

        public async Task<string> GetUniqueNumber(string connectionId)
        {
            var uniqueNumber = _userConnections[connectionId];
            return uniqueNumber.ToString();
        }

        // Send a message to a specific group
        public async Task SendToGroup(string groupName, string message, string userName)
        {
            await Clients.Group(groupName).SendAsync("SendMessageEvent", await SensitizedStringApi(message), userName);
        }

        // Send a message to all connected clients
        public async Task SendToAll(string message, string userName)
        {
            await Clients.All.SendAsync("SendMessageEvent", await SensitizedStringApi(message), userName);
        }
        // Join a group
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        // Leave a group
        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task<string> SensitizedStringApi(string message)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("SensitiveWords");

                // Serialize the action object to JSON
                var jsonContent = JsonSerializer.Serialize(new BloopRequestDto(message, true));

                // Create the StringContent object with the JSON data
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Post the JSON data to the specified endpoint
                var response = await client.PostAsync($"api/v1/messages/bloop", httpContent);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[SensitizedStringApi] Sensitize message error: {error}");
                    return $"{message} (Sensitized Failed)";
                }

                var data = JsonSerializer.Deserialize<BloopResponseDto>(await response.Content.ReadAsStringAsync());
                if (data == null)
                {
                    _logger.LogError($"[SensitizedStringApi] Sensitize message error: Response data is null");
                    return $"{message} (Sensitized Failed)";
                }

                _logger.LogInformation($"[SensitizedStringApi] Successfully sensitized message");
                return $"{data.Blooped}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SensitizedStringApi] Exception occurred while sensitizing message");
                return $"{message} (Sensitized Failed - Error '{ex.Message}')";
            }
        }
    }
}
