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
                { "a", VirtualKeyCode.VK_A },
                { "b", VirtualKeyCode.VK_B },
                { "c", VirtualKeyCode.VK_C },
                { "d", VirtualKeyCode.VK_D },
                { "e", VirtualKeyCode.VK_E },
                { "f", VirtualKeyCode.VK_F },
                { "g", VirtualKeyCode.VK_G },
                { "h", VirtualKeyCode.VK_H },
                { "i", VirtualKeyCode.VK_I },
                { "j", VirtualKeyCode.VK_J },
                { "k", VirtualKeyCode.VK_K },
                { "l", VirtualKeyCode.VK_L },
                { "m", VirtualKeyCode.VK_M },
                { "n", VirtualKeyCode.VK_N },
                { "o", VirtualKeyCode.VK_O },
                { "p", VirtualKeyCode.VK_P },
                { "q", VirtualKeyCode.VK_Q },
                { "r", VirtualKeyCode.VK_R },
                { "s", VirtualKeyCode.VK_S },
                { "t", VirtualKeyCode.VK_T },
                { "u", VirtualKeyCode.VK_U },
                { "v", VirtualKeyCode.VK_V },
                { "w", VirtualKeyCode.VK_W },
                { "x", VirtualKeyCode.VK_X },
                { "y", VirtualKeyCode.VK_Y },
                { "z", VirtualKeyCode.VK_Z },
                { "1", VirtualKeyCode.VK_1 },
                { "2", VirtualKeyCode.VK_2 },
                { "3", VirtualKeyCode.VK_3 },
                { "4", VirtualKeyCode.VK_4 },
                { "5", VirtualKeyCode.VK_5 },
                { "6", VirtualKeyCode.VK_6 },
                { "7", VirtualKeyCode.VK_7 },
                { "8", VirtualKeyCode.VK_8 },
                { "9", VirtualKeyCode.VK_9 },
                { "0", VirtualKeyCode.VK_0 },
                { "enter", VirtualKeyCode.RETURN },
                { "space", VirtualKeyCode.SPACE },
                { "esc", VirtualKeyCode.ESCAPE },
                { "tab", VirtualKeyCode.TAB },
                { "delete", VirtualKeyCode.DELETE },
                { "backspace", VirtualKeyCode.BACK },
                { "left_arrow", VirtualKeyCode.LEFT },
                { "right_arrow", VirtualKeyCode.RIGHT },
                { "up_arrow", VirtualKeyCode.UP },
                { "down_arrow", VirtualKeyCode.DOWN }
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
