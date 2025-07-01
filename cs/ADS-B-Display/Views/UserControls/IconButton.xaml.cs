using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ADS_B_Display.Views.UserControls
{
    /// <summary>
    /// Interaction logic for IconButton.xaml
    /// </summary>
    public partial class IconButton : UserControl
    {
        public IconButton()
        {
            InitializeComponent();
        }

        private bool _isPressed = false;


        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(object), typeof(IconButton), new PropertyMetadata(null));

        public static readonly DependencyProperty NormalBrushProperty =
            DependencyProperty.Register(nameof(NormalBrush), typeof(Brush), typeof(IconButton), new PropertyMetadata(Brushes.Transparent));

        public static readonly DependencyProperty PressedBrushProperty =
            DependencyProperty.Register(nameof(PressedBrush), typeof(Brush), typeof(IconButton), new PropertyMetadata(Brushes.LightGray));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(IconButton), new PropertyMetadata(null));

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(IconButton), new PropertyMetadata(null));

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public Brush NormalBrush
        {
            get => (Brush)GetValue(NormalBrushProperty);
            set => SetValue(NormalBrushProperty, value);
        }

        public Brush PressedBrush
        {
            get => (Brush)GetValue(PressedBrushProperty);
            set => SetValue(PressedBrushProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _isPressed = !_isPressed;
            PART_Button.Background = _isPressed ? PressedBrush : NormalBrush;
        }
    }
}
