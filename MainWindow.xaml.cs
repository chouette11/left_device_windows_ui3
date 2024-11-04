using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using QRCoder;
using System.Text.Json;
using WindowsInput.Native;
using WindowsInput;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Storage.Streams;
using System.Text;
using System.Net.NetworkInformation;
using System.Drawing;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Microsoft.UI;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private TcpListener? _tcpListener;
        private Thread? _listenerThread;
        private CancellationTokenSource? _cancellationTokenSource;
        private static InputSimulator sim = new InputSimulator();
        static byte cnt = 0;
        [DllImport("user32.dll")]
        static extern int GetDpiForWindow(IntPtr hWnd);

        public MainWindow()
        {
            this.InitializeComponent();
            CheckStartupTaskStatus();

            this.Title = "左手デバイス";
            nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            double DefaultPixelsPerInch = 96D;
            double dpiScale = GetDpiForWindow(windowHandle) / DefaultPixelsPerInch; AppWindow.ResizeClient(new SizeInt32(
                (int)(500 * dpiScale),
                (int)(300 * dpiScale)));
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(
           Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
           new Microsoft.UI.Dispatching.DispatcherQueueHandler(() =>
           {
               MainWindow_Activated();
           }));

        }

        private async void LaunchOnStartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                LaunchOnStartupToggle.IsEnabled = false; // トグルの操作を一時的に無効化
                await ToggleLaunchOnStartup(LaunchOnStartupToggle.IsOn);
            }
            catch (Exception ex)
            {
                // エラーが発生した場合にログを出力
                ContentDialog errorDialog = new ContentDialog()
                {
                    Title = "Error",
                    Content = $"An error occurred: {ex.Message}",
                    CloseButtonText = "OK"
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                LaunchOnStartupToggle.IsEnabled = true; // 操作を再度有効化
            }
        }

        // 起動時にスタートアップタスクの状態を確認し、トグルボタンの状態を更新
        private async void CheckStartupTaskStatus()
        {
            var startupTask = await StartupTask.GetAsync("LaunchOnStartupTaskId");
            UpdateToggleState(startupTask.State);
        }

        // トグルボタンの状態を設定
        private void UpdateToggleState(StartupTaskState state)
        {
            LaunchOnStartupToggle.IsEnabled = true;
            switch (state)
            {
                case StartupTaskState.Enabled:
                    LaunchOnStartupToggle.IsOn = true;
                    break;
                case StartupTaskState.Disabled:
                case StartupTaskState.DisabledByUser:
                    LaunchOnStartupToggle.IsOn = false;
                    break;
                default:
                    LaunchOnStartupToggle.IsEnabled = false;
                    break;
            }
        }

        private async Task ToggleLaunchOnStartup(bool enable)
        {
            try
            {
                var startupTask = await StartupTask.GetAsync("LaunchOnStartupTaskId");
                switch (startupTask.State)
                {
                    case StartupTaskState.Enabled when !enable:
                        startupTask.Disable();
                        break;
                    case StartupTaskState.Disabled when enable:
                        var updatedState = await startupTask.RequestEnableAsync();
                        UpdateToggleState(updatedState);
                        break;
                    case StartupTaskState.DisabledByUser when enable:
                      
                        break;
                    default:
                      
                        break;
                }
            }
            catch (Exception ex)
            {
                // 例外のキャッチとエラーハンドリング
                ContentDialog errorDialog = new ContentDialog()
                {
                    Title = "Error",
                    Content = $"An unexpected error occurred: {ex.Message}",
                    CloseButtonText = "OK"
                };
                await errorDialog.ShowAsync();
            }
        }

        private void MainWindow_Activated()
        {
            InitializeBluetoothAsync();
            string ipv4Address = GetLocalIPv4();
            if (!string.IsNullOrEmpty(ipv4Address))
            {
                StartTcpServer(ipv4Address);
                GenerateQRCode(ipv4Address + "_a_windows");
            }
            else
            {
                
            }
        }

        private void GenerateQRCode(string qrText)
        {
            try
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                qrCode.ToString();

                Bitmap qrBitmap = qrCode.GetGraphic(10);

                Bitmap resizedQrBitmap = new Bitmap(qrBitmap, new System.Drawing.Size(200, 200));

                BitmapImage bitmapImage = new BitmapImage();

                // QrBitmapをImageSourceに変換

                bitmapImage = ConvertBitmapToBitmapImage(resizedQrBitmap);

                myImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("QRコード生成中にエラーが発生しました: " + ex.Message);
            }
        }

        public BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Bitmapをメモリーストリームに保存
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

                // メモリーストリームの位置を0にリセット
                memoryStream.Position = 0;

                // BitmapImageを作成
                var bitmapImage = new BitmapImage();

                // IRandomAccessStreamから直接読み込む代替方法
                using (var randomAccessStream = new InMemoryRandomAccessStream())
                {
                    using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
                    {
                        var writer = new DataWriter(outputStream);
                        writer.WriteBytes(memoryStream.ToArray());
                        writer.StoreAsync().GetAwaiter().GetResult();
                        writer.FlushAsync().GetAwaiter().GetResult();
                    }

                    // BitmapImageにストリームを設定
                    bitmapImage.SetSource(randomAccessStream);
                }

                return bitmapImage;
            }
        }


        private async Task InitializeBluetoothAsync()
        {
            var gattServiceProviderResult = await GattServiceProvider.CreateAsync(new Guid("BDFA3AEB-13E6-4C45-881E-83B108C913C1")).AsTask();
            if (gattServiceProviderResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("GATTサービスの起動に失敗 (Bluetooth LEデバイスが非対応)");
                return;
            }

            var gattServiceProvider = gattServiceProviderResult.ServiceProvider;

            // キャラクタリスティックのパラメータを定義
            var cReadWriteParam = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Write | GattCharacteristicProperties.Notify,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "cReadWrite"
            };

            // UUIDを指定してキャラクタリスティックをサービスに登録
            var cReadWrite = await gattServiceProvider.Service.CreateCharacteristicAsync(new Guid("4C587114-038B-47B2-997D-0E9D13F12DA3"), cReadWriteParam);

            // 読み取りリクエストのハンドラを設定
            cReadWrite.Characteristic.ReadRequested += async (sender, args) =>
            {
                var deferral = args.GetDeferral();
                var request = await args.GetRequestAsync();
                byte[] buf = new byte[1] { cnt };

                // DataWriter を使用して IBuffer を作成
                IBuffer buffer;
                using (var dataWriter = new DataWriter())
                {
                    dataWriter.WriteBytes(buf);
                    buffer = dataWriter.DetachBuffer();
                }

                request.RespondWithValue(buffer);
                deferral.Complete();
            };

            // 書き込みリクエストのハンドラを設定
            cReadWrite.Characteristic.WriteRequested += async (sender, args) =>
            {
                var deferral = args.GetDeferral();
                var request = await args.GetRequestAsync();

                // 拡張メソッドを使用してバイト配列を取得
                byte[] data = request.Value.ToByteArray();

                // バイト配列を文字列に変換（UTF-8エンコーディングを使用）
                string receivedData = Encoding.UTF8.GetString(data);

                // Consoleへの表示（デバッグ用）
                Console.WriteLine(receivedData);

                // UIにデータを表示
                ReceiveData(receivedData);

                if (request.Option == GattWriteOption.WriteWithResponse)
                {
                    request.Respond();
                }
                deferral.Complete();
            };


            // 通知購読の変更ハンドラを設定
            cReadWrite.Characteristic.SubscribedClientsChanged += (sender, args) =>
            {
                Console.WriteLine("通知購読の変更");
                foreach (var c in sender.SubscribedClients)
                {
                    Console.WriteLine("- デバイス: " + c.Session.DeviceId.Id);
                }
                Console.WriteLine("- デバイス終了");
            };

            // アドバタイジングのパラメータ
            var advertisingParameters = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            };
            gattServiceProvider.StartAdvertising(advertisingParameters);
            Console.WriteLine("アドバタイジング開始...");

            // 1秒ごとにインクリメント値を通知
            while (true)
            {
                byte[] bufN = new byte[1] { cnt };

                // DataWriter を使用して IBuffer を作成
                IBuffer buffer;
                using (var dataWriter = new DataWriter())
                {
                    dataWriter.WriteBytes(bufN);
                    buffer = dataWriter.DetachBuffer();
                }

                // 値の通知
                await cReadWrite.Characteristic.NotifyValueAsync(buffer);
                Console.WriteLine("Notify " + cnt.ToString("X"));

                // 1秒待機
                await Task.Delay(1000);

                // カウンターをインクリメント
                cnt++;
            }
        }

        private void StartTcpServer(string ipAddress)
        {
            IPAddress localAddr = IPAddress.Parse(ipAddress);
            _tcpListener = new TcpListener(localAddr, 5111);
            _tcpListener.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            _listenerThread = new Thread(() => ListenForClients(_cancellationTokenSource.Token));
            _listenerThread.Start();
            Console.WriteLine("TCPサーバーを起動しました。");
        }

        private async void ListenForClients(CancellationToken cancellationToken)
        {
            try
            {
                while (_tcpListener != null && !cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("クライアントの接続を待機中...");
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();

                    _ = Task.Run(() => HandleClient(client), cancellationToken);
                }
            }
            catch (ObjectDisposedException)
            {
                // サーバーが停止した場合の例外を無視
            }
            catch (Exception ex)
            {
                Console.WriteLine("サーバーエラー: " + ex.ToString());
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();

                // リクエスト全体を読み込む
                string request = await ReadRequestAsync(stream);
                Console.WriteLine("受信したリクエスト:\n" + request);

                // リクエストラインとヘッダーの解析
                int headerEndIndex = request.IndexOf("\r\n\r\n");
                if (headerEndIndex == -1)
                {
                    await SendBadRequestResponse(stream);
                    return;
                }

                string headerPart = request.Substring(0, headerEndIndex);
                string[] lines = headerPart.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string requestLine = lines[0];
                Console.WriteLine("リクエストライン: " + requestLine);

                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length < 3)
                {
                    await SendBadRequestResponse(stream);
                    return;
                }

                string method = requestParts[0];
                string path = requestParts[1];
                string protocol = requestParts[2];

                // ヘッダーの解析
                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int separatorIndex = line.IndexOf(':');
                    if (separatorIndex > 0)
                    {
                        string headerName = line.Substring(0, separatorIndex).Trim();
                        string headerValue = line.Substring(separatorIndex + 1).Trim();
                        headers[headerName] = headerValue;
                        Console.WriteLine($"ヘッダー: {headerName} = {headerValue}");
                    }
                }

                // リクエストボディの読み取り
                string requestBody = "";
                if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    if (headers.ContainsKey("Content-Length"))
                    {
                        int contentLength = int.Parse(headers["Content-Length"]);
                        int bodyStartIndex = headerEndIndex + 4; // "\r\n\r\n" の後
                        int bodyLength = request.Length - bodyStartIndex;

                        if (bodyLength < contentLength)
                        {
                            // ボディが完全に読み込まれていない場合、残りを読み込む
                            byte[] bodyBuffer = new byte[contentLength - bodyLength];
                            int bytesRead = await stream.ReadAsync(bodyBuffer, 0, bodyBuffer.Length);
                            requestBody = request.Substring(bodyStartIndex) + Encoding.UTF8.GetString(bodyBuffer, 0, bytesRead);
                        }
                        else
                        {
                            requestBody = request.Substring(bodyStartIndex, contentLength);
                        }

                        Console.WriteLine("受信したデータ: " + requestBody);

                        // リクエストの処理
                        ProcessRequest(path, requestBody);

                        // レスポンスを送信
                        await SendSuccessResponse(stream, "データを受け取りました");
                    }
                    else
                    {
                        await SendBadRequestResponse(stream);
                    }
                }
                else
                {
                    await SendBadRequestResponse(stream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("クライアント処理中のエラー: " + ex.ToString());
            }
            finally
            {
                client.Close();
            }
        }

        private async Task<string> ReadRequestAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            int bytesRead = 0;
            StringBuilder requestBuilder = new StringBuilder();

            while (true)
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // クライアントが切断
                }

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                requestBuilder.Append(chunk);

                // ヘッダーの終わりを検出
                if (requestBuilder.ToString().Contains("\r\n\r\n"))
                {
                    // Content-Length ヘッダーを確認して、ボディがすべて読み込まれているか確認
                    string tempRequest = requestBuilder.ToString();
                    int headerEndIndex = tempRequest.IndexOf("\r\n\r\n");
                    string headerPart = tempRequest.Substring(0, headerEndIndex);
                    string[] lines = headerPart.Split(new[] { "\r\n" }, StringSplitOptions.None);

                    Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        int separatorIndex = line.IndexOf(':');
                        if (separatorIndex > 0)
                        {
                            string headerName = line.Substring(0, separatorIndex).Trim();
                            string headerValue = line.Substring(separatorIndex + 1).Trim();
                            headers[headerName] = headerValue;
                        }
                    }

                    if (headers.ContainsKey("Content-Length"))
                    {
                        int contentLength = int.Parse(headers["Content-Length"]);
                        int totalLength = headerEndIndex + 4 + contentLength;
                        if (requestBuilder.Length >= totalLength)
                        {
                            break; // リクエスト全体を読み込んだ
                        }
                    }
                    else
                    {
                        break; // Content-Length がない場合はヘッダーまでで終了
                    }
                }
            }

            return requestBuilder.ToString();
        }

        private async Task SendSuccessResponse(NetworkStream stream, string responseBody)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(responseBody);
            string responseHeader = "HTTP/1.1 200 OK\r\n" +
                                    "Content-Type: text/plain; charset=UTF-8\r\n" +
                                    "Content-Length: " + bodyBytes.Length + "\r\n" +
                                    "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeader);

            // デバッグ用にレスポンスを表示
            Console.WriteLine("送信するレスポンス:\n" + responseHeader + responseBody);

            // ヘッダーとボディを順番に送信
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            await stream.FlushAsync();
        }

        private async Task SendBadRequestResponse(NetworkStream stream)
        {
            string responseBody = "不正なリクエストです";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(responseBody);
            string responseHeader = "HTTP/1.1 400 Bad Request\r\n" +
                                    "Content-Type: text/plain; charset=UTF-8\r\n" +
                                    "Content-Length: " + bodyBytes.Length + "\r\n" +
                                    "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeader);

            // デバッグ用にレスポンスを表示
            Console.WriteLine("送信するエラーレスポンス:\n" + responseHeader + responseBody);

            // ヘッダーとボディを順番に送信
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            await stream.FlushAsync();
        }

        private void ProcessRequest(string path, string requestData)
        {
            if (path == "/data")
            {
                try
                {
                    using var jsonDoc = JsonDocument.Parse(requestData);
                    JsonElement root = jsonDoc.RootElement;

                    if (root.TryGetProperty("data", out JsonElement dataElement))
                    {
                        string? dataValue = dataElement.GetString();
                        if (!string.IsNullOrEmpty(dataValue))
                        {
                            ReceiveData(dataValue);
                        }
                        else
                        {
                            Console.WriteLine("dataが見つかりませんでした。");
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine("JSONパースエラー: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("未対応のパス: " + path);
            }
        }

        private void ReceiveData(string dataValue)
        {
            string[] values = dataValue.Split("_+_");
            if (values.Length == 2)
            {
                string type = values[0];
                string value = values[1];
                if (type == "hotkey")
                {
                    SimulateHotkey(value);
                }
                else if (type == "typewrite")
                {
                    sim.Keyboard.TextEntry(value);
                }
            }
            else
            {
                Console.WriteLine("dataの値が2つではありません。");
            }
        }

        private string GetLocalIPv4()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }

            return "127.0.0.1";
        }

        public static void SimulateHotkey(string value)
        {
            string[] keys = value.Trim().Split(' ');
            Console.WriteLine("Hotkey: " + string.Join(", ", keys));

            List<VirtualKeyCode> modifiers = new List<VirtualKeyCode>();
            List<VirtualKeyCode> codes = new List<VirtualKeyCode>();

            foreach (var key in keys)
            {
                var modifier = ModifierFlagForKey(key);
                var keyCode = KeyCodeForKey(key);

                if (modifier != null)
                {
                    modifiers.Add(modifier.Value);
                }
                else if (keyCode != null)
                {
                    codes.Add(keyCode.Value);
                }
            }

            if (modifiers.Count > 0 || codes.Count > 0)
            {
                if (modifiers.Count > 0)
                {
                    sim.Keyboard.ModifiedKeyStroke(modifiers, codes);
                }
                else if (codes.Count > 0)
                {
                    foreach (var code in codes)
                    {
                        sim.Keyboard.KeyPress(code);
                    }
                }
            }
        }

        public static VirtualKeyCode? ModifierFlagForKey(string key)
        {
            switch (key.ToLower())
            {
                case "control":
                case "ctrl":
                    return VirtualKeyCode.CONTROL;
                case "shift":
                    return VirtualKeyCode.SHIFT;
                case "alt":
                    return VirtualKeyCode.MENU;
                case "capslock":
                    return VirtualKeyCode.CAPITAL;
                case "numlock":
                    return VirtualKeyCode.NUMLOCK;
                case "scrolllock":
                    return VirtualKeyCode.SCROLL;
                case "win":
                case "lwin":
                    return VirtualKeyCode.LWIN;
                case "rwin":
                    return VirtualKeyCode.RWIN;
                case "apps":
                    return VirtualKeyCode.APPS;
                default:
                    return null;
            }
        }

        public static VirtualKeyCode? KeyCodeForKey(string key)
        {
            Dictionary<string, VirtualKeyCode> keyCodeMap = new Dictionary<string, VirtualKeyCode>
            {
                { "lbutton", VirtualKeyCode.LBUTTON },          // マウスの左ボタン
                { "rbutton", VirtualKeyCode.RBUTTON },          // マウスの右ボタン
                { "mbutton", VirtualKeyCode.MBUTTON },          // マウスの中央ボタン
                { "xbutton1", VirtualKeyCode.XBUTTON1 },        // X1 マウス ボタン
                { "xbutton2", VirtualKeyCode.XBUTTON2 },        // X2 マウス ボタン
                { "back", VirtualKeyCode.BACK },                // Backspace キー
                { "tab", VirtualKeyCode.TAB },                  // Tab キー
                { "clear", VirtualKeyCode.CLEAR },              // Clear キー
                { "return", VirtualKeyCode.RETURN },            // Enter キー
                { "shift", VirtualKeyCode.SHIFT },              // Shift キー
                { "control", VirtualKeyCode.CONTROL },          // Ctrl キー
                { "menu", VirtualKeyCode.MENU },                // ALT キー
                { "pause", VirtualKeyCode.PAUSE },              // Pause キー
                { "capital", VirtualKeyCode.CAPITAL },          // CAPS LOCK キー
                { "kana", VirtualKeyCode.KANA },                // IME かなモード
                { "hangul", VirtualKeyCode.HANGUL },            // IME ハングル モード
                { "junja", VirtualKeyCode.JUNJA },              // IME Junja モード
                { "final", VirtualKeyCode.FINAL },              // IME Final モード
                { "hanja", VirtualKeyCode.HANJA },              // IME Hanja モード
                { "kanji", VirtualKeyCode.KANJI },              // IME 漢字モード
                { "escape", VirtualKeyCode.ESCAPE },            // Esc キー
                { "convert", VirtualKeyCode.CONVERT },          // IME 変換
                { "nonconvert", VirtualKeyCode.NONCONVERT },    // IME 無変換
                { "accept", VirtualKeyCode.ACCEPT },            // IME 使用可能
                { "space", VirtualKeyCode.SPACE },              // Space キー
                { "prior", VirtualKeyCode.PRIOR },              // PageUp キー
                { "next", VirtualKeyCode.NEXT },                // PageDown キー
                { "end", VirtualKeyCode.END },                  // End キー
                { "home", VirtualKeyCode.HOME },                // Home キー
                { "left", VirtualKeyCode.LEFT },                // 左方向キー
                { "up", VirtualKeyCode.UP },                    // 上方向キー
                { "right", VirtualKeyCode.RIGHT },              // 右方向キー
                { "down", VirtualKeyCode.DOWN },                // 下方向キー
                { "select", VirtualKeyCode.SELECT },            // Select キー
                { "print", VirtualKeyCode.PRINT },              // Print キー
                { "execute", VirtualKeyCode.EXECUTE },          // Execute キー
                { "snapshot", VirtualKeyCode.SNAPSHOT },        // Print Screen キー
                { "insert", VirtualKeyCode.INSERT },            // Ins キー
                { "delete", VirtualKeyCode.DELETE },            // DEL キー
                { "help", VirtualKeyCode.HELP },                // Help キー
                { "0", VirtualKeyCode.VK_0 },                   // 0 キー
                { "1", VirtualKeyCode.VK_1 },                   // 1 キー
                { "2", VirtualKeyCode.VK_2 },                   // 2 キー
                { "3", VirtualKeyCode.VK_3 },                   // 3 キー
                { "4", VirtualKeyCode.VK_4 },                   // 4 キー
                { "5", VirtualKeyCode.VK_5 },                   // 5 キー
                { "6", VirtualKeyCode.VK_6 },                   // 6 キー
                { "7", VirtualKeyCode.VK_7 },                   // 7 キー
                { "8", VirtualKeyCode.VK_8 },                   // 8 キー
                { "9", VirtualKeyCode.VK_9 },                   // 9 キー
                { "a", VirtualKeyCode.VK_A },                   // A キー
                { "b", VirtualKeyCode.VK_B },                   // B キー
                { "c", VirtualKeyCode.VK_C },                   // C キー
                { "d", VirtualKeyCode.VK_D },                   // D キー
                { "e", VirtualKeyCode.VK_E },                   // E キー
                { "f", VirtualKeyCode.VK_F },                   // F キー
                { "g", VirtualKeyCode.VK_G },                   // G キー
                { "h", VirtualKeyCode.VK_H },                   // H キー
                { "i", VirtualKeyCode.VK_I },                   // I キー
                { "j", VirtualKeyCode.VK_J },                   // J キー
                { "k", VirtualKeyCode.VK_K },                   // K キー
                { "l", VirtualKeyCode.VK_L },                   // L キー
                { "m", VirtualKeyCode.VK_M },                   // M キー
                { "n", VirtualKeyCode.VK_N },                   // N キー
                { "o", VirtualKeyCode.VK_O },                   // O キー
                { "p", VirtualKeyCode.VK_P },                   // P キー
                { "q", VirtualKeyCode.VK_Q },                   // Q キー
                { "r", VirtualKeyCode.VK_R },                   // R キー
                { "s", VirtualKeyCode.VK_S },                   // S キー
                { "t", VirtualKeyCode.VK_T },                   // T キー
                { "u", VirtualKeyCode.VK_U },                   // U キー
                { "v", VirtualKeyCode.VK_V },                   // V キー
                { "w", VirtualKeyCode.VK_W },                   // W キー
                { "x", VirtualKeyCode.VK_X },                   // X キー
                { "y", VirtualKeyCode.VK_Y },                   // Y キー
                { "z", VirtualKeyCode.VK_Z },                   // Z キー
                { "lwin", VirtualKeyCode.LWIN },                // Windows の左キー
                { "rwin", VirtualKeyCode.RWIN },                // 右の Windows キー
                { "apps", VirtualKeyCode.APPS },                // アプリケーション キー
                { "sleep", VirtualKeyCode.SLEEP },              // スリープ キー
                { "numpad0", VirtualKeyCode.NUMPAD0 },          // テンキーの 0 キー
                { "numpad1", VirtualKeyCode.NUMPAD1 },          // テンキーの 1 キー
                { "numpad2", VirtualKeyCode.NUMPAD2 },          // テンキーの 2 キー
                { "numpad3", VirtualKeyCode.NUMPAD3 },          // テンキーの 3 キー
                { "numpad4", VirtualKeyCode.NUMPAD4 },          // テンキーの 4 キー
                { "numpad5", VirtualKeyCode.NUMPAD5 },          // テンキーの 5 キー
                { "numpad6", VirtualKeyCode.NUMPAD6 },          // テンキーの 6 キー
                { "numpad7", VirtualKeyCode.NUMPAD7 },          // テンキーの 7 キー
                { "numpad8", VirtualKeyCode.NUMPAD8 },          // テンキーの 8 キー
                { "numpad9", VirtualKeyCode.NUMPAD9 },          // テンキーの 9 キー
                { "multiply", VirtualKeyCode.MULTIPLY },        // 乗算キー
                { "separator", VirtualKeyCode.SEPARATOR },      // 区切り記号キー
                { "subtract", VirtualKeyCode.SUBTRACT },        // 減算キー
                { "decimal", VirtualKeyCode.DECIMAL },          // 10 進キー
                { "divide", VirtualKeyCode.DIVIDE },            // 除算キー
                { "f1", VirtualKeyCode.F1 },                    // F1 キー
                { "f2", VirtualKeyCode.F2 },                    // F2 キー
                { "f3", VirtualKeyCode.F3 },                    // F3 キー
                { "f4", VirtualKeyCode.F4 },                    // F4 キー
                { "f5", VirtualKeyCode.F5 },                    // F5 キー
                { "f6", VirtualKeyCode.F6 },                    // F6 キー
                { "f7", VirtualKeyCode.F7 },                    // F7 キー
                { "f8", VirtualKeyCode.F8 },                    // F8 キー
                { "f9", VirtualKeyCode.F9 },                    // F9 キー
                { "f10", VirtualKeyCode.F10 },                  // F10 キー
                { "f11", VirtualKeyCode.F11 },                  // F11 キー
                { "f12", VirtualKeyCode.F12 },                  // F12 キー
                { "f13", VirtualKeyCode.F13 },                  // F13 キー
                { "f14", VirtualKeyCode.F14 },                  // F14 キー
                { "f15", VirtualKeyCode.F15 },                  // F15 キー
                { "f16", VirtualKeyCode.F16 },                  // F16 キー
                { "f17", VirtualKeyCode.F17 },                  // F17 キー
                { "f18", VirtualKeyCode.F18 },                  // F18 キー
                { "f19", VirtualKeyCode.F19 },                  // F19 キー
                { "f20", VirtualKeyCode.F20 },                  // F20 キー
                { "f21", VirtualKeyCode.F21 },                  // F21 キー
                { "f22", VirtualKeyCode.F22 },                  // F22 キー
                { "f23", VirtualKeyCode.F23 },                  // F23 キー
                { "f24", VirtualKeyCode.F24 },                  // F24 キー
                { "numlock", VirtualKeyCode.NUMLOCK },          // NUM LOCK キー
                { "scroll", VirtualKeyCode.SCROLL },            // ScrollLock キー
                { "lshift", VirtualKeyCode.LSHIFT },            // 左 Shift キー
                { "rshift", VirtualKeyCode.RSHIFT },            // 右 Shift キー
                { "lcontrol", VirtualKeyCode.LCONTROL },        // 左 Ctrl キー
                { "rcontrol", VirtualKeyCode.RCONTROL },        // 右 Ctrl キー
                { "lmenu", VirtualKeyCode.LMENU },              // 左 Alt キー
                { "rmenu", VirtualKeyCode.RMENU },              // 右 Alt キー
                { "browser_back", VirtualKeyCode.BROWSER_BACK },            // ブラウザーの戻るキー
                { "browser_forward", VirtualKeyCode.BROWSER_FORWARD },      // ブラウザーの進むキー
                { "browser_refresh", VirtualKeyCode.BROWSER_REFRESH },      // ブラウザーの更新キー
                { "browser_stop", VirtualKeyCode.BROWSER_STOP },            // ブラウザーの停止キー
                { "browser_search", VirtualKeyCode.BROWSER_SEARCH },        // ブラウザーの検索キー
                { "browser_favorites", VirtualKeyCode.BROWSER_FAVORITES },  // ブラウザーのお気に入りキー
                { "browser_home", VirtualKeyCode.BROWSER_HOME },            // ブラウザーのホーム キー
                { "volume_mute", VirtualKeyCode.VOLUME_MUTE },              // 音量ミュート キー
                { "volume_down", VirtualKeyCode.VOLUME_DOWN },              // 音量下げるキー
                { "volume_up", VirtualKeyCode.VOLUME_UP },                  // 音量上げるキー
                { "media_next_track", VirtualKeyCode.MEDIA_NEXT_TRACK },    // 次のトラックキー
                { "media_prev_track", VirtualKeyCode.MEDIA_PREV_TRACK },    // 前のトラック
                { "media_stop", VirtualKeyCode.MEDIA_STOP },                // メディアの停止キー
                { "media_play_pause", VirtualKeyCode.MEDIA_PLAY_PAUSE },    // メディアの再生/一時停止キー
                { "launch_media_select", VirtualKeyCode.LAUNCH_MEDIA_SELECT },  // メディアの選択キー
                { "launch_app1", VirtualKeyCode.LAUNCH_APP1 },              // アプリケーション 1 の起動キー
                { "launch_app2", VirtualKeyCode.LAUNCH_APP2 },              // アプリケーション 2 の起動キー
                { "oem_1", VirtualKeyCode.OEM_1 },                          // ;: キー (米国標準キーボード)
                { "+", VirtualKeyCode.OEM_PLUS },                    // + キー
                { ",", VirtualKeyCode.OEM_COMMA },                  // , キー
                { "-", VirtualKeyCode.OEM_MINUS },                  // - キー
                { ".", VirtualKeyCode.OEM_PERIOD },                // . キー
                { "oem_2", VirtualKeyCode.OEM_2 },                          // /? キー (米国標準キーボード)
                { "oem_3", VirtualKeyCode.OEM_3 },                          // `~ キー (米国標準キーボード)
                { "oem_4", VirtualKeyCode.OEM_4 },                          // [{ キー (米国標準キーボード)
                { "oem_5", VirtualKeyCode.OEM_5 },                          // \| キー (米国標準キーボード)
                { "oem_6", VirtualKeyCode.OEM_6 },                          // ]} キー (米国標準キーボード)
                { "oem_7", VirtualKeyCode.OEM_7 },                          // '" キー (米国標準キーボード)
                { "oem_102", VirtualKeyCode.OEM_102 },                      // <> または \| キー (US 以外のキーボード)
                { "processkey", VirtualKeyCode.PROCESSKEY },                // IME PROCESS キー
                { "packet", VirtualKeyCode.PACKET },                        // Unicode 文字
                { "attn", VirtualKeyCode.ATTN },                            // Attn キー
                { "crsel", VirtualKeyCode.CRSEL },                          // CrSel キー
                { "exsel", VirtualKeyCode.EXSEL },                          // ExSel キー
                { "play", VirtualKeyCode.PLAY },                            // 再生キー
                { "zoom", VirtualKeyCode.ZOOM },                            // ズーム キー
            };


            return keyCodeMap.TryGetValue(key.ToLower(), out var keyCode) ? keyCode : null;
        }


        private void open_Client_Window(object sender, RoutedEventArgs e)
        {
            BlankWindow1 window = new BlankWindow1();
            window.Activate();
        }
    }
}

public static class TaskEx
{
    public static Task<T> AsTask<T>(this IAsyncOperation<T> operation)
    {
        var tcs = new TaskCompletionSource<T>();
        operation.Completed = delegate
        {
            switch (operation.Status)
            {
                case AsyncStatus.Completed:
                    tcs.SetResult(operation.GetResults());
                    break;
                case AsyncStatus.Error:
                    tcs.SetException(operation.ErrorCode);
                    break;
                case AsyncStatus.Canceled:
                    tcs.SetCanceled();
                    break;
            }
        };
        return tcs.Task;
    }

    public static TaskAwaiter<T> GetAwaiter<T>(this IAsyncOperation<T> operation)
    {
        return operation.AsTask().GetAwaiter();
    }
}

public static class BufferExtensions
{
    public static byte[] ToByteArray(this IBuffer buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        byte[] data = new byte[buffer.Length];

        // バッファからバイト配列にコピー
        using (var dataReader = DataReader.FromBuffer(buffer))
        {
            dataReader.ReadBytes(data);
        }

        return data;
    }
}
