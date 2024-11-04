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

            this.Title = "����f�o�C�X";
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
                LaunchOnStartupToggle.IsEnabled = false; // �g�O���̑�����ꎞ�I�ɖ�����
                await ToggleLaunchOnStartup(LaunchOnStartupToggle.IsOn);
            }
            catch (Exception ex)
            {
                // �G���[�����������ꍇ�Ƀ��O���o��
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
                LaunchOnStartupToggle.IsEnabled = true; // ������ēx�L����
            }
        }

        // �N�����ɃX�^�[�g�A�b�v�^�X�N�̏�Ԃ��m�F���A�g�O���{�^���̏�Ԃ��X�V
        private async void CheckStartupTaskStatus()
        {
            var startupTask = await StartupTask.GetAsync("LaunchOnStartupTaskId");
            UpdateToggleState(startupTask.State);
        }

        // �g�O���{�^���̏�Ԃ�ݒ�
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
                // ��O�̃L���b�`�ƃG���[�n���h�����O
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

                // QrBitmap��ImageSource�ɕϊ�

                bitmapImage = ConvertBitmapToBitmapImage(resizedQrBitmap);

                myImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("QR�R�[�h�������ɃG���[���������܂���: " + ex.Message);
            }
        }

        public BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Bitmap���������[�X�g���[���ɕۑ�
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

                // �������[�X�g���[���̈ʒu��0�Ƀ��Z�b�g
                memoryStream.Position = 0;

                // BitmapImage���쐬
                var bitmapImage = new BitmapImage();

                // IRandomAccessStream���璼�ړǂݍ��ޑ�֕��@
                using (var randomAccessStream = new InMemoryRandomAccessStream())
                {
                    using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
                    {
                        var writer = new DataWriter(outputStream);
                        writer.WriteBytes(memoryStream.ToArray());
                        writer.StoreAsync().GetAwaiter().GetResult();
                        writer.FlushAsync().GetAwaiter().GetResult();
                    }

                    // BitmapImage�ɃX�g���[����ݒ�
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
                Console.WriteLine("GATT�T�[�r�X�̋N���Ɏ��s (Bluetooth LE�f�o�C�X����Ή�)");
                return;
            }

            var gattServiceProvider = gattServiceProviderResult.ServiceProvider;

            // �L�����N�^���X�e�B�b�N�̃p�����[�^���`
            var cReadWriteParam = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Write | GattCharacteristicProperties.Notify,
                ReadProtectionLevel = GattProtectionLevel.Plain,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "cReadWrite"
            };

            // UUID���w�肵�ăL�����N�^���X�e�B�b�N���T�[�r�X�ɓo�^
            var cReadWrite = await gattServiceProvider.Service.CreateCharacteristicAsync(new Guid("4C587114-038B-47B2-997D-0E9D13F12DA3"), cReadWriteParam);

            // �ǂݎ�胊�N�G�X�g�̃n���h����ݒ�
            cReadWrite.Characteristic.ReadRequested += async (sender, args) =>
            {
                var deferral = args.GetDeferral();
                var request = await args.GetRequestAsync();
                byte[] buf = new byte[1] { cnt };

                // DataWriter ���g�p���� IBuffer ���쐬
                IBuffer buffer;
                using (var dataWriter = new DataWriter())
                {
                    dataWriter.WriteBytes(buf);
                    buffer = dataWriter.DetachBuffer();
                }

                request.RespondWithValue(buffer);
                deferral.Complete();
            };

            // �������݃��N�G�X�g�̃n���h����ݒ�
            cReadWrite.Characteristic.WriteRequested += async (sender, args) =>
            {
                var deferral = args.GetDeferral();
                var request = await args.GetRequestAsync();

                // �g�����\�b�h���g�p���ăo�C�g�z����擾
                byte[] data = request.Value.ToByteArray();

                // �o�C�g�z��𕶎���ɕϊ��iUTF-8�G���R�[�f�B���O���g�p�j
                string receivedData = Encoding.UTF8.GetString(data);

                // Console�ւ̕\���i�f�o�b�O�p�j
                Console.WriteLine(receivedData);

                // UI�Ƀf�[�^��\��
                ReceiveData(receivedData);

                if (request.Option == GattWriteOption.WriteWithResponse)
                {
                    request.Respond();
                }
                deferral.Complete();
            };


            // �ʒm�w�ǂ̕ύX�n���h����ݒ�
            cReadWrite.Characteristic.SubscribedClientsChanged += (sender, args) =>
            {
                Console.WriteLine("�ʒm�w�ǂ̕ύX");
                foreach (var c in sender.SubscribedClients)
                {
                    Console.WriteLine("- �f�o�C�X: " + c.Session.DeviceId.Id);
                }
                Console.WriteLine("- �f�o�C�X�I��");
            };

            // �A�h�o�^�C�W���O�̃p�����[�^
            var advertisingParameters = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true
            };
            gattServiceProvider.StartAdvertising(advertisingParameters);
            Console.WriteLine("�A�h�o�^�C�W���O�J�n...");

            // 1�b���ƂɃC���N�������g�l��ʒm
            while (true)
            {
                byte[] bufN = new byte[1] { cnt };

                // DataWriter ���g�p���� IBuffer ���쐬
                IBuffer buffer;
                using (var dataWriter = new DataWriter())
                {
                    dataWriter.WriteBytes(bufN);
                    buffer = dataWriter.DetachBuffer();
                }

                // �l�̒ʒm
                await cReadWrite.Characteristic.NotifyValueAsync(buffer);
                Console.WriteLine("Notify " + cnt.ToString("X"));

                // 1�b�ҋ@
                await Task.Delay(1000);

                // �J�E���^�[���C���N�������g
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
            Console.WriteLine("TCP�T�[�o�[���N�����܂����B");
        }

        private async void ListenForClients(CancellationToken cancellationToken)
        {
            try
            {
                while (_tcpListener != null && !cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("�N���C�A���g�̐ڑ���ҋ@��...");
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();

                    _ = Task.Run(() => HandleClient(client), cancellationToken);
                }
            }
            catch (ObjectDisposedException)
            {
                // �T�[�o�[����~�����ꍇ�̗�O�𖳎�
            }
            catch (Exception ex)
            {
                Console.WriteLine("�T�[�o�[�G���[: " + ex.ToString());
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();

                // ���N�G�X�g�S�̂�ǂݍ���
                string request = await ReadRequestAsync(stream);
                Console.WriteLine("��M�������N�G�X�g:\n" + request);

                // ���N�G�X�g���C���ƃw�b�_�[�̉��
                int headerEndIndex = request.IndexOf("\r\n\r\n");
                if (headerEndIndex == -1)
                {
                    await SendBadRequestResponse(stream);
                    return;
                }

                string headerPart = request.Substring(0, headerEndIndex);
                string[] lines = headerPart.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string requestLine = lines[0];
                Console.WriteLine("���N�G�X�g���C��: " + requestLine);

                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length < 3)
                {
                    await SendBadRequestResponse(stream);
                    return;
                }

                string method = requestParts[0];
                string path = requestParts[1];
                string protocol = requestParts[2];

                // �w�b�_�[�̉��
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
                        Console.WriteLine($"�w�b�_�[: {headerName} = {headerValue}");
                    }
                }

                // ���N�G�X�g�{�f�B�̓ǂݎ��
                string requestBody = "";
                if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    if (headers.ContainsKey("Content-Length"))
                    {
                        int contentLength = int.Parse(headers["Content-Length"]);
                        int bodyStartIndex = headerEndIndex + 4; // "\r\n\r\n" �̌�
                        int bodyLength = request.Length - bodyStartIndex;

                        if (bodyLength < contentLength)
                        {
                            // �{�f�B�����S�ɓǂݍ��܂�Ă��Ȃ��ꍇ�A�c���ǂݍ���
                            byte[] bodyBuffer = new byte[contentLength - bodyLength];
                            int bytesRead = await stream.ReadAsync(bodyBuffer, 0, bodyBuffer.Length);
                            requestBody = request.Substring(bodyStartIndex) + Encoding.UTF8.GetString(bodyBuffer, 0, bytesRead);
                        }
                        else
                        {
                            requestBody = request.Substring(bodyStartIndex, contentLength);
                        }

                        Console.WriteLine("��M�����f�[�^: " + requestBody);

                        // ���N�G�X�g�̏���
                        ProcessRequest(path, requestBody);

                        // ���X�|���X�𑗐M
                        await SendSuccessResponse(stream, "�f�[�^���󂯎��܂���");
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
                Console.WriteLine("�N���C�A���g�������̃G���[: " + ex.ToString());
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
                    break; // �N���C�A���g���ؒf
                }

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                requestBuilder.Append(chunk);

                // �w�b�_�[�̏I�������o
                if (requestBuilder.ToString().Contains("\r\n\r\n"))
                {
                    // Content-Length �w�b�_�[���m�F���āA�{�f�B�����ׂēǂݍ��܂�Ă��邩�m�F
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
                            break; // ���N�G�X�g�S�̂�ǂݍ���
                        }
                    }
                    else
                    {
                        break; // Content-Length ���Ȃ��ꍇ�̓w�b�_�[�܂łŏI��
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

            // �f�o�b�O�p�Ƀ��X�|���X��\��
            Console.WriteLine("���M���郌�X�|���X:\n" + responseHeader + responseBody);

            // �w�b�_�[�ƃ{�f�B�����Ԃɑ��M
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            await stream.FlushAsync();
        }

        private async Task SendBadRequestResponse(NetworkStream stream)
        {
            string responseBody = "�s���ȃ��N�G�X�g�ł�";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(responseBody);
            string responseHeader = "HTTP/1.1 400 Bad Request\r\n" +
                                    "Content-Type: text/plain; charset=UTF-8\r\n" +
                                    "Content-Length: " + bodyBytes.Length + "\r\n" +
                                    "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeader);

            // �f�o�b�O�p�Ƀ��X�|���X��\��
            Console.WriteLine("���M����G���[���X�|���X:\n" + responseHeader + responseBody);

            // �w�b�_�[�ƃ{�f�B�����Ԃɑ��M
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
                            Console.WriteLine("data��������܂���ł����B");
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine("JSON�p�[�X�G���[: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("���Ή��̃p�X: " + path);
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
                Console.WriteLine("data�̒l��2�ł͂���܂���B");
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
                { "lbutton", VirtualKeyCode.LBUTTON },          // �}�E�X�̍��{�^��
                { "rbutton", VirtualKeyCode.RBUTTON },          // �}�E�X�̉E�{�^��
                { "mbutton", VirtualKeyCode.MBUTTON },          // �}�E�X�̒����{�^��
                { "xbutton1", VirtualKeyCode.XBUTTON1 },        // X1 �}�E�X �{�^��
                { "xbutton2", VirtualKeyCode.XBUTTON2 },        // X2 �}�E�X �{�^��
                { "back", VirtualKeyCode.BACK },                // Backspace �L�[
                { "tab", VirtualKeyCode.TAB },                  // Tab �L�[
                { "clear", VirtualKeyCode.CLEAR },              // Clear �L�[
                { "return", VirtualKeyCode.RETURN },            // Enter �L�[
                { "shift", VirtualKeyCode.SHIFT },              // Shift �L�[
                { "control", VirtualKeyCode.CONTROL },          // Ctrl �L�[
                { "menu", VirtualKeyCode.MENU },                // ALT �L�[
                { "pause", VirtualKeyCode.PAUSE },              // Pause �L�[
                { "capital", VirtualKeyCode.CAPITAL },          // CAPS LOCK �L�[
                { "kana", VirtualKeyCode.KANA },                // IME ���ȃ��[�h
                { "hangul", VirtualKeyCode.HANGUL },            // IME �n���O�� ���[�h
                { "junja", VirtualKeyCode.JUNJA },              // IME Junja ���[�h
                { "final", VirtualKeyCode.FINAL },              // IME Final ���[�h
                { "hanja", VirtualKeyCode.HANJA },              // IME Hanja ���[�h
                { "kanji", VirtualKeyCode.KANJI },              // IME �������[�h
                { "escape", VirtualKeyCode.ESCAPE },            // Esc �L�[
                { "convert", VirtualKeyCode.CONVERT },          // IME �ϊ�
                { "nonconvert", VirtualKeyCode.NONCONVERT },    // IME ���ϊ�
                { "accept", VirtualKeyCode.ACCEPT },            // IME �g�p�\
                { "space", VirtualKeyCode.SPACE },              // Space �L�[
                { "prior", VirtualKeyCode.PRIOR },              // PageUp �L�[
                { "next", VirtualKeyCode.NEXT },                // PageDown �L�[
                { "end", VirtualKeyCode.END },                  // End �L�[
                { "home", VirtualKeyCode.HOME },                // Home �L�[
                { "left", VirtualKeyCode.LEFT },                // �������L�[
                { "up", VirtualKeyCode.UP },                    // ������L�[
                { "right", VirtualKeyCode.RIGHT },              // �E�����L�[
                { "down", VirtualKeyCode.DOWN },                // �������L�[
                { "select", VirtualKeyCode.SELECT },            // Select �L�[
                { "print", VirtualKeyCode.PRINT },              // Print �L�[
                { "execute", VirtualKeyCode.EXECUTE },          // Execute �L�[
                { "snapshot", VirtualKeyCode.SNAPSHOT },        // Print Screen �L�[
                { "insert", VirtualKeyCode.INSERT },            // Ins �L�[
                { "delete", VirtualKeyCode.DELETE },            // DEL �L�[
                { "help", VirtualKeyCode.HELP },                // Help �L�[
                { "0", VirtualKeyCode.VK_0 },                   // 0 �L�[
                { "1", VirtualKeyCode.VK_1 },                   // 1 �L�[
                { "2", VirtualKeyCode.VK_2 },                   // 2 �L�[
                { "3", VirtualKeyCode.VK_3 },                   // 3 �L�[
                { "4", VirtualKeyCode.VK_4 },                   // 4 �L�[
                { "5", VirtualKeyCode.VK_5 },                   // 5 �L�[
                { "6", VirtualKeyCode.VK_6 },                   // 6 �L�[
                { "7", VirtualKeyCode.VK_7 },                   // 7 �L�[
                { "8", VirtualKeyCode.VK_8 },                   // 8 �L�[
                { "9", VirtualKeyCode.VK_9 },                   // 9 �L�[
                { "a", VirtualKeyCode.VK_A },                   // A �L�[
                { "b", VirtualKeyCode.VK_B },                   // B �L�[
                { "c", VirtualKeyCode.VK_C },                   // C �L�[
                { "d", VirtualKeyCode.VK_D },                   // D �L�[
                { "e", VirtualKeyCode.VK_E },                   // E �L�[
                { "f", VirtualKeyCode.VK_F },                   // F �L�[
                { "g", VirtualKeyCode.VK_G },                   // G �L�[
                { "h", VirtualKeyCode.VK_H },                   // H �L�[
                { "i", VirtualKeyCode.VK_I },                   // I �L�[
                { "j", VirtualKeyCode.VK_J },                   // J �L�[
                { "k", VirtualKeyCode.VK_K },                   // K �L�[
                { "l", VirtualKeyCode.VK_L },                   // L �L�[
                { "m", VirtualKeyCode.VK_M },                   // M �L�[
                { "n", VirtualKeyCode.VK_N },                   // N �L�[
                { "o", VirtualKeyCode.VK_O },                   // O �L�[
                { "p", VirtualKeyCode.VK_P },                   // P �L�[
                { "q", VirtualKeyCode.VK_Q },                   // Q �L�[
                { "r", VirtualKeyCode.VK_R },                   // R �L�[
                { "s", VirtualKeyCode.VK_S },                   // S �L�[
                { "t", VirtualKeyCode.VK_T },                   // T �L�[
                { "u", VirtualKeyCode.VK_U },                   // U �L�[
                { "v", VirtualKeyCode.VK_V },                   // V �L�[
                { "w", VirtualKeyCode.VK_W },                   // W �L�[
                { "x", VirtualKeyCode.VK_X },                   // X �L�[
                { "y", VirtualKeyCode.VK_Y },                   // Y �L�[
                { "z", VirtualKeyCode.VK_Z },                   // Z �L�[
                { "lwin", VirtualKeyCode.LWIN },                // Windows �̍��L�[
                { "rwin", VirtualKeyCode.RWIN },                // �E�� Windows �L�[
                { "apps", VirtualKeyCode.APPS },                // �A�v���P�[�V���� �L�[
                { "sleep", VirtualKeyCode.SLEEP },              // �X���[�v �L�[
                { "numpad0", VirtualKeyCode.NUMPAD0 },          // �e���L�[�� 0 �L�[
                { "numpad1", VirtualKeyCode.NUMPAD1 },          // �e���L�[�� 1 �L�[
                { "numpad2", VirtualKeyCode.NUMPAD2 },          // �e���L�[�� 2 �L�[
                { "numpad3", VirtualKeyCode.NUMPAD3 },          // �e���L�[�� 3 �L�[
                { "numpad4", VirtualKeyCode.NUMPAD4 },          // �e���L�[�� 4 �L�[
                { "numpad5", VirtualKeyCode.NUMPAD5 },          // �e���L�[�� 5 �L�[
                { "numpad6", VirtualKeyCode.NUMPAD6 },          // �e���L�[�� 6 �L�[
                { "numpad7", VirtualKeyCode.NUMPAD7 },          // �e���L�[�� 7 �L�[
                { "numpad8", VirtualKeyCode.NUMPAD8 },          // �e���L�[�� 8 �L�[
                { "numpad9", VirtualKeyCode.NUMPAD9 },          // �e���L�[�� 9 �L�[
                { "multiply", VirtualKeyCode.MULTIPLY },        // ��Z�L�[
                { "separator", VirtualKeyCode.SEPARATOR },      // ��؂�L���L�[
                { "subtract", VirtualKeyCode.SUBTRACT },        // ���Z�L�[
                { "decimal", VirtualKeyCode.DECIMAL },          // 10 �i�L�[
                { "divide", VirtualKeyCode.DIVIDE },            // ���Z�L�[
                { "f1", VirtualKeyCode.F1 },                    // F1 �L�[
                { "f2", VirtualKeyCode.F2 },                    // F2 �L�[
                { "f3", VirtualKeyCode.F3 },                    // F3 �L�[
                { "f4", VirtualKeyCode.F4 },                    // F4 �L�[
                { "f5", VirtualKeyCode.F5 },                    // F5 �L�[
                { "f6", VirtualKeyCode.F6 },                    // F6 �L�[
                { "f7", VirtualKeyCode.F7 },                    // F7 �L�[
                { "f8", VirtualKeyCode.F8 },                    // F8 �L�[
                { "f9", VirtualKeyCode.F9 },                    // F9 �L�[
                { "f10", VirtualKeyCode.F10 },                  // F10 �L�[
                { "f11", VirtualKeyCode.F11 },                  // F11 �L�[
                { "f12", VirtualKeyCode.F12 },                  // F12 �L�[
                { "f13", VirtualKeyCode.F13 },                  // F13 �L�[
                { "f14", VirtualKeyCode.F14 },                  // F14 �L�[
                { "f15", VirtualKeyCode.F15 },                  // F15 �L�[
                { "f16", VirtualKeyCode.F16 },                  // F16 �L�[
                { "f17", VirtualKeyCode.F17 },                  // F17 �L�[
                { "f18", VirtualKeyCode.F18 },                  // F18 �L�[
                { "f19", VirtualKeyCode.F19 },                  // F19 �L�[
                { "f20", VirtualKeyCode.F20 },                  // F20 �L�[
                { "f21", VirtualKeyCode.F21 },                  // F21 �L�[
                { "f22", VirtualKeyCode.F22 },                  // F22 �L�[
                { "f23", VirtualKeyCode.F23 },                  // F23 �L�[
                { "f24", VirtualKeyCode.F24 },                  // F24 �L�[
                { "numlock", VirtualKeyCode.NUMLOCK },          // NUM LOCK �L�[
                { "scroll", VirtualKeyCode.SCROLL },            // ScrollLock �L�[
                { "lshift", VirtualKeyCode.LSHIFT },            // �� Shift �L�[
                { "rshift", VirtualKeyCode.RSHIFT },            // �E Shift �L�[
                { "lcontrol", VirtualKeyCode.LCONTROL },        // �� Ctrl �L�[
                { "rcontrol", VirtualKeyCode.RCONTROL },        // �E Ctrl �L�[
                { "lmenu", VirtualKeyCode.LMENU },              // �� Alt �L�[
                { "rmenu", VirtualKeyCode.RMENU },              // �E Alt �L�[
                { "browser_back", VirtualKeyCode.BROWSER_BACK },            // �u���E�U�[�̖߂�L�[
                { "browser_forward", VirtualKeyCode.BROWSER_FORWARD },      // �u���E�U�[�̐i�ރL�[
                { "browser_refresh", VirtualKeyCode.BROWSER_REFRESH },      // �u���E�U�[�̍X�V�L�[
                { "browser_stop", VirtualKeyCode.BROWSER_STOP },            // �u���E�U�[�̒�~�L�[
                { "browser_search", VirtualKeyCode.BROWSER_SEARCH },        // �u���E�U�[�̌����L�[
                { "browser_favorites", VirtualKeyCode.BROWSER_FAVORITES },  // �u���E�U�[�̂��C�ɓ���L�[
                { "browser_home", VirtualKeyCode.BROWSER_HOME },            // �u���E�U�[�̃z�[�� �L�[
                { "volume_mute", VirtualKeyCode.VOLUME_MUTE },              // ���ʃ~���[�g �L�[
                { "volume_down", VirtualKeyCode.VOLUME_DOWN },              // ���ʉ�����L�[
                { "volume_up", VirtualKeyCode.VOLUME_UP },                  // ���ʏグ��L�[
                { "media_next_track", VirtualKeyCode.MEDIA_NEXT_TRACK },    // ���̃g���b�N�L�[
                { "media_prev_track", VirtualKeyCode.MEDIA_PREV_TRACK },    // �O�̃g���b�N
                { "media_stop", VirtualKeyCode.MEDIA_STOP },                // ���f�B�A�̒�~�L�[
                { "media_play_pause", VirtualKeyCode.MEDIA_PLAY_PAUSE },    // ���f�B�A�̍Đ�/�ꎞ��~�L�[
                { "launch_media_select", VirtualKeyCode.LAUNCH_MEDIA_SELECT },  // ���f�B�A�̑I���L�[
                { "launch_app1", VirtualKeyCode.LAUNCH_APP1 },              // �A�v���P�[�V���� 1 �̋N���L�[
                { "launch_app2", VirtualKeyCode.LAUNCH_APP2 },              // �A�v���P�[�V���� 2 �̋N���L�[
                { "oem_1", VirtualKeyCode.OEM_1 },                          // ;: �L�[ (�č��W���L�[�{�[�h)
                { "+", VirtualKeyCode.OEM_PLUS },                    // + �L�[
                { ",", VirtualKeyCode.OEM_COMMA },                  // , �L�[
                { "-", VirtualKeyCode.OEM_MINUS },                  // - �L�[
                { ".", VirtualKeyCode.OEM_PERIOD },                // . �L�[
                { "oem_2", VirtualKeyCode.OEM_2 },                          // /? �L�[ (�č��W���L�[�{�[�h)
                { "oem_3", VirtualKeyCode.OEM_3 },                          // `~ �L�[ (�č��W���L�[�{�[�h)
                { "oem_4", VirtualKeyCode.OEM_4 },                          // [{ �L�[ (�č��W���L�[�{�[�h)
                { "oem_5", VirtualKeyCode.OEM_5 },                          // \| �L�[ (�č��W���L�[�{�[�h)
                { "oem_6", VirtualKeyCode.OEM_6 },                          // ]} �L�[ (�č��W���L�[�{�[�h)
                { "oem_7", VirtualKeyCode.OEM_7 },                          // '" �L�[ (�č��W���L�[�{�[�h)
                { "oem_102", VirtualKeyCode.OEM_102 },                      // <> �܂��� \| �L�[ (US �ȊO�̃L�[�{�[�h)
                { "processkey", VirtualKeyCode.PROCESSKEY },                // IME PROCESS �L�[
                { "packet", VirtualKeyCode.PACKET },                        // Unicode ����
                { "attn", VirtualKeyCode.ATTN },                            // Attn �L�[
                { "crsel", VirtualKeyCode.CRSEL },                          // CrSel �L�[
                { "exsel", VirtualKeyCode.EXSEL },                          // ExSel �L�[
                { "play", VirtualKeyCode.PLAY },                            // �Đ��L�[
                { "zoom", VirtualKeyCode.ZOOM },                            // �Y�[�� �L�[
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

        // �o�b�t�@����o�C�g�z��ɃR�s�[
        using (var dataReader = DataReader.FromBuffer(buffer))
        {
            dataReader.ReadBytes(data);
        }

        return data;
    }
}
