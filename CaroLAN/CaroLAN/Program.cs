using CaroLAN.Managers;
namespace CaroLAN
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using Forms.LoginForm loginForm = new Forms.LoginForm();
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                SocketManager socket = loginForm.GetSocket();
                string username = loginForm.GetUsername();
                string password = loginForm.GetPassword();
                Queue<string> pendingMessages = loginForm.GetPendingMessages();

                Application.Run(new Forms.sanhCho(username, password, socket, pendingMessages));
            }
        }
    }
}