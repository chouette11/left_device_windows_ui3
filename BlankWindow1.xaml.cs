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
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Net;
using Microsoft.UI;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public enum ActionType
    {
        Hotkey,
        Typewrite,
        // 他のアクションタイプがあれば追加
    }

    public class ActionEntity
    {
        public ActionType Type { get; set; }
        public string Value { get; set; }
    }

    public sealed partial class BlankWindow1 : Window
    {
        [DllImport("user32.dll")]
        static extern int GetDpiForWindow(IntPtr hWnd);
        public BlankWindow1()
        {
            nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            double DefaultPixelsPerInch = 96D;

        double dpiScale = GetDpiForWindow(windowHandle) / DefaultPixelsPerInch; AppWindow.ResizeClient(new SizeInt32(
                (int)(770 * dpiScale),
                (int)(450 * dpiScale)));
            this.InitializeComponent();
        }

        private string ipAddress;

        private async void ButtonSend_Click(String value, ActionType actionType)
        {

            if (string.IsNullOrEmpty(ipAddress))
            {
                return;
            }


            ActionEntity action = new ActionEntity { Type = actionType, Value = value };
            await SendData(action, ipAddress);
        }

        private void IpTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ipAddress = ipTextBox.Text; // テキストボックスの内容を変数に保持
        }

        private async Task SendData(ActionEntity action, string ipAddress)
        {
            using (var client = new HttpClient())
            {
                var url = $"http://{ipAddress}:5111/data";

                var data = new Dictionary<string, string>
                {
                    { "data", $"{action.Type.ToString().ToLower()}_+_{action.Value}" }
                };

                var json = JsonSerializer.Serialize(data);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                       
                    }
                    else
                    {
                        
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void buttonScreenShot_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("win shift s", ActionType.Hotkey);
        }

        private void buttonCopy_Click_1(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("ctrl c", ActionType.Hotkey);
        }

        private void buttonCut_Click_1(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("ctrl x", ActionType.Hotkey);
        }

        private void buttonPaste_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("ctrl v", ActionType.Hotkey);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("1", ActionType.Typewrite);
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("2", ActionType.Typewrite);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("3", ActionType.Typewrite);
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("4", ActionType.Typewrite);
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("5", ActionType.Typewrite);
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("6", ActionType.Typewrite);
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("7", ActionType.Typewrite);
        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("8", ActionType.Typewrite);
        }

        private void button9_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("9", ActionType.Typewrite);
        }

        private void button10_Click(object sender, RoutedEventArgs e)
        {
            ButtonSend_Click("0", ActionType.Typewrite);
        }
    }
}
