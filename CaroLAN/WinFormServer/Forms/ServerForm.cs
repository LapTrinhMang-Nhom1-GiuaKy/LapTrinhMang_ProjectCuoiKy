using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinFormServer.Managers;

namespace WinFormServer.Forms
{
    public partial class ServerForm : Form
    {
        ServerSocketManager? socket;
        UserManager? userManager;
        BroadcastDiscovery? broadcastDiscovery;

        private const string DB_SERVER = "localhost";
        private const string DB_DATABASE = "gamecaro";
        private const string DB_USER = "root";
        private const string DB_PASSWORD = "27072004";

        public ServerForm()
        {
            InitializeComponent();

            // Kiểm tra và tạo database nếu chưa có
            LogToTextBox("Đang kiểm tra database...");
            bool dbInitialized = false;

            try
            {
                dbInitialized = UserManager.InitializeDatabase(DB_SERVER, DB_DATABASE, DB_USER, DB_PASSWORD, LogToTextBox);
            }
            catch (Exception ex)
            {
                LogToTextBox($"Lỗi khi khởi tạo database: {ex.Message}");
            }


            if (dbInitialized)
            {
                // Khởi tạo UserManager
                userManager = new UserManager(DB_SERVER, DB_DATABASE, DB_USER, DB_PASSWORD);
                socket = new ServerSocketManager(userManager);

                // Khởi tạo broadcast discovery
                broadcastDiscovery = new BroadcastDiscovery("GameCaro Server", ServerSocketManager.PORT);

                LogToTextBox("Server đã sẵn sàng. Nhấn 'Bat server' để bắt đầu server.");
            }
            else
            {
                LogToTextBox("Lỗi: Không thể khởi tạo database. Vui lòng kiểm tra kết nối MySQL.");
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                socket?.CreateServer(LogToTextBox, UpdateClientList);

                // Bắt đầu broadcast khi server chạy
                broadcastDiscovery?.Start();
                LogToTextBox("Broadcast discovery đã được bật - Client có thể tự động tìm server!");

                lblStatus.Text = "Đang chờ kết nối...";
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                button1.Enabled = true;
                button2.Enabled = true;
            }
            catch (Exception ex)
            {
                LogToTextBox($"Lỗi khi bật server: {ex.Message}");
                MessageBox.Show("Không thể bật server. Vui lòng kiểm tra PORT hoặc Firewall.");
            }
        }


        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                // Dừng broadcast trước
                broadcastDiscovery?.Stop();
                LogToTextBox("Broadcast discovery đã được tắt.");

                socket?.stopServer(LogToTextBox);

                lblStatus.Text = "Server đã dừng.";
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
            }
            catch (Exception ex)
            {
                LogToTextBox($"Lỗi khi dừng server: {ex.Message}");
                MessageBox.Show("Có lỗi xảy ra khi dừng server!");
            }
        }


        private void LogToTextBox(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(() =>
                {
                    txtLog.AppendText(message + Environment.NewLine);
                }));
            }
            else
            {
                txtLog.AppendText(message + Environment.NewLine);
            }
        }


        private void UpdateClientList()
        {
            if (lstClients.InvokeRequired)
            {
                lstClients.Invoke(new Action(UpdateClientList));
                return;
            }

            //LogToTextBox("Cập nhật danh sách client...");
            List<string> connectedClients = socket?.GetConnectedClients() ?? new List<string>();
            //LogToTextBox($"Number of connected clients: {connectedClients.Count}");

            // Cập nhật lstClients trên giao diện
            lstClients.BeginUpdate();
            lstClients.Items.Clear();
            foreach (string client in connectedClients)
            {
                lstClients.Items.Add(client);
            }
            lstClients.EndUpdate();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateClientList();
                LogToTextBox("Cập nhật danh sách client...");
                List<string> connectedClients = socket?.GetConnectedClients() ?? new List<string>();
                LogToTextBox($"Số client đang kết nối: {connectedClients.Count}");
            }
            catch (Exception ex)
            {
                LogToTextBox($"Lỗi khi cập nhật danh sách client: {ex.Message}");
            }
        }

        private void btnCloseConection_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItem != null)
            {
                try
                {
                    string? selectedClient = lstClients.SelectedItem?.ToString();
                    if (selectedClient != null)
                    {
                        socket?.DisconnectClient(selectedClient, LogToTextBox);
                    }
                    UpdateClientList();
                }
                catch (Exception ex)
                {
                    LogToTextBox($"Lỗi khi ngắt client: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một client để ngắt kết nối.");
            }
        }
    }
}
