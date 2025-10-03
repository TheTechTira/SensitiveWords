document.addEventListener("DOMContentLoaded", function () {
    // Connect to the SignalR hub
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .build();

    // --- NEW: helpers for bubble UI ---
    const messagesList = document.getElementById("messages");
    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m]));
    }
    function addMessage({ text, user, isMe, time }) {
        const li = document.createElement("li");
        li.className = `msg ${isMe ? "me" : "them"}`;
        li.innerHTML = `
      <div class="bubble">
        <div class="text">${escapeHtml(text)}</div>
        <div class="meta">${isMe ? "You" : escapeHtml(user)} • ${time}</div>
      </div>`;
        messagesList.appendChild(li);
        li.scrollIntoView({ behavior: "smooth", block: "end" });
    }
    function nowHM() {
        return new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }
    // -----------------------------------

    connection.start().then(function () {
        let uniqueNumber;

        connection.invoke("GetUniqueNumber", connection.connectionId)
            .then(uniqueNum => {
                uniqueNumber = uniqueNum;
                console.log("Receive ID: ", uniqueNum);
                document.getElementById("UserId").innerText = "Your user ID for direct messages: " + uniqueNum;
            })
            .catch(err => console.log(err));

        console.log("Connected to SignalR hub. Connection ID: ", connection.connectionId);

        // --- CHANGED: nicer receive UI (uses bubbles) ---
        connection.on("SendMessageEvent", function (message, senderName) {
            const myName = document.getElementById("userName").value?.trim();
            const isMe = myName && senderName && myName.toLowerCase() === senderName.toLowerCase();
            addMessage({ text: message, user: senderName || "Unknown", isMe, time: nowHM() });
        });
        // -------------------------------------------------

        // Send a message to all users
        document.getElementById("sendToAllButton").addEventListener("click", function () {
            const messageInput = document.getElementById("messageInput");
            const userName = document.getElementById("userName");
            const message = messageInput.value;
            if (!message) return;
            connection.invoke("SendToAll", message, userName.value)
                .catch(err => console.error("Error sending message to all:", err));
            messageInput.value = "";
        });

        // Join a group
        document.getElementById("joinGroupButton").addEventListener("click", function () {
            const groupNameInput = document.getElementById("groupNameInput");
            const userName = document.getElementById("userName");
            alert(userName.value + " Joined the group");
            const groupName = groupNameInput.value;
            connection.invoke("JoinGroup", groupName)
                .catch(err => console.error("Error while joining group:", err));
        });

        document.getElementById("leaveGroupButton").addEventListener("click", function () {
            const groupNameInput = document.getElementById("groupNameInput");
            const userName = document.getElementById("userName");
            alert(userName.value + " Left the group");
            const groupName = groupNameInput.value;
            connection.invoke("LeaveGroup", groupName)
                .catch(err => console.error("Error while leaving group:", err));
        });

        // Send a message to a group
        document.getElementById("sendToGroupButton").addEventListener("click", function () {
            const groupName = document.getElementById("groupNameInput").value;
            const messageInput = document.getElementById("messageInput");
            const userName = document.getElementById("userName").value;
            const message = messageInput.value;
            if (!message || !groupName) return;
            connection.invoke("SendToGroup", groupName, message, userName)
                .catch(err => console.error("Error sending message to group:", err));
            messageInput.value = "";
        });

        // Send a message to a specific user
        document.getElementById("sendToUserButton").addEventListener("click", function () {
            const userId = document.getElementById("userIdInput").value;
            const userName = document.getElementById("userName").value;
            const message = document.getElementById("messageInput").value;
            if (!message || !userId) return;

            connection.invoke("SendToUser", userId, message, userName)
                .catch(err => console.error("Error sending message to user:", err));

            document.getElementById("messageInput").value = "";
        });

        // --- Optional: press Enter to send to All ---
        document.getElementById("messageInput").addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                document.getElementById("sendToAllButton").click();
            }
        });
    }).catch(err => console.error("Error connecting to SignalR hub:", err));
});
