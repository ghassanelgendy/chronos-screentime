using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace chronos_screentime.Windows
{
    public partial class ThemedMessageBox : Window
    {
        public enum MessageType
        {
            Information,
            Question,
            Warning,
            Error
        }

        public enum MessageButtons
        {
            OK,
            OKCancel,
            YesNo,
            YesNoCancel
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public ThemedMessageBox()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string message, string title = "Chronos", 
            MessageButtons buttons = MessageButtons.OK, MessageType messageType = MessageType.Information)
        {
            var dialog = new ThemedMessageBox();
            dialog.SetupDialog(message, title, buttons, messageType);
            dialog.ShowDialog();
            return dialog.Result;
        }

        public static MessageBoxResult Show(Window owner, string message, string title = "Chronos", 
            MessageButtons buttons = MessageButtons.OK, MessageType messageType = MessageType.Information)
        {
            var dialog = new ThemedMessageBox();
            dialog.Owner = owner;
            dialog.SetupDialog(message, title, buttons, messageType);
            dialog.ShowDialog();
            return dialog.Result;
        }

        private void SetupDialog(string message, string title, MessageButtons buttons, MessageType messageType)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            
            // Set icon based on message type
            switch (messageType)
            {
                case MessageType.Information:
                    IconText.Text = "ℹ️";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Blue
                    break;
                case MessageType.Question:
                    IconText.Text = "❓";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // Blue
                    break;
                case MessageType.Warning:
                    IconText.Text = "⚠️";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)); // Orange
                    break;
                case MessageType.Error:
                    IconText.Text = "❌";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
                    break;
            }

            // Add buttons based on type
            switch (buttons)
            {
                case MessageButtons.OK:
                    AddButton("OK", MessageBoxResult.OK, "#27AE60");
                    break;
                case MessageButtons.OKCancel:
                    AddButton("OK", MessageBoxResult.OK, "#27AE60");
                    AddButton("Cancel", MessageBoxResult.Cancel, "#95A5A6");
                    break;
                case MessageButtons.YesNo:
                    AddButton("Yes", MessageBoxResult.Yes, "#27AE60");
                    AddButton("No", MessageBoxResult.No, "#E74C3C");
                    break;
                case MessageButtons.YesNoCancel:
                    AddButton("Yes", MessageBoxResult.Yes, "#27AE60");
                    AddButton("No", MessageBoxResult.No, "#E74C3C");
                    AddButton("Cancel", MessageBoxResult.Cancel, "#95A5A6");
                    break;
            }
        }

        private void AddButton(string content, MessageBoxResult result, string backgroundColor)
        {
            var button = new Button
            {
                Content = content,
                Padding = new Thickness(20, 8),
                Margin = new Thickness(5, 0),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Add rounded corners style
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);

            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            button.Style = style;

            button.Click += (sender, e) =>
            {
                Result = result;
                this.Close();
            };

            ButtonPanel.Children.Add(button);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Result = MessageBoxResult.Cancel;
                this.Close();
            }
            base.OnKeyDown(e);
        }
    }
} 