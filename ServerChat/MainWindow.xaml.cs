using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ServerChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpListener server;
        private bool isRunning = false;
        private NetworkStream stream;
        private List<TcpClient> clients = new List<TcpClient>();
        private List<string> clientNames = new();
        private byte[] lastSelectedEmojiBytes;
        public MainWindow()
        {
            InitializeComponent();
            txtChatLog.Document.LineHeight = 1;
            lstClients.Items.Clear();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                StartServer(13000);
                border_btnStart.Background = Brushes.Red;
                btnStart.Foreground = Brushes.White;
                btnStart.Content = "Stop Server";
            }
            else
            {
                StopServer();
                border_btnStart.Background = Brushes.LightGreen;
                btnStart.Content = "Start Server";
            }
        }

        private void StopServer()
        {
            isRunning = false;
            try
            {
                foreach (var client in clients)
                {
                    client.Close();
                }
                clients.Clear();
                clientNames.Clear();

                Dispatcher.Invoke(() =>
                {
                    lstClients.Items.Clear();
                });

                server.Stop();
            }
            catch { }
            AppendLog("Server stopped.");
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            // Lấy nội dung text từ RichTextBox
            TextRange textRange = new TextRange(txtMessage.Document.ContentStart, txtMessage.Document.ContentEnd);
            string displayMessage = textRange.Text.Trim();

            // Lấy emoji được chọn
            string emojiPart = lastSelectedEmojiBytes != null && lastSelectedEmojiBytes.Length > 0
                ? Encoding.UTF8.GetString(lastSelectedEmojiBytes)
                : "";

            // Ghép message + emoji
            string finalMessage = displayMessage + emojiPart;

            if (string.IsNullOrWhiteSpace(finalMessage))
                return;

            if (clients == null || clients.Count == 0)
            {
                MessageBox.Show("No clients connected. Please start server and wait for clients to connect.",
                                "Connection Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Gửi message từ server
                byte[] data = Encoding.UTF8.GetBytes($"[Server]: {finalMessage}");

                foreach (var client in clients)
                {
                    try
                    {
                        NetworkStream s = client.GetStream();
                        s.Write(data, 0, data.Length);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Failed to send to client: {ex.Message}", Brushes.OrangeRed);
                    }
                }
                AppendLog($"[Server]: {finalMessage}");
                lastSelectedEmojiBytes = null;
                txtMessage.Document.Blocks.Clear();
            }
            catch (Exception ex)
            {
                AppendLog("Send error: " + ex.Message, Brushes.Red);
            }
        }


        private void StartServer(int port)
        {
            try
            {
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                isRunning = true;

                AppendLog("Server started. Waiting for clients...", Brushes.Green);

                // Received connection from client
                Thread acceptThread = new Thread(() =>
                {
                    while (isRunning)
                    {
                        try
                        {
                            TcpClient client = server.AcceptTcpClient();
                            Thread clientThread = new Thread(() => HandleClient(client));
                            clientThread.IsBackground = true;
                            clientThread.Start();
                        }
                        catch (SocketException)
                        {
                            // Server stopped
                            break;
                        }
                    }
                });
                acceptThread.IsBackground = true;
                acceptThread.Start();
            }
            catch (Exception ex)
            {
                AppendLog("Error starting server: " + ex.Message, Brushes.Red);
            }
        }

        private void HandleClient(TcpClient client)
        {
            //AppendLog($"Client trying to connect: {client.Client.RemoteEndPoint}", Brushes.Gray);
            string clientName = null;
            try
            {
                NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                // Get name
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                clientName = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                // Thêm client & tên vào danh sách
                clients.Add(client);
                clientNames.Add(clientName);

                // Hiển thị tên
                Dispatcher.Invoke(() =>
                {
                    lstClients.Items.Clear();
                    foreach (var name in clientNames)
                    {
                        lstClients.Items.Add(name);
                    }
                    Dispatcher.Invoke(() =>
                    {
                        txtClientCount.Text = $"Total clients: {clients.Count}";
                    });
                });

                while (isRunning && client.Connected)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    //// Resend message to all clients (broadcast)
                    foreach (var c in clients)
                    {
                        if (c == client) continue; // Bypass the client sending the message
                        try
                        {
                            NetworkStream s = c.GetStream();
                            byte[] data = Encoding.UTF8.GetBytes($"[{clientName}]: {message}");
                            s.Write(data, 0, data.Length);
                        }
                        catch { }
                    }

                    // Received message from client
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog($"[{clientName}]: {message}");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendLog("Client error: " + ex.Message, Brushes.Red));
            }
            finally
            {
                // Client ngắt kết nối
                clients.Remove(client);
                clientNames.Remove(clientName);

                Dispatcher.Invoke(() =>
                {
                    lstClients.Items.Clear();
                    foreach (var name in clientNames)
                    {

                        lstClients.Items.Add(name);
                    }
                    Dispatcher.Invoke(() =>
                    {
                        txtClientCount.Text = $"Total clients: {clients.Count}";
                    });
                    AppendLog($"Client disconnected: {clientName}", Brushes.Red);
                });

                client.Close();
            }
        }

        //private void AppendLog(string message, SolidColorBrush color = null)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        // Create a paragraph for the message
        //        Paragraph paragraph = new Paragraph();

        //        // Determine if this is the user's own message
        //        bool isOwnMessage = message.StartsWith("[You]:");

        //        // Create the emoji text block for the message
        //        var emojiText = new Emoji.Wpf.TextBlock
        //        {
        //            Text = message,
        //            Foreground = color ?? Brushes.Black,
        //            TextWrapping = TextWrapping.Wrap,
        //            MaxWidth = 500  // Limit width to enable wrapping
        //        };

        //        // Create a border around the message
        //        Border messageBorder = new Border
        //        {
        //            Background = isOwnMessage ? Brushes.LightBlue : Brushes.White,
        //            CornerRadius = new CornerRadius(8),
        //            Padding = new Thickness(10, 5, 10, 5),
        //            Margin = new Thickness(5),
        //            Child = emojiText
        //        };

        //        // Add the border to an InlineUIContainer
        //        InlineUIContainer container = new InlineUIContainer(messageBorder);

        //        // Add alignment to the paragraph
        //        paragraph.TextAlignment = isOwnMessage ? TextAlignment.Right : TextAlignment.Left;
        //        paragraph.Margin = new Thickness(isOwnMessage ? 100 : 0, 5, isOwnMessage ? 0 : 100, 5);
        //        paragraph.Inlines.Add(container);

        //        // Add the paragraph to the RichTextBox
        //        txtChatLog.Document.Blocks.Add(paragraph);
        //        txtChatLog.ScrollToEnd();
        //    });
        //}


        private void btnEmoji_Click(object sender, RoutedEventArgs e)
        {
            // Toggle emoji popup
            emojiPopup.IsOpen = !emojiPopup.IsOpen;
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            string? emoji = null;

            // if Content is Emoji.Wpf.TextBlock then assign .Text
            if (button.Content is Emoji.Wpf.TextBlock emojiTextBlock)
            {
                emoji = emojiTextBlock.Text;
            }
            // if Content is string then assign directly
            else if (button.Content is string directEmoji)
            {
                emoji = directEmoji;
            }

            if (!string.IsNullOrEmpty(emoji))
            {
                byte[] emojiBytes = Encoding.UTF8.GetBytes(emoji);

                if (lastSelectedEmojiBytes != null)
                {
                    // Nối mảng byte
                    byte[] combined = new byte[lastSelectedEmojiBytes.Length + emojiBytes.Length];
                    Buffer.BlockCopy(lastSelectedEmojiBytes, 0, combined, 0, lastSelectedEmojiBytes.Length);
                    Buffer.BlockCopy(emojiBytes, 0, combined, lastSelectedEmojiBytes.Length, emojiBytes.Length);
                    lastSelectedEmojiBytes = combined;
                }
                else
                {
                    lastSelectedEmojiBytes = emojiBytes;
                }

                txtMessage.Focus();
                txtMessage.CaretPosition = txtMessage.Document.ContentEnd;
                txtMessage.CaretPosition.InsertTextInRun(emoji);
            }

            emojiPopup.IsOpen = false;
        }
        private void AppendLog(string message, SolidColorBrush color = null)
        {
            Dispatcher.Invoke(() =>
            {
                // Create a paragraph for the message
                Paragraph paragraph = new Paragraph();

                // Determine if this is the user's own message
                bool isOwnMessage = message.StartsWith("[You]:");

                // Create the emoji text block for the message
                var emojiText = new Emoji.Wpf.TextBlock
                {
                    Text = message,
                    Foreground = color ?? Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 500  // Limit width to enable wrapping
                };

                // Create a border around the message
                Border messageBorder = new Border
                {
                    Background = isOwnMessage ? Brushes.LightBlue : Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(5),
                    Child = emojiText
                };

                // Add the border to an InlineUIContainer
                InlineUIContainer container = new InlineUIContainer(messageBorder);

                // Add alignment to the paragraph
                paragraph.TextAlignment = isOwnMessage ? TextAlignment.Right : TextAlignment.Left;
                paragraph.Margin = new Thickness(isOwnMessage ? 100 : 0, 5, isOwnMessage ? 0 : 100, 5);
                paragraph.Inlines.Add(container);

                // Add the paragraph to the RichTextBox
                txtChatLog.Document.Blocks.Add(paragraph);
                txtChatLog.ScrollToEnd();
            });
        }
    }
}