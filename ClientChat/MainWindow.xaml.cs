using System.Net.Sockets;
using System.Text;
using System.Windows;
using Emoji.Wpf;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;


namespace ClientChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private string userName;
        private byte[] lastSelectedEmojiBytes;
        public MainWindow()
        {
            InitializeComponent();
            txtChat.Document.LineHeight = 1;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            userName = MyTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("Please enter your name.");
                return;
            }

            try
            {
                client = new TcpClient("192.168.149.1", 13000);
                //client = new TcpClient();
                //client.Connect("192.168.149.1", 13000);
                stream = client.GetStream();


                // Send username to server
                byte[] nameData = Encoding.UTF8.GetBytes(userName);
                stream.Write(nameData, 0, nameData.Length);

                // Start receiving messages
                AppendMessage($"Connect successfully", Brushes.Green);
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection error: " + ex.Message);
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytes;

                while ((bytes = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytes);
                    Dispatcher.Invoke(() =>
                    {
                        AppendMessage($"{message}");
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    AppendMessage("Receive error: " + ex.Message, Brushes.Red);
                });
            }
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check name and connection
                if (client == null || !client.Connected || stream == null)
                {
                    MessageBox.Show("Please enter your name !", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Lấy nội dung tin nhắn và emoji
                TextRange textRange = new TextRange(txtMessage.Document.ContentStart, txtMessage.Document.ContentEnd);
                string displayMessage = textRange.Text.Replace("\r", "").Replace("\n", "");

                string emojiPart = lastSelectedEmojiBytes != null && lastSelectedEmojiBytes.Length > 0
                    ? Encoding.UTF8.GetString(lastSelectedEmojiBytes)
                    : "";

                string finalMessage = displayMessage + emojiPart;

                if (string.IsNullOrWhiteSpace(finalMessage)) return;

                byte[] data = Encoding.UTF8.GetBytes(finalMessage);

                // Reset emoji buffer
                lastSelectedEmojiBytes = null;

                // Gửi dữ liệu
                stream.Write(data, 0, data.Length);
                AppendMessage($"[You]: {finalMessage}");

                // Xóa nội dung tin nhắn
                txtMessage.Document.Blocks.Clear();
            }
            catch (Exception ex)
            {
                AppendMessage("Send error: " + ex.Message, Brushes.Red);
            }
        }


        private void AppendMessage(string message, SolidColorBrush color = null)
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
                txtChat.Document.Blocks.Add(paragraph);
                txtChat.ScrollToEnd();
            });
        }

        private void btnEmoji_Click(object sender, RoutedEventArgs e)
        {
            // Toggle emoji popup
            emojiPopup.IsOpen = !emojiPopup.IsOpen;
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            string? emoji = null;

            // if Content is Emoji.Wpf.TextBlock then get .Text
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
    }
}