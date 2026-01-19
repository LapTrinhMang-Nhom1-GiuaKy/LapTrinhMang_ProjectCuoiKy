using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CaroLAN
{
    public partial class sanhCho : Form
    {
        ChessBoardManager chessBoard;
        private readonly SocketManager socket;
        Thread listenThread;
        private CancellationTokenSource cancellationTokenSource;
        private ServerDiscoveryClient? serverDiscovery;

        private string? currentRoomId;
        private bool isInRoom = false;
        private bool amFirst = false;
        private string username = string.Empty;
        private string password = string.Empty;

        private Dictionary<string, string> receivedInvitations;
        private Dictionary<string, DateTime> invitationTimestamps;
        
        private string myEndPoint;
        
        private Queue<string> pendingMessages;

        private static readonly Regex ClientListRegex = new Regex(@"^CLIENT_LIST:(.*)$", RegexOptions.Compiled);

        public sanhCho() : this(string.Empty, string.Empty, null, null)
        {
        }

        public sanhCho(string username, SocketManager? existingSocket) : this(username, string.Empty, existingSocket, null)
        {
        }

        public sanhCho(string username, string password, SocketManager? existingSocket) : this(username, password, existingSocket, null)
        {
        }

        public sanhCho(string username, string password, SocketManager? existingSocket, Queue<string>? pendingMsgs)
        {
            InitializeComponent();
            this.username = username;
            this.password = password;
            socket = existingSocket ?? new SocketManager();
            receivedInvitations = new Dictionary<string, string>();
            invitationTimestamps = new Dictionary<string, DateTime>();
            cancellationTokenSource = new CancellationTokenSource();
            serverDiscovery = new ServerDiscoveryClient();
            pendingMessages = pendingMsgs ?? new Queue<string>();

            FormClosing += sanhCho_FormClosing;

            btnConnect.Enabled = true;

            if (!string.IsNullOrEmpty(username))
            {
                Text = $"GameCaro - {username}";
            }

            if (socket.IsConnected)
            {
                lblStatus.Text = string.IsNullOrEmpty(username)
                    ? "Đã kết nối đến server"
                    : $"Đã kết nối - {username}";
                btnConnect.Text = "Ngắt kết nối";
                txtIP.Enabled = false;

                try
                {
                    string? remoteEndpoint = socket.GetServerEndPoint();
                    if (!string.IsNullOrEmpty(remoteEndpoint) && remoteEndpoint.Contains(':'))
                    {
                        txtIP.Text = remoteEndpoint.Split(':')[0];
                    }
                }
                catch
                {
                }

                myEndPoint = socket.GetLocalEndPoint();
                
                lobbyListening();
                
                Task.Delay(300).ContinueWith(_ => 
                {
                    try
                    {
                        if (socket.IsConnected)
                        {
                            socket.Send("GET_CLIENT_LIST");
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                });
            }
            else
            {
                lblStatus.Text = "Chưa kết nối";
                btnConnect.Text = "Kết nối";
                txtIP.Enabled = true;
                myEndPoint = string.Empty;
            }
        }

        private void btnConnect_Click_1(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            if (socket.IsConnected)
            {
                DisconnectFromServer();
            }
            else
            {
                ConnectToServer();
            }
        }

        private void lobbyListening()
        {
            // Hủy token cũ nếu có
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            listenThread = new Thread(() =>
            {
                int loopCount = 0;
                
                Thread.Sleep(100);
                
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        loopCount++;
                        
                        // Kiểm tra kết nối
                        if (!socket.IsConnected)
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Mất kết nối! Đang thử kết nối lại...";
                                lstClients.Items.Clear();
                                lstRequests.Items.Clear();
                                UpdateConnectionState(false);
                                
                                bool reconnected = TryReconnect();
                                if (!reconnected)
                                {
                                    btnAccept.Enabled = true;
                                    btnRequest.Enabled = true;
                                    btnConnect.Text = "Kết nối";
                                    btnConnect.Enabled = true;
                                    txtIP.Enabled = true;
                                    isInRoom = false;
                                    currentRoomId = null;
                                    MessageBox.Show("Mất kết nối đến server! Vui lòng kết nối lại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
                            }));
                            break;
                        }

                        string data;
                        lock (pendingMessages)
                        {
                            if (pendingMessages.Count > 0)
                            {
                                data = pendingMessages.Dequeue();
                            }
                            else
                            {
                                data = socket.Receive();
                            }
                        }
                        
                        if (string.IsNullOrEmpty(data))
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        if (data.StartsWith("Server đã nhận:"))
                        {
                            continue;
                        }

                        if (data.StartsWith("LOGIN_SUCCESS:"))
                        {
                            string[] parts = data.Split(':');
                            if (parts.Length >= 3)
                            {
                                string loggedInUsername = parts[2];
                                Invoke(new Action(() =>
                                {
                                    username = loggedInUsername;
                                    Text = $"GameCaro - {username}";
                                    lblStatus.Text = $"Đã đăng nhập lại: {username}";
                                    LoadHistory();
                                }));
                            }
                            continue;
                        }

                        if (data.StartsWith("LOGIN_FAILED:"))
                        {
                            string error = data.Substring("LOGIN_FAILED:".Length);
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = $"Đăng nhập lại thất bại: {error}";
                            }));
                            continue;
                        }

                        if (data.StartsWith("AUTH_REQUIRED:"))
                        {
                            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                            {
                                try
                                {
                                    socket.Send($"LOGIN:{username}:{password}");
                                }
                                catch
                                {
                                }
                            }
                            continue;
                        }

                        if (data == "SERVER_STOPPED")
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Server đã dừng!";
                                lstClients.Items.Clear();
                                lstRequests.Items.Clear();
                                btnConnect.Text = "Kết nối";
                                btnConnect.Enabled = true;
                                txtIP.Enabled = true;
                                UpdateConnectionState(false);
                                MessageBox.Show("Server đã dừng hoạt động!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                            break;
                        }

                        if (data.StartsWith("INVITATION_RECEIVED:"))
                        {
                            HandleInvitationReceived(data);
                        }

                        if (data.StartsWith("INVITATION_SENT:"))
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Lời mời đã được gửi!";
                            }));
                        }

                        if (data.StartsWith("INVITATION_REJECTED:"))
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Lời mời bị từ chối!";
                                MessageBox.Show("Đối thủ đã từ chối lời mời của bạn.", "Thông báo");
                            }));
                        }

                        if (data.StartsWith("INVITATION_ACCEPTED:"))
                        {
                            string[] parts = data.Split(':');
                            string invitationId = parts[1];
                            string roomId = parts[2];
                            string position = parts.Length > 3 ? parts[3] : "";

                            Invoke(new Action(() =>
                            {
                                RemoveInvitationFromList(invitationId);
                                currentRoomId = roomId;
                                isInRoom = true;
                                
                                amFirst = (position == "FIRST");
                                
                                string positionText = amFirst ? "Bạn đi trước (X)" : "Bạn đi sau (O)";
                                lblStatus.Text = $"Lời mời được chấp nhận. Vào phòng {roomId} - {positionText}";
                                StartGame();
                            }));
                        }


                        if (data.StartsWith("INVITATION_EXPIRED:"))
                        {
                            string id = data.Split(':')[1];

                            Invoke(new Action(() =>
                            {
                                RemoveInvitationFromList(id);
                                lblStatus.Text = "Một lời mời đã hết hạn.";
                            }));
                        }


                        if (data.StartsWith("INVITATION_CANCELLED:"))
                        {
                            string id = data.Split(':')[1];

                            Invoke(new Action(() =>
                            {
                                RemoveInvitationFromList(id);
                                lblStatus.Text = "Lời mời đã bị hủy bởi người gửi.";
                            }));
                        }


                        if (data.StartsWith("INVITATION_SEND_FAILED:"))
                        {
                            string[] parts = data.Split(':', 2);
                            string reason = parts.Length > 1 ? parts[1] : "Unknown error";
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Không thể gửi lời mời!";
                                MessageBox.Show($"Không thể gửi lời mời: {reason}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }

                        if (data.StartsWith("ROOM_JOINED:"))
                        {
                            string[] parts = data.Split(':');
                            if (parts.Length >= 3)
                            {
                                currentRoomId = parts[1];
                                int playerCount = int.Parse(parts[2]);
                                amFirst = (playerCount == 1);

                                Invoke(new Action(() =>
                                {
                                    isInRoom = true;
                                    lblStatus.Text = $"Đã tham gia phòng {currentRoomId} ({playerCount}/2 người chơi)";

                                    if (playerCount < 2)
                                    {
                                        lblStatus.Text += " - Đang chờ đối thủ...";
                                    }
                                }));
                            }
                        }

                        if (data == "GAME_START")
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = $"Trận đấu trong phòng {currentRoomId} đã bắt đầu!";
                                StartGame();
                            }));
                        }

                        if (data == "OPPONENT_LEFT")
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Đối thủ đã rời phòng!";
                                MessageBox.Show("Đối thủ đã rời phòng. Bạn sẽ quay lại sảnh chờ.", "Thông báo");
                                isInRoom = false;
                                currentRoomId = null;
                            }));
                        }

                        if (data.StartsWith("GAME_MOVE:"))
                        {
                            string moveData = data.Substring("GAME_MOVE:".Length);
                            string[] moveParts = moveData.Split(',');
                            if (moveParts.Length == 2 && int.TryParse(moveParts[0], out int x) && int.TryParse(moveParts[1], out int y))
                            {
                                Invoke(new Action(() =>
                                {
                                    chessBoard?.OtherPlayerMove(new Point(x, y));
                                }));
                            }
                        }

                        if (!isInRoom)
                        {
                            Match clientListMatch = ClientListRegex.Match(data);
                            if (clientListMatch.Success)
                            {
                                string clientData = clientListMatch.Groups[1].Value;
                                string[] clients = string.IsNullOrEmpty(clientData) 
                                    ? Array.Empty<string>() 
                                    : clientData.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                
                                Invoke(new Action(() =>
                                {
                                    UpdateClientList(clients);
                                }));
                                continue;
                            }
                        }

                        if (data.StartsWith("HISTORY_MY:"))
                        {
                            string historyData = data.Substring("HISTORY_MY:".Length);
                            Invoke(new Action(() =>
                            {
                                UpdateMyHistory(historyData);
                            }));
                        }

                        if (data == "ROOM_JOIN_FAILED")
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Không thể tham gia phòng! Đang thử kết nối lại...";
                                UpdateConnectionState(false);
                                
                                bool reconnected = TryReconnect();
                                
                                if (reconnected)
                                {
                                    lblStatus.Text = "Đã kết nối lại thành công!";
                                    Task.Delay(300).ContinueWith(_ => 
                                    {
                                        try
                                        {
                                            if (socket.IsConnected)
                                            {
                                                socket.Send("GET_CLIENT_LIST");
                                            }
                                        }
                                        catch { }
                                    });
                                }
                                else
                                {
                                    MessageBox.Show("Không thể tham gia phòng và không thể kết nối lại server.\n\nVui lòng kết nối lại thủ công.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    btnConnect.Text = "Kết nối";
                                    btnConnect.Enabled = true;
                                    txtIP.Enabled = true;
                                }
                            }));
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Kết nối bị gián đoạn!";
                                btnConnect.Text = "Kết nối";
                                btnConnect.Enabled = true;
                                txtIP.Enabled = true;
                                UpdateConnectionState(false);
                            }));
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Chỉ hiển thị lỗi nếu không phải do cancellation
                        if (!token.IsCancellationRequested)
                        {
                            Invoke(new Action(() =>
                            {
                                lblStatus.Text = "Lỗi không xác định! Đang thử kết nối lại...";
                                UpdateConnectionState(false);

                                // THỬ RECONNECT TỰ ĐỘNG
                                bool reconnected = TryReconnect();

                                if (reconnected)
                                {
                                    lblStatus.Text = "Đã kết nối lại sau lỗi!";
                                }
                                else
                                {
                                    // Reconnect thất bại → hiển thị lỗi
                                    MessageBox.Show(
                                        $"Lỗi khi nhận dữ liệu từ server:\n\n{ex.GetType().Name}\n{ex.Message}\n\nKhông thể kết nối lại. Vui lòng kết nối thủ công.",
                                        "Lỗi",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error
                                    );
                                    btnConnect.Text = "Kết nối";
                                    btnConnect.Enabled = true;
                                    txtIP.Enabled = true;
                                }
                            }));
                        }
                        break;
                    }
                }
                
                try
                {
                    Invoke(new Action(() =>
                    {
                        if (!socket.IsConnected)
                        {
                            btnConnect.Text = "Kết nối";
                            btnConnect.Enabled = true;
                            txtIP.Enabled = true;
                        }
                    }));
                }
                catch
                {
                }
            });

            listenThread.IsBackground = true;
            listenThread.Start();
        }

        private void HandleInvitationReceived(string data)
        {
            try
            {
                int firstColon = data.IndexOf(':');
                int secondColon = data.IndexOf(':', firstColon + 1);

                if (firstColon < 0 || secondColon < 0)
                    return;

                string invitationId = data.Substring(firstColon + 1, secondColon - firstColon - 1);
                string senderInfo = data.Substring(secondColon + 1);

                if (senderInfo == username || senderInfo == myEndPoint)
                    return;

                if (isInRoom)
                {
                    socket.Send($"REJECT_INVITATION:{invitationId}");
                    return;
                }

                Invoke(new Action(() =>
                {
                    if (receivedInvitations.ContainsKey(invitationId))
                        return;

                    receivedInvitations[invitationId] = senderInfo;
                    invitationTimestamps[invitationId] = DateTime.Now;

                    lstRequests.Items.Add($"{senderInfo} (ID: {invitationId})");
                    lblStatus.Text = $"Nhận lời mời từ {senderInfo}";
                }));
            }
            catch (Exception)
            {
            }
        }


        private void RemoveInvitationFromList(string invitationId)
        {
            if (receivedInvitations.ContainsKey(invitationId))
            {
                receivedInvitations.Remove(invitationId);
                invitationTimestamps.Remove(invitationId);
            }

            for (int i = 0; i < lstRequests.Items.Count; i++)
            {
                string? item = lstRequests.Items[i].ToString();

                if (!string.IsNullOrEmpty(item) && item.EndsWith($"(ID: {invitationId})"))
                {
                    lstRequests.Items.RemoveAt(i);
                    break;
                }
            }
        }


        private void UpdateClientList(string[] clients)
        {
            try
            {
                if (lstClients.InvokeRequired)
                {
                    lstClients.Invoke(new Action(() => UpdateClientList(clients)));
                    return;
                }
                
                lstClients.Items.Clear();

                if (clients.Length == 0 || string.IsNullOrEmpty(clients[0]))
                {
                    return;
                }

                string currentEndPoint = string.Empty;
                if (socket.IsConnected)
                {
                    currentEndPoint = socket.GetLocalEndPoint();
                    if (!string.IsNullOrEmpty(currentEndPoint) && currentEndPoint != "Not connected" && currentEndPoint != "Error")
                    {
                        myEndPoint = currentEndPoint;
                    }
                }

                List<string> availableClients = new List<string>();
                List<string> busyClients = new List<string>();

                foreach (string client in clients)
                {
                    if (string.IsNullOrWhiteSpace(client))
                        continue;

                    string cleanClient = client.Replace("|BUSY", "").Trim();

                    bool isMe = false;

                    if (!string.IsNullOrEmpty(username))
                    {
                        isMe = cleanClient.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                    
                    if (!isMe && !string.IsNullOrEmpty(currentEndPoint) && currentEndPoint != "Not connected" && currentEndPoint != "Error")
                    {
                        isMe = cleanClient.Equals(currentEndPoint.Trim(), StringComparison.OrdinalIgnoreCase);
                    }

                    if (!isMe && !string.IsNullOrEmpty(myEndPoint) && myEndPoint != "Not connected" && myEndPoint != "Error")
                    {
                        isMe = cleanClient.Equals(myEndPoint.Trim(), StringComparison.OrdinalIgnoreCase);
                    }

                    if (!isMe && !string.IsNullOrEmpty(currentEndPoint) && currentEndPoint.Contains(':'))
                    {
                        try
                        {
                            string myIP = currentEndPoint.Split(':')[0];
                            if (cleanClient.Contains(':'))
                            {
                                string clientIP = cleanClient.Split(':')[0];
                                if (myIP == clientIP && (myIP == "127.0.0.1" || myIP == "localhost" || myIP.StartsWith("192.168.") || myIP.StartsWith("10.")))
                                {
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (isMe)
                    {
                        continue;
                    }

                    if (client.Contains("|BUSY"))
                    {
                        busyClients.Add($"[BUSY] {cleanClient}");
                    }
                    else
                    {
                        availableClients.Add(cleanClient);
                    }
                }

                foreach (string client in availableClients)
                {
                    lstClients.Items.Add(client);
                }

                foreach (string client in busyClients)
                {
                    lstClients.Items.Add(client);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật danh sách: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void button3_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            if (!socket.IsConnected)
            {
                MessageBox.Show("Bạn chưa kết nối đến server!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (isInRoom)
            {
                MessageBox.Show("Bạn đã ở trong phòng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                socket.Send("JOIN_ROOM");
                lblStatus.Text = "Đang tìm phòng...";
                btnRequest.Enabled = false;
                btnAccept.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi yêu cầu tham gia phòng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartGame()
        {
            cancellationTokenSource?.Cancel();

            if (listenThread != null && listenThread.IsAlive)
            {
                if (!listenThread.Join(2000))
                {
                }
            }

            try
            {
                if (string.IsNullOrEmpty(currentRoomId))
                {
                    MessageBox.Show("Lỗi: Không có thông tin phòng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                Form1 gameForm = new Form1(currentRoomId, socket, amFirst);
                gameForm.FormClosed += (s, args) =>
                {
                    this.Show();
                    btnRequest.Enabled = true;
                    btnAccept.Enabled = true;

                    isInRoom = false;
                    currentRoomId = null;
                    lblStatus.Text = "Đã kết nối đến server";

                    lobbyListening();

                    Task.Delay(300).ContinueWith(_ => 
                    {
                        try
                        {
                            if (socket.IsConnected)
                            {
                                socket.Send("GET_CLIENT_LIST");
                                Invoke(new Action(() =>
                                {
                                    lblStatus.Text = "Đang cập nhật danh sách người chơi...";
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    });

                    System.Threading.Timer? historyUpdateTimer = null;
                    historyUpdateTimer = new System.Threading.Timer((state) =>
                    {
                        try
                        {
                            if (socket.IsConnected)
                            {
                                Invoke(new Action(() =>
                                {
                                    LoadHistory();
                                    lblStatus.Text = "Đã cập nhật lịch sử đấu";
                                }));
                            }
                        }
                        catch { }
                        finally
                        {
                            historyUpdateTimer?.Dispose();
                        }
                    }, null, 1000, System.Threading.Timeout.Infinite);
                };

                gameForm.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi bắt đầu game: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void sanhCho_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                SoundManager.StopMusic();
                
                if (socket.IsConnected)
                {
                    if (isInRoom)
                    {
                        try
                        {
                            socket.Send("LEAVE_ROOM");
                        }
                        catch { }
                    }

                    // Gửi tín hiệu ngắt kết nối
                    try
                    {
                        socket.Send("DISCONNECT");
                    }
                    catch { }
                }

                cancellationTokenSource?.Cancel();

                if (listenThread != null && listenThread.IsAlive)
                {
                    listenThread.Join(1000);
                }

                serverDiscovery?.StopDiscovery();

                socket.Disconnect();
            }
            catch
            {
            }
        }

        private void btnFindServers_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            try
            {
                lblStatus.Text = "🔍 Đang tìm server trong mạng LAN...";
                btnFindServers.Enabled = false;
                Application.DoEvents();

                List<DiscoveredServer> foundServers = new List<DiscoveredServer>();

                serverDiscovery?.StartDiscovery(
                    onServerFound: (server) =>
                    {
                        foundServers.Add(server);
                        Invoke(new Action(() =>
                        {
                            lblStatus.Text = $"🔍 Đã tìm thấy {foundServers.Count} server...";
                        }));
                    },
                    onDiscoveryComplete: (servers) =>
                    {
                        Invoke(new Action(() =>
                        {
                            btnFindServers.Enabled = true;

                            if (servers.Count == 0)
                            {
                                lblStatus.Text = "❌ Không tìm thấy server nào";
                                MessageBox.Show(
                                    "Không tìm thấy server trong mạng LAN.\n\n" +
                                    "Vui lòng đảm bảo:\n" +
                                    "✓ Server đã được bật\n" +
                                    "✓ Cả server và client trong cùng mạng LAN\n" +
                                    "✓ Firewall không chặn kết nối (port 9998 và 9999)",
                                    "Không tìm thấy server",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else if (servers.Count == 1)
                            {
                                txtIP.Text = servers[0].IPAddress;
                                lblStatus.Text = $"✅ Tìm thấy: {servers[0].ServerName}";
                                
                                var result = MessageBox.Show(
                                    $"Đã tìm thấy server:\n\n" +
                                    $"📌 Tên: {servers[0].ServerName}\n" +
                                    $"📍 Địa chỉ: {servers[0].IPAddress}:{servers[0].Port}\n\n" +
                                    $"Bạn có muốn kết nối ngay không?",
                                    "Tìm thấy server",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question);

                                if (result == DialogResult.Yes)
                                {
                                    ConnectToServer();
                                }
                            }
                            else
                            {
                                lblStatus.Text = $"✅ Tìm thấy {servers.Count} server";
                                ShowServerSelectionDialog(servers);
                            }
                        }));
                    }
                );
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ Lỗi khi tìm server";
                btnFindServers.Enabled = true;
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowServerSelectionDialog(List<DiscoveredServer> servers)
        {
            Form selectionForm = new Form
            {
                Text = "Chọn server để kết nối",
                Width = 450,
                Height = 350,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            Label lblInfo = new Label
            {
                Text = $"🎮 Tìm thấy {servers.Count} server trong mạng LAN:",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94)
            };

            ListBox lstServers = new ListBox
            {
                Location = new Point(20, 50),
                Width = 390,
                Height = 200,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.FixedSingle
            };

            foreach (var server in servers)
            {
                lstServers.Items.Add(server);
            }

            if (lstServers.Items.Count > 0)
            {
                lstServers.SelectedIndex = 0;
            }

            Button btnSelect = new Button
            {
                Text = "✅ Kết nối",
                Location = new Point(200, 265),
                Width = 100,
                Height = 35,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnSelect.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text = "❌ Hủy",
                Location = new Point(310, 265),
                Width = 100,
                Height = 35,
                DialogResult = DialogResult.Cancel,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            selectionForm.Controls.Add(lblInfo);
            selectionForm.Controls.Add(lstServers);
            selectionForm.Controls.Add(btnSelect);
            selectionForm.Controls.Add(btnCancel);

            selectionForm.AcceptButton = btnSelect;
            selectionForm.CancelButton = btnCancel;

            if (selectionForm.ShowDialog() == DialogResult.OK && lstServers.SelectedItem != null)
            {
                DiscoveredServer selected = (DiscoveredServer)lstServers.SelectedItem;
                txtIP.Text = selected.IPAddress;
                lblStatus.Text = $"✅ Đã chọn: {selected.ServerName}";
                
                ConnectToServer();
            }
            else
            {
                lblStatus.Text = "Đã hủy chọn server";
            }
        }

        private void sanhCho_Load(object sender, EventArgs e)
        {
            SoundManager.Initialize();
            SoundManager.PlayLobbyMusic();
            UpdateSoundButtonsText();
            
            UpdateConnectionState(socket.IsConnected);
            
            if (socket.IsConnected)
            {
                lblStatus.Text = string.IsNullOrEmpty(username)
                    ? "Đã kết nối đến server"
                    : $"Đã kết nối - {username}";
                btnConnect.Text = "Ngắt kết nối";
                txtIP.Enabled = false;
            }
            else
            {
                lblStatus.Text = "Chưa kết nối";
                btnConnect.Text = "Kết nối";
                txtIP.Enabled = true;
            }
        }

        private void UpdateConnectionState(bool isConnected)
        {
            button3.Enabled = isConnected;
            btnRequest.Enabled = isConnected;
            btnAccept.Enabled = isConnected;
            btnRefreshMy.Enabled = isConnected;
            lstClients.Enabled = isConnected;
            lstRequests.Enabled = isConnected;
            tabHistory.Enabled = isConnected;
            
            if (!isConnected)
            {
                lblStatus.Text = "⚪ Chưa kết nối - Vui lòng kết nối server";
                lstClients.Items.Clear();
                lstRequests.Items.Clear();
            }
        }

        private void ConnectToServer()
        {
            string serverIP = txtIP.Text.Trim();

            if (string.IsNullOrEmpty(serverIP))
            {
                MessageBox.Show("Vui lòng nhập địa chỉ IP server!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtIP.Focus();
                return;
            }

            try
            {
                lblStatus.Text = "Đang kết nối...";
                btnConnect.Enabled = false;
                Application.DoEvents();

                if (socket.ConnectToServer(serverIP))
                {
                    lblStatus.Text = $"Đã kết nối đến server {socket.GetServerEndPoint()}";
                    btnConnect.Text = "Ngắt kết nối";
                    btnConnect.Enabled = true;
                    txtIP.Enabled = false;
                    UpdateConnectionState(true);

                    myEndPoint = socket.GetLocalEndPoint();

                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        try
                        {
                            socket.Send($"LOGIN:{username}:{password}");
                            lblStatus.Text = "Đang đăng nhập lại...";
                        }
                        catch
                        {
                        }
                    }

                    lobbyListening();
                }
                else
                {
                    lblStatus.Text = "Không kết nối được server!";
                    btnConnect.Enabled = true;
                    UpdateConnectionState(false);
                    MessageBox.Show(
                        "Không thể kết nối đến server.\n\n",
                        "Lỗi kết nối",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi kết nối!";
                btnConnect.Enabled = true;
                UpdateConnectionState(false);
                MessageBox.Show($"Lỗi khi kết nối: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisconnectFromServer()
        {
            try
            {
                btnConnect.Enabled = false;
                lblStatus.Text = "Đang ngắt kết nối...";
                Application.DoEvents();

                if (isInRoom)
                {
                    try
                    {
                        socket.Send("LEAVE_ROOM");
                    }
                    catch
                    {
                    }
                    isInRoom = false;
                    currentRoomId = null;
                }

                try
                {
                    socket.Send("DISCONNECT");
                    Thread.Sleep(100);
                }
                catch
                {
                }

                cancellationTokenSource?.Cancel();

                if (listenThread != null && listenThread.IsAlive)
                {
                    if (!listenThread.Join(2000))
                    {
                    }
                }

                socket.Disconnect();

                lblStatus.Text = "Đã ngắt kết nối khỏi server";
                lstClients.Items.Clear();
                lstRequests.Items.Clear();
                btnConnect.Text = "Kết nối";
                btnConnect.Enabled = true;
                txtIP.Enabled = true;
                UpdateConnectionState(false);
            }
            catch (Exception ex)
            {
                btnConnect.Enabled = true;
                MessageBox.Show($"Lỗi khi ngắt kết nối: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRequest_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            if (!socket.IsConnected)
            {
                MessageBox.Show("Bạn chưa kết nối đến server!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (isInRoom)
            {
                MessageBox.Show("Bạn đang trong phòng chơi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (lstClients.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một người chơi để mời!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string? selectedClient = lstClients.SelectedItem.ToString();
                
                if (string.IsNullOrEmpty(selectedClient))
                {
                    MessageBox.Show("Không thể lấy thông tin người chơi!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                if (selectedClient.StartsWith("[BUSY]"))
                {
                    MessageBox.Show("Người chơi này đang bận!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string cleanClientName = selectedClient.Replace("[BUSY] ", "").Trim();
                
                socket.Send($"SEND_INVITATION:{cleanClientName}");
                lblStatus.Text = $"Đang gửi lời mời đến {cleanClientName}...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi lời mời: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            if (lstRequests.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một lời mời để chấp nhận!");
                return;
            }

            string? selected = lstRequests.SelectedItem.ToString();
            
            if (string.IsNullOrEmpty(selected))
            {
                MessageBox.Show("Không thể lấy thông tin lời mời!");
                return;
            }

            int index = selected.IndexOf("(ID: ");
            if (index < 0) return;

            string invitationId = selected
                .Substring(index + 5)
                .TrimEnd(')');

            if (!receivedInvitations.ContainsKey(invitationId))
            {
                MessageBox.Show("Lời mời không còn hợp lệ!");
                RemoveInvitationFromList(invitationId);
                return;
            }

            try
            {
                socket.Send($"ACCEPT_INVITATION:{invitationId}");
                lblStatus.Text = "Đang chấp nhận lời mời...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chấp nhận lời mời: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

        }


        private void lstRequests_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void UpdateMyHistory(string historyData)
        {
            dgvMyHistory.Rows.Clear();
            
            if (string.IsNullOrEmpty(historyData))
            {
                lblHistoryStats.Text = "Tổng: 0 trận | Thắng: 0 | Thua: 0 | Hòa: 0";
                lblStatus.Text = "Đã cập nhật lịch sử game";
                return;
            }

            string[] matches = historyData.Split(';', StringSplitOptions.RemoveEmptyEntries);
            int totalMatches = 0;
            int wins = 0;
            int losses = 0;
            int draws = 0;
            
            foreach (string match in matches)
            {
                string[] parts = match.Split('|');
                if (parts.Length >= 7)
                {
                    string roomId = parts[1];
                    string player1 = parts[2];
                    string player2 = parts[3];
                    string winner = parts[4];
                    string startedAt = parts[5];
                    string endedAt = parts[6];

                    string opponent = (player1 == username) ? player2 : player1;
                    
                    string result;
                    Color resultColor;
                    if (winner == username)
                    {
                        result = "THẮNG";
                        resultColor = Color.FromArgb(46, 204, 113); // Xanh lá
                        wins++;
                    }
                    else if (winner == "Hòa")
                    {
                        result = "HÒA";
                        resultColor = Color.FromArgb(149, 165, 166); // Xám
                        draws++;
                    }
                    else
                    {
                        result = "THUA";
                        resultColor = Color.FromArgb(231, 76, 60); // Đỏ
                        losses++;
                    }

                    string displayTime = endedAt;
                    try
                    {
                        if (DateTime.TryParse(endedAt, out DateTime dt))
                        {
                            displayTime = dt.ToString("dd/MM/yyyy HH:mm");
                        }
                    }
                    catch
                    {
                    }

                    int rowIndex = dgvMyHistory.Rows.Add(roomId, opponent, result, displayTime);
                    
                    dgvMyHistory.Rows[rowIndex].Cells[2].Style.ForeColor = resultColor;
                    dgvMyHistory.Rows[rowIndex].Cells[2].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    
                    totalMatches++;
                }
            }

            lblHistoryStats.Text = $"Tổng: {totalMatches} trận | Thắng: {wins} | Thua: {losses} | Hòa: {draws}";
            
            StyleDataGridView();
            
            lblStatus.Text = "Đã cập nhật lịch sử game";
        }

        private void StyleDataGridView()
        {
            dgvMyHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvMyHistory.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvMyHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvMyHistory.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMyHistory.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            
            dgvMyHistory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
            dgvMyHistory.RowsDefaultCellStyle.BackColor = Color.White;
            
            dgvMyHistory.RowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvMyHistory.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            
            dgvMyHistory.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvMyHistory.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
            dgvMyHistory.DefaultCellStyle.Padding = new Padding(5);
            dgvMyHistory.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            
            if (dgvMyHistory.Columns.Count > 2)
            {
                dgvMyHistory.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void btnRefreshMy_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            
            if (!socket.IsConnected)
            {
                MessageBox.Show("Bạn chưa kết nối đến server!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Bạn cần đăng nhập để xem lịch sử của mình!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                socket.Send("GET_MY_HISTORY");
                lblStatus.Text = "Đang tải lịch sử đấu của bạn...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi yêu cầu lịch sử: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Lỗi tải lịch sử";
            }
        }

        private void LoadHistory()
        {
            if (!socket.IsConnected) return;

            try
            {
                if (!string.IsNullOrEmpty(username))
                {
                    socket.Send("GET_MY_HISTORY");
                }
            }
            catch (Exception ex)
            {
            }
        }

        private bool TryReconnect()
        {
            try
            {
                string serverIP = txtIP.Text.Trim();
                if (string.IsNullOrEmpty(serverIP))
                {
                    return false;
                }
                
                socket.Disconnect();
                Thread.Sleep(500);
                
                if (socket.ConnectToServer(serverIP))
                {
                    lblStatus.Text = "Đã kết nối lại! Đang đăng nhập...";
                    UpdateConnectionState(true);
                    
                    if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                    {
                        Thread.Sleep(200);
                        socket.Send($"LOGIN:{username}:{password}");
                        Thread.Sleep(300);
                    }
                    
                    lobbyListening();
                    
                    lblStatus.Text = "Đã kết nối lại thành công!";
                    return true;
                }
                
                UpdateConnectionState(false);
                return false;
            }
            catch (Exception ex)
            {
                UpdateConnectionState(false);
                return false;
            }
        }

        private void UpdateSoundButtonsText()
        {
            btnToggleMusic.Text = SoundManager.MusicEnabled ? "🎵 Nhạc: BẬT" : "🔇 Nhạc: TẮT";
            btnToggleSfx.Text = SoundManager.SfxEnabled ? "🔊 SFX: BẬT" : "🔈 SFX: TẮT";
        }

        private void btnToggleMusic_Click(object sender, EventArgs e)
        {
            SoundManager.PlayClickSound();
            SoundManager.ToggleMusic();
            UpdateSoundButtonsText();
            
            if (SoundManager.MusicEnabled)
            {
                SoundManager.PlayLobbyMusic();
            }
        }

        private void btnToggleSfx_Click(object sender, EventArgs e)
        {
            SoundManager.ToggleSfx();
            UpdateSoundButtonsText();
            
            if (SoundManager.SfxEnabled)
            {
                SoundManager.PlayClickSound();
            }
        }
    }
}
